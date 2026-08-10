using System.Text.Json;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.DeadLettering;
using Cleansia.Core.Domain.Enums;
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
/// T-0583 — after an erasure, the data subject's e-mail and real name must not survive in
/// <see cref="DeadLetter.RawBody"/>. A poisoned <c>send-email</c> body is the one queue body that carries
/// them in the clear (<see cref="SendEmailMessage"/> = <c>Email</c> + <c>UserName</c> + a live token), and
/// the row has no navigation to a user, so the walk is id-keyed exactly like the payout-details erasure.
///
/// <para><b>Both directions are asserted, and neither alone is worth anything.</b> Deleting nothing
/// satisfies "no bystander was touched"; deleting the table satisfies "the subject's body is gone". The
/// second direction has two distinct failure modes here, so it gets two tests: another <i>subject's</i>
/// send-email row (the match must read the key's user segment, not the queue) and the same subject's
/// <c>notifications-dispatch</c> / <c>live-activity-dispatch</c> rows, whose bodies contain his
/// <c>UserId</c> literally — so a <c>RawBody LIKE '%userId%'</c> match would take them (the match must
/// read the queue, not just the id).</para>
///
/// <para>The fixture bodies are the REAL wire shapes: a <c>QueueEnvelope&lt;SendEmailMessage&gt;</c>
/// serialized with the same camelCase options <c>AzureStorageQueueClient</c> uses, carrying an address and
/// a name the assertions then search for. Asserting the absence of a field nothing populated proves
/// nothing, so the seed is read back and checked to contain them before the erasure runs.</para>
///
/// <para><b>The guarantee is stated with its boundary, not as completeness.</b> An erasure keyed on the
/// subject reaches every dead-letter body it can identify; a body that does not parse carries no subject
/// handle anywhere, so it is out of reach by construction and is bounded by the absolute-clock retention
/// sweep instead.
/// <see cref="Erasure_Reaches_A_Truncated_Body_That_Kept_Its_Key_And_Cannot_Reach_One_With_No_Handle"/>
/// is that boundary, asserted rather than left silent, and asserted at its real width — the line is the
/// absence of a handle, not the absence of valid JSON.</para>
///
/// <para>Real repositories over in-memory SQLite, mirroring <c>DisputeEvidenceErasureTests</c>; only the
/// external edges (blobs, Stripe) are doubles.</para>
/// </summary>
public sealed class DeadLetterErasureTests : IDisposable
{
    private const string ErasedUserId = "user-erase-dl-1";
    private const string ErasedEmail = "milada.novotna@cleansia.test";
    private const string ErasedFirstName = "Milada";
    private const string ErasedLastName = "Novotna";

    private const string BystanderUserId = "user-keep-dl-1";
    private const string BystanderEmail = "tomas.svoboda@cleansia.test";

    private const string PoisonTenantId = "tenant-alpha";

    private static readonly JsonSerializerOptions QueueJson =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly SqliteConnection _connection;
    private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();

    public DeadLetterErasureTests()
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
    public async Task Erasure_Removes_The_Subjects_Poisoned_Send_Email_Body_With_Its_Address_And_Name()
    {
        await SeedAsync();

        var seeded = await ReadRowsAsync();
        Assert.Contains(seeded, r => r.RawBody.Contains(ErasedEmail, StringComparison.Ordinal));
        Assert.Contains(seeded, r => r.RawBody.Contains(ErasedLastName, StringComparison.Ordinal));

        await EraseAsync(ErasedUserId);

        var remaining = await ReadRowsAsync();
        Assert.DoesNotContain(remaining, r => r.RawBody.Contains(ErasedEmail, StringComparison.Ordinal));
        Assert.DoesNotContain(remaining, r => r.RawBody.Contains(ErasedFirstName, StringComparison.Ordinal));
        Assert.DoesNotContain(remaining, r => r.RawBody.Contains(ErasedLastName, StringComparison.Ordinal));
    }

    /// <summary>
    /// The bare, pre-envelope body the send-email consumer still dual-reads (ADR-0002 D2.1a): no
    /// <c>messageKey</c> to parse, so the only handle on the subject is the payload's own <c>userId</c>.
    /// It carries the same address and name as an enveloped one and must go the same way.
    /// </summary>
    [Fact]
    public async Task Erasure_Removes_A_Bare_Pre_Envelope_Body_Keyed_By_Its_Payload_UserId()
    {
        await SeedAsync();

        await EraseAsync(ErasedUserId);

        var remaining = await ReadRowsAsync();
        Assert.DoesNotContain(remaining, r => r.RawBody == BareSendEmailBody(ErasedUserId, ErasedEmail));
    }

