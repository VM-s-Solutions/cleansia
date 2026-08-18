using System.Text.Json;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Gdpr;

/// <summary>
/// T-0586 — after an erasure, the data subject's e-mail and real name must not survive in
/// <see cref="OutboxMessage.Body"/>. The body is the already-serialized wire payload stored verbatim, so a
/// <c>send-email</c> row holds <see cref="SendEmailMessage"/> whole: address, real name and the raw
/// confirmation/reset token. Byte-identical content to the dead-letter row T-0583 erased, one table over.
///
/// <para><b>No sweep bounds this table for the rows that matter.</b> <c>PruneOutbox</c> deletes only
/// <see cref="OutboxMessageStatus.Dispatched"/> rows, and only after its retention window; a row retired by
/// <c>MarkFailed</c> is never pruned at all, by explicit design (it is still re-drivable). So the erasure
/// must take the subject's row in EVERY status, and
/// <see cref="Erasure_Removes_The_Subjects_Row_In_Every_Status_Including_The_Retry_Exhausted_One"/> is what
/// says so.</para>
///
/// <para><b>The handle is the <c>MessageKey</c> COLUMN, not the body.</b> That is the whole difference from
/// the dead-letter walk: the subject is read structurally out of a first-class column, using the frozen
/// <see cref="MessageKeys.Email"/> formula with an empty code segment as the per-subject prefix, so no body
/// parsing exists here to get wrong. The <c>Contains</c> in the query is index-assisted narrowing only —
/// <see cref="Erasure_Leaves_A_Bystander_Whose_UserId_Contains_The_Subjects"/> is the case that separates
/// the two, and a substring match alone fails it.</para>
///
/// <para><b>Both directions are asserted, and neither alone is worth anything.</b> Deleting nothing
/// satisfies "no bystander was touched"; deleting the table satisfies "the subject's body is gone".</para>
///
/// <para>Real repositories over in-memory SQLite, mirroring <c>DeadLetterErasureTests</c>; only the external
/// edges (blobs, Stripe) are doubles.</para>
/// </summary>
public sealed class OutboxErasureTests : IDisposable
{
    private const string ErasedUserId = "user-erase-ob-1";
    private const string ErasedEmail = "milada.novotna@cleansia.test";
    private const string ErasedFirstName = "Milada";
    private const string ErasedLastName = "Novotna";

    // Deliberately CONTAINS the erased id: a `MessageKey LIKE '%userId%'` match takes this row, the frozen
    // per-subject prefix does not.
    private const string BystanderUserId = "x-user-erase-ob-1";
    private const string BystanderEmail = "tomas.svoboda@cleansia.test";

    private const string EnqueueTenantId = "tenant-alpha";

    private static readonly JsonSerializerOptions QueueJson =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly SqliteConnection _connection;
    private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();

    public OutboxErasureTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();

