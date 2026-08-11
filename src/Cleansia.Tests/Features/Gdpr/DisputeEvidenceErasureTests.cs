using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using AppConstants = Cleansia.Core.AppServices.Common.Constants;

namespace Cleansia.Tests.Features.Gdpr;

/// <summary>
/// An erasure must leave the data subject's dispute-evidence files gone from storage, and every other
/// subject's files untouched. Both halves are asserted here because either one alone is satisfiable by a
/// wrong implementation: deleting nothing satisfies "no bystander was touched", and deleting the whole
/// container satisfies "the subject's file is gone".
///
/// <para><b>Ordering is the third assertion, and it is the one with no second chance.</b>
/// <c>DisputeEvidence.FilePath</c> is the only stored pointer to the blob — no export, audit row or
/// sibling table records it, and <c>Dispute.Anonymize()</c> overwrites it. Anonymizing before deleting
/// therefore issues the delete against <c>"[DELETED]"</c>, which succeeds against nothing and leaves a
/// file the database can no longer name. So the test does not merely assert that a delete happened; it
/// asserts the delete carried the REAL path, which is what goes red if the two steps are ever
/// transposed.</para>
///
/// <para>Real repositories over in-memory SQLite, mirroring
/// <c>UserNotificationRetentionAndGdprTests</c> — only the storage edge is a double, because the
/// recorded calls against it are the assertion.</para>
/// </summary>
public sealed class DisputeEvidenceErasureTests : IDisposable
{
    private const string ErasedUserId = "user-erase-1";
    private const string BystanderUserId = "user-keep-1";
    private const string ErasedEvidencePath = "dispute-erased/2f9c1a4b7d6e4f0b9c3a5e8d1f2b4c60.jpg";
    private const string BystanderEvidencePath = "dispute-kept/8a1d3c5e7f9b2d4a6c8e0f1a3b5d7e90.pdf";

    private readonly SqliteConnection _connection;
    private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();
    private readonly List<(string Container, string BlobName)> _deletes = [];

    public DisputeEvidenceErasureTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();

        _blobClientFactory
            .Setup(f => f.GetBlobContainerClient(It.IsAny<string>()))
            .Returns((string container) =>
            {
                var client = new Mock<IBlobContainerClient>();
                client
                    .Setup(c => c.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Returns((string blobName, CancellationToken _) =>
                    {
                        _deletes.Add((container, blobName));
                        return Task.CompletedTask;
                    });
                return client.Object;
            });
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Erasure_Deletes_The_Subjects_Evidence_Blob_By_Its_Real_Path_Then_Clears_The_Path()
    {
        await SeedAsync();

        await EraseAsync(ErasedUserId);

        // Contains, not an exact-list equality: over-deletion is the sibling test's job, and pinning it
        // here too would leave both directions killed by the same mutation.
        Assert.Contains((AppConstants.BlobContainers.DisputeEvidence, ErasedEvidencePath), _deletes);

        var evidence = await ReadEvidenceAsync(ErasedUserId);
        Assert.Equal(AnonymizationMarker.Value, evidence.FilePath);
        Assert.Equal(AnonymizationMarker.Value, evidence.FileName);
    }

    [Fact]
    public async Task Erasure_Leaves_Another_Subjects_Evidence_Blob_And_Row_Untouched()
    {
        await SeedAsync();

        await EraseAsync(ErasedUserId);

        Assert.DoesNotContain(
            (AppConstants.BlobContainers.DisputeEvidence, BystanderEvidencePath),
            _deletes);

        var evidence = await ReadEvidenceAsync(BystanderUserId);
        Assert.Equal(BystanderEvidencePath, evidence.FilePath);
        Assert.Equal("bystander-receipt.pdf", evidence.FileName);
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
            new OutboxMessageRepository(ctx),
            Mock.Of<IStripeClient>(),
            _blobClientFactory.Object,
            NullLogger<GdprDeletionService>.Instance);

        var result = await service.DeleteUserAccountAsync(
            userId, "gdpr_erasure_test", _ => ("test-actor", null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task<DisputeEvidence> ReadEvidenceAsync(string userId)
    {
        await using var ctx = NewContext();
        return await ctx.Set<DisputeEvidence>()
            .IgnoreQueryFilters()
            .Join(
                ctx.Set<Dispute>().IgnoreQueryFilters().Where(d => d.UserId == userId),
                e => e.DisputeId,
                d => d.Id,
                (e, _) => e)
            .SingleAsync();
    }

    private async Task SeedAsync()
    {
        await using (var schema = NewContext())
        {
            await schema.Database.EnsureCreatedAsync();
        }

        await using var ctx = NewContext();

        ctx.Add(NewUser(ErasedUserId, "erase-me@cleansia.test"));
        ctx.Add(NewUser(BystanderUserId, "keep-me@cleansia.test"));
        ctx.Add(NewDispute(ErasedUserId, "evidence-of-the-mess.jpg", ErasedEvidencePath));
        ctx.Add(NewDispute(BystanderUserId, "bystander-receipt.pdf", BystanderEvidencePath));

        await ctx.CommitAsync(CancellationToken.None);
    }

    private static User NewUser(string userId, string email)
    {
        var user = User.CreateWithPassword(email, "Test-password-1!", "Data", "Subject", UserProfile.Customer);
        user.Id = userId;
        return user;
    }

    private static Dispute NewDispute(string userId, string fileName, string filePath)
    {
        var dispute = new Dispute(
            orderId: $"order-{userId}",
            userId: userId,
            reason: DisputeReason.QualityIssue,
            description: "The bathroom was left dirty.",
            createdBy: userId);
        dispute.AddEvidence(fileName, filePath, userId);
        return dispute;
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