    [Fact]
    public async Task Erasure_Leaves_Another_Subjects_Poisoned_Send_Email_Row_Byte_Exact()
    {
        await SeedAsync();
        var expected = EnvelopedSendEmailBody(BystanderUserId, BystanderEmail, "Tomas", "Svoboda", null);

        await EraseAsync(ErasedUserId);

        var remaining = await ReadRowsAsync();
        Assert.Contains(remaining, r => r.SourceQueue == QueueNames.SendEmail && r.RawBody == expected);
    }

    /// <summary>
    /// The over-deletion half that a substring scan gets wrong. Both bodies contain the erased subject's
    /// <c>UserId</c> verbatim, and both are D3 recovery sources that carry no identifier of their own —
    /// <see cref="SendPushNotificationMessage"/> and <see cref="SendLiveActivityUpdateMessage"/> both
    /// forbid PII in the payload by contract, and after the erasure the <c>UserId</c> resolves to an
    /// anonymized <c>User</c> row. They stay.
    /// </summary>
    [Fact]
    public async Task Erasure_Leaves_The_Subjects_Push_And_Live_Activity_Rows_On_Other_Queues()
    {
        await SeedAsync();

        await EraseAsync(ErasedUserId);

        var remaining = await ReadRowsAsync();
        Assert.Contains(remaining, r => r.SourceQueue == QueueNames.NotificationsDispatch);
        Assert.Contains(remaining, r => r.SourceQueue == QueueNames.LiveActivityDispatch);
        Assert.All(
            remaining.Where(r => r.SourceQueue != QueueNames.SendEmail),
            r => Assert.Contains(ErasedUserId, r.RawBody, StringComparison.Ordinal));
    }

    /// <summary>
    /// The stated hole, pinned so it stays stated — and pinned at its real width. A truncated body still
    /// holds the address and the name; when the cut leaves an identity token behind, the erasure takes the
    /// row anyway, so the hole is only the residue with NO token at all — the case the alert path reports
    /// as <c>&lt;unparseable&gt;</c>. Nothing here guesses from a loose substring; the absolute-clock
    /// retention sweep is what bounds this remainder.
    ///
    /// <para>Both halves are asserted together: an erasure that simply gave up on every unparseable body
    /// would pass the second half alone, and the first half is what says the boundary is drawn at the
    /// absence of a handle rather than at the absence of valid JSON. Seeded on their own so the table-wide
    /// assertions above keep their full strength over the rows that ARE identifiable.</para>
    /// </summary>
    [Fact]
    public async Task Erasure_Reaches_A_Truncated_Body_That_Kept_Its_Key_And_Cannot_Reach_One_With_No_Handle()
    {
        await SeedAsync();
        var keyedButTruncated = TruncatedKeyedSendEmailBody(ErasedUserId, ErasedEmail, ErasedLastName);
        var handleFree = TruncatedHandleFreeSendEmailBody(ErasedUserId, ErasedEmail, ErasedLastName);

        Assert.All(
            new[] { keyedButTruncated, handleFree },
            body =>
            {
                Assert.Contains(ErasedEmail, body, StringComparison.Ordinal);
                Assert.Contains(ErasedLastName, body, StringComparison.Ordinal);
                Assert.ThrowsAny<JsonException>(() => JsonDocument.Parse(body));
            });
        Assert.DoesNotContain(ErasedUserId, handleFree, StringComparison.Ordinal);

        await using (var ctx = NewContext())
        {
            ctx.Add(Poisoned(QueueNames.SendEmail, keyedButTruncated, tenantId: null));
            ctx.Add(Poisoned(QueueNames.SendEmail, handleFree, tenantId: null));
            await ctx.CommitAsync(CancellationToken.None);
        }

        await EraseAsync(ErasedUserId);

        var remaining = await ReadRowsAsync();
        Assert.DoesNotContain(remaining, r => r.RawBody == keyedButTruncated);
        Assert.Contains(remaining, r => r.RawBody == handleFree);
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
            new CartRepository(ctx),
            new UserConsentRepository(ctx),
            new GdprRequestRepository(ctx),
            new DisputeRepository(ctx),
            new SavedAddressRepository(ctx, session),
            new OrderEmployeePayRepository(ctx),
            new RecurringBookingTemplateRepository(ctx),
            new UserNotificationRepository(ctx),
            new DeadLetterRepository(ctx),
            Mock.Of<IStripeClient>(),
            _blobClientFactory.Object,
            NullLogger<GdprDeletionService>.Instance);