        _blobClientFactory
            .Setup(f => f.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(Mock.Of<IBlobContainerClient>());
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Erasure_Removes_The_Subjects_Send_Email_Body_With_Its_Address_And_Name()
    {
        await SeedAsync();

        var seeded = await ReadRowsAsync();
        Assert.Contains(seeded, r => r.Body.Contains(ErasedEmail, StringComparison.Ordinal));
        Assert.Contains(seeded, r => r.Body.Contains(ErasedLastName, StringComparison.Ordinal));

        await EraseAsync(ErasedUserId);

        var remaining = await ReadRowsAsync();
        Assert.DoesNotContain(remaining, r => r.Body.Contains(ErasedEmail, StringComparison.Ordinal));
        Assert.DoesNotContain(remaining, r => r.Body.Contains(ErasedFirstName, StringComparison.Ordinal));
        Assert.DoesNotContain(remaining, r => r.Body.Contains(ErasedLastName, StringComparison.Ordinal));
    }

    /// <summary>
    /// Status is not part of the handle, and it must not become part of it. A <c>Pending</c> row is still
    /// going to be sent, a <c>Dispatched</c> row keeps its verbatim body until the retention window closes,
    /// and a <c>Failed</c> row — retry budget exhausted — has no clock on it at all: <c>PruneOutbox</c>
    /// refuses to prune it by design. All three carry the same address and name.
    /// </summary>
    [Fact]
    public async Task Erasure_Removes_The_Subjects_Row_In_Every_Status_Including_The_Retry_Exhausted_One()
    {
        await SeedAsync();

        var seeded = await ReadRowsAsync();
        Assert.Equal(
            [OutboxMessageStatus.Pending, OutboxMessageStatus.Dispatched, OutboxMessageStatus.Failed],
            seeded.Where(r => r.QueueName == QueueNames.SendEmail && r.Body.Contains(ErasedEmail, StringComparison.Ordinal))
                .Select(r => r.Status)
                .Order()
                .ToArray());

        await EraseAsync(ErasedUserId);

        var remaining = await ReadRowsAsync();
        Assert.DoesNotContain(remaining, r => r.MessageKey.StartsWith($"email:reset:{ErasedUserId}:", StringComparison.Ordinal));
        Assert.DoesNotContain(remaining, r => r.MessageKey.StartsWith($"email:confirmation:{ErasedUserId}:", StringComparison.Ordinal));
    }

    /// <summary>
    /// The over-deletion half a substring scan gets wrong. This bystander's user id CONTAINS the erased
    /// subject's, so his key and his body both contain it verbatim — and the walk's <c>Contains</c> narrowing
    /// therefore hands his row to the decision. Only the frozen per-subject prefix separates them.
    /// </summary>
    [Fact]
    public async Task Erasure_Leaves_A_Bystander_Whose_UserId_Contains_The_Subjects()
    {
        await SeedAsync();
        var expected = SendEmailBody(BystanderUserId, BystanderEmail, "Tomas", "Svoboda");

        await EraseAsync(ErasedUserId);

        var remaining = await ReadRowsAsync();
        Assert.Contains(remaining, r => r.QueueName == QueueNames.SendEmail && r.Body == expected);
    }

    /// <summary>
    /// The other over-deletion half. A <c>push:{userId}:…</c> key carries the erased subject's id in its own
    /// first segment and the body repeats it, so a queue-blind key match takes it — and it is a real recovery
    /// source that carries no PII by contract (<see cref="SendPushNotificationMessage"/>). It stays.
    /// </summary>
    [Fact]
    public async Task Erasure_Leaves_The_Subjects_Rows_On_Other_Queues()
    {
        await SeedAsync();

        await EraseAsync(ErasedUserId);

        var remaining = await ReadRowsAsync();
        Assert.Contains(remaining, r => r.QueueName == QueueNames.NotificationsDispatch);
        Assert.Contains(remaining, r => r.QueueName == QueueNames.GenerateReceipt);
        Assert.All(
            remaining.Where(r => r.QueueName == QueueNames.NotificationsDispatch),
            r => Assert.Contains(ErasedUserId, r.MessageKey, StringComparison.Ordinal));
    }

    /// <summary>
    /// The boundary, asserted rather than left silent. The <c>MessageKey</c> column is the ONLY handle this
    /// walk has — it deliberately never reads <see cref="OutboxMessage.Body"/>, because a body match would
    /// take the push and receipt rows above. So a <c>send-email</c> row whose key was not built by the frozen
    /// <see cref="MessageKeys.Email"/> formula is out of reach even though its body carries the address in
    /// full. Nothing produces such a row today (<c>EmailDispatch</c> is the only writer of a send-email
    /// envelope), which is what makes this a limit of the handle rather than a live hole — and it is the
    /// sentence that has to be re-checked the day a second producer appears.
    /// </summary>
    [Fact]
    public async Task Erasure_Cannot_Reach_A_Send_Email_Row_Whose_Key_Does_Not_Follow_The_Frozen_Formula()
    {
        await SeedAsync();
        const string offFormulaKey = "email-legacy-replay-1";
        var body = SendEmailBody(ErasedUserId, ErasedEmail, ErasedFirstName, ErasedLastName);

        await using (var ctx = NewContext())
        {
            ctx.Add(OutboxMessage.Create(QueueNames.SendEmail, offFormulaKey, body, tenantId: null));
            await ctx.CommitAsync(CancellationToken.None);
        }

        await EraseAsync(ErasedUserId);

        var remaining = await ReadRowsAsync();
        Assert.Contains(remaining, r => r.MessageKey == offFormulaKey);
    }

    private async Task EraseAsync(string userId)
    {
        await using var ctx = NewContext();
        var session = new TestUserSessionProvider(userId, $"{userId}@cleansia.test");
        var service = new GdprDeletionService(
            new UserRepository(ctx),
            new OrderRepository(ctx),
            new EmployeeDocumentRepository(ctx),
            new EmployeeInvoiceRepository(ctx),
            new EmployeePayoutDetailsRepository(ctx),
            new UserMembershipRepository(ctx),
            new OrderPhotoRepository(ctx),
            new DeviceRepository(ctx, session),
            new LiveActivityTokenRepository(ctx),
            new CartRepository(ctx),
            new UserConsentRepository(ctx),
            new GdprRequestRepository(ctx),
            new DisputeRepository(ctx),
            new SavedAddressRepository(ctx, session),
            new OrderEmployeePayRepository(ctx),
            new RecurringBookingTemplateRepository(ctx),
            new UserNotificationRepository(ctx),
            new DeadLetterRepository(ctx),
            new OutboxMessageRepository(ctx),
            Mock.Of<IRefreshTokenService>(),
            Mock.Of<IStripeClient>(),
            _blobClientFactory.Object,
            NullLogger<GdprDeletionService>.Instance);

        var result = await service.DeleteUserAccountAsync(
            userId, "gdpr_erasure_test", _ => ("test-actor", null), deferEmployeeErasure: false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task<List<OutboxMessage>> ReadRowsAsync()
    {
        await using var ctx = NewContext();
        return await ctx.Set<OutboxMessage>().IgnoreQueryFilters().ToListAsync();
    }

    private async Task SeedAsync()
    {
        await using (var schema = NewContext())
        {
            await schema.Database.EnsureCreatedAsync();
        }

        await using var ctx = NewContext();

        ctx.Add(NewUser(ErasedUserId, ErasedEmail, ErasedFirstName, ErasedLastName));
        ctx.Add(NewUser(BystanderUserId, BystanderEmail, "Tomas", "Svoboda"));

        // Stamped with a real tenant: the row's tenant is read back out of the envelope the producer wrote,
        // so it carries the SUBJECT's tenant — not the erasing request's, which here has none. A
        // tenant-scoped read cannot see this row.
        ctx.Add(SendEmailRow(ErasedUserId, ErasedEmail, ErasedFirstName, ErasedLastName, EmailType.ResetPassword, EnqueueTenantId));

        var dispatched = SendEmailRow(
            ErasedUserId, ErasedEmail, ErasedFirstName, ErasedLastName, EmailType.ConfirmationEmail, tenantId: null);
        dispatched.MarkDispatched(DateTimeOffset.UtcNow);
        ctx.Add(dispatched);

        var failed = SendEmailRow(
            ErasedUserId, ErasedEmail, ErasedFirstName, ErasedLastName, EmailType.OrderStatusUpdate, tenantId: null);
        failed.MarkFailed("smtp refused");
        ctx.Add(failed);

        ctx.Add(SendEmailRow(BystanderUserId, BystanderEmail, "Tomas", "Svoboda", EmailType.ResetPassword, tenantId: null));

        ctx.Add(PushRow(ErasedUserId));
        ctx.Add(ReceiptRow());

        await ctx.CommitAsync(CancellationToken.None);
    }

    private static OutboxMessage SendEmailRow(
        string userId, string email, string firstName, string lastName, EmailType emailType, string? tenantId) =>
        OutboxMessage.Create(
            QueueNames.SendEmail,
            MessageKeys.Email(emailType, userId, MessageKeys.HashCode(CodeFor(userId, emailType))),
            SendEmailBody(userId, email, firstName, lastName, emailType),
            tenantId);

    private static string SendEmailBody(
        string userId, string email, string firstName, string lastName, EmailType emailType = EmailType.ResetPassword)
    {
        var code = CodeFor(userId, emailType);
        var payload = new SendEmailMessage(
            emailType, email, $"{firstName} {lastName}", code, "cs", userId, null);

        return JsonSerializer.Serialize(
            new QueueEnvelope<SendEmailMessage>(
                MessageKeys.Email(emailType, userId, MessageKeys.HashCode(code)), null, payload),
            QueueJson);
    }

    private static string CodeFor(string userId, EmailType emailType) => $"{userId}-{emailType}-raw-token";

    private static OutboxMessage PushRow(string userId)
    {
        const string eventKey = "order.completed";
        var payload = new SendPushNotificationMessage(
            userId, eventKey, new Dictionary<string, string> { ["orderNumber"] = "CL-2026-0042" }, null);
        var key = MessageKeys.Push(userId, eventKey, "order-ob-1");

        return OutboxMessage.Create(
            QueueNames.NotificationsDispatch,
            key,
            JsonSerializer.Serialize(new QueueEnvelope<SendPushNotificationMessage>(key, null, payload), QueueJson),
            tenantId: null);
    }

    private static OutboxMessage ReceiptRow()
    {
        const string orderId = "order-ob-1";
        var key = MessageKeys.Receipt(orderId);

        return OutboxMessage.Create(
            QueueNames.GenerateReceipt,
            key,
            JsonSerializer.Serialize(
                new QueueEnvelope<GenerateReceiptMessage>(key, null, new GenerateReceiptMessage(orderId, "cs")),
                QueueJson),
            tenantId: null);
    }

    private static User NewUser(string userId, string email, string firstName, string lastName)
    {
        var user = User.CreateWithPassword(email, "Test-password-1!", firstName, lastName, UserProfile.Customer);
        user.Id = userId;
        return user;
    }

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(null));

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