        var result = await service.DeleteUserAccountAsync(
            userId, "gdpr_erasure_test", _ => ("test-actor", null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task<List<DeadLetter>> ReadRowsAsync()
    {
        await using var ctx = NewContext();
        return await ctx.Set<DeadLetter>().IgnoreQueryFilters().ToListAsync();
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

        // Stamped with a real tenant: a poison consumer records the body verbatim and derives no tenant
        // from it, so these rows carry whatever tenant was ambient on the Functions host — which is not
        // the erasing request's. A tenant-scoped read cannot see this row.
        ctx.Add(Poisoned(
            QueueNames.SendEmail,
            EnvelopedSendEmailBody(ErasedUserId, ErasedEmail, ErasedFirstName, ErasedLastName, PoisonTenantId),
            PoisonTenantId));

        ctx.Add(Poisoned(QueueNames.SendEmail, BareSendEmailBody(ErasedUserId, ErasedEmail), tenantId: null));

        ctx.Add(Poisoned(
            QueueNames.SendEmail,
            EnvelopedSendEmailBody(BystanderUserId, BystanderEmail, "Tomas", "Svoboda", null),
            tenantId: null));

        ctx.Add(Poisoned(QueueNames.NotificationsDispatch, PushBody(ErasedUserId), tenantId: null));
        ctx.Add(Poisoned(QueueNames.LiveActivityDispatch, LiveActivityBody(ErasedUserId), tenantId: null));

        await ctx.CommitAsync(CancellationToken.None);
    }

    private static DeadLetter Poisoned(string sourceQueue, string rawBody, string? tenantId)
    {
        var row = DeadLetter.Create(sourceQueue, rawBody, error: null);
        row.TenantId = tenantId;
        return row;
    }

    private static string EnvelopedSendEmailBody(
        string userId, string email, string firstName, string lastName, string? tenantId)
    {
        const string code = "9f2c41ae-reset-token";
        var payload = new SendEmailMessage(
            EmailType.ResetPassword, email, $"{firstName} {lastName}", code, "cs", userId, tenantId);
        var envelope = new QueueEnvelope<SendEmailMessage>(
            MessageKeys.Email(EmailType.ResetPassword, userId, MessageKeys.HashCode(code)), tenantId, payload);

        return JsonSerializer.Serialize(envelope, QueueJson);
    }

    /// <summary>
    /// An enveloped body cut mid-flight. The <c>messageKey</c> leads the wire, so it survives a cut that
    /// destroys the JSON — the row is still this subject's, unambiguously, and still goes.
    /// </summary>
    private static string TruncatedKeyedSendEmailBody(string userId, string email, string lastName)
    {
        var whole = EnvelopedSendEmailBody(userId, email, ErasedFirstName, lastName, null);
        var cutAfterTheName = whole.IndexOf(lastName, StringComparison.Ordinal) + lastName.Length;

        return whole[..cutAfterTheName];
    }

    /// <summary>
    /// A bare payload cut mid-flight: <c>SendEmailMessage</c> serializes <c>UserId</c> LAST and a bare body
    /// has no envelope key at all, so a cut just past the name takes every identity token with it and
    /// leaves the address and the name behind. This is the residue nothing can key.
    /// </summary>
    private static string TruncatedHandleFreeSendEmailBody(string userId, string email, string lastName)
    {
        var whole = BareSendEmailBody(userId, email);
        var cutAfterTheName = whole.IndexOf(lastName, StringComparison.Ordinal) + lastName.Length;

        return whole[..cutAfterTheName];
    }

    private static string BareSendEmailBody(string userId, string email) =>
        JsonSerializer.Serialize(
            new SendEmailMessage(
                EmailType.ConfirmationEmail, email, $"{ErasedFirstName} {ErasedLastName}", "1c4d-confirm", "cs", userId),
            QueueJson);

    private static string PushBody(string userId)
    {
        const string eventKey = "order.completed";
        var payload = new SendPushNotificationMessage(
            userId, eventKey, new Dictionary<string, string> { ["orderNumber"] = "CL-2026-0042" }, null);

        return JsonSerializer.Serialize(
            new QueueEnvelope<SendPushNotificationMessage>(
                MessageKeys.Push(userId, eventKey, "order-dl-1"), null, payload),
            QueueJson);
    }

    private static string LiveActivityBody(string userId)
    {
        const string eventKey = "update";
        var now = DateTimeOffset.UtcNow;
        var payload = new SendLiveActivityUpdateMessage(
            userId, "order-dl-1", eventKey, "CL-2026-0042", now, now.AddHours(3), now, null);

        return JsonSerializer.Serialize(
            new QueueEnvelope<SendLiveActivityUpdateMessage>(
                MessageKeys.LiveActivity("order-dl-1", eventKey, 3), null, payload),
            QueueJson);
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
