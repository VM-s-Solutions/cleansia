using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Gdpr;

/// <summary>
/// One deletion request per subject until it is done. A <c>Failed</c> row is not done — the daily sweep
/// or an admin retry finishes it — so a second self-service filing is refused as already pending.
/// Without that, the second filing erased the subject on a row of its own and the sweep then re-walked
/// the erased subject through the first, leaving two Completed rows for one erasure.
///
/// <para>Both directions: a Completed row blocks nothing, and a Failed row of another request type is
/// not a deletion in progress. Real repositories over in-memory SQLite, mirroring
/// <c>ErasureBlockingOrderStatusTests</c> — the verdict has to come from the service.</para>
/// </summary>
public sealed class OpenDeletionRequestBlocksRefilingTests : IDisposable
{
    private const string SubjectUserId = "user-refile-1";
    private const string SubjectEmail = "milena.dvorakova@cleansia.test";

    private readonly SqliteConnection _connection;
    private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();

    public OpenDeletionRequestBlocksRefilingTests()
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

    [Theory]
    [InlineData(GdprRequestStatus.Pending, true)]
    [InlineData(GdprRequestStatus.Processing, true)]
    [InlineData(GdprRequestStatus.Failed, true)]
    [InlineData(GdprRequestStatus.Completed, false)]
    public async Task A_Deletion_Request_Not_Yet_Completed_Refuses_A_Second_Filing(GdprRequestStatus status, bool expectRefused)
    {
        await SeedAsync(ExistingRequest(status, GdprRequest.DeletionRequestType));

        var result = await DeleteAsync();

        Assert.Equal(expectRefused, result.IsFailure);

        await using var verify = NewContext();
        var requests = await verify.GdprRequests.ToListAsync();
        var user = await verify.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == SubjectUserId);

        if (expectRefused)
        {
            Assert.Equal(BusinessErrorMessage.GdprDeletionAlreadyPending, result.Error!.Message);
            var untouched = Assert.Single(requests);
            Assert.Equal(status, untouched.Status);
            Assert.Equal(SubjectEmail, user.Email);
            Assert.True(user.IsActive);
        }
        else
        {
            Assert.Equal(2, requests.Count);
            Assert.All(requests, r => Assert.Equal(GdprRequestStatus.Completed, r.Status));
            Assert.False(user.IsActive);
        }
    }

    [Fact]
    public async Task A_Failed_Row_Of_Another_Request_Type_Is_Not_A_Deletion_In_Progress()
    {
        await SeedAsync(ExistingRequest(GdprRequestStatus.Failed, GdprAuditReasons.ExportRequestType));

        var result = await DeleteAsync();

        Assert.True(result.IsSuccess);
    }

    private static GdprRequest ExistingRequest(GdprRequestStatus status, string requestType)
    {
        var request = GdprRequest.Create(SubjectUserId, requestType);
        request.Id = "gdpr-request-refile-existing";
        return status switch
        {
            GdprRequestStatus.Pending => request,
            GdprRequestStatus.Processing => request.MarkProcessing(),
            GdprRequestStatus.Failed => request.MarkFailed("self", "DbUpdateException: boom"),
            GdprRequestStatus.Completed => request.MarkCompleted("self"),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };
    }

    private async Task<BusinessResult> DeleteAsync()
    {
        await using var ctx = NewContext();
        var session = new TestUserSessionProvider(SubjectUserId, SubjectEmail);

        var service = new GdprDeletionService(
            new UserRepository(ctx),
            new OrderRepository(ctx),
            new EmployeeDocumentRepository(ctx),
            new DocumentDeletionRequestRepository(ctx),
            new EmployeeInvoiceRepository(ctx),
            new CreditAccountRepository(ctx),
            new EmployeePayoutDetailsRepository(ctx),
            new UserMembershipRepository(ctx),
            new UserStripeCustomerRepository(ctx),
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
            new CustomerActionAuditRepository(ctx),
            new WorkContractAcceptanceRepository(ctx),
            Mock.Of<IRefreshTokenService>(),
            Mock.Of<IStripeClient>(),
            _blobClientFactory.Object,
            Mock.Of<IAppConfigurationProvider>(),
            new ErasureAttempt(),
            new ArchiveWriteGate(),
            NullLogger<GdprDeletionService>.Instance);

        var result = await service.DeleteUserAccountAsync(
            SubjectUserId, GdprAuditReasons.SelfDeletion, _ => (GdprAuditReasons.SelfActor, null), deferEmployeeErasure: true, CancellationToken.None);

        await ctx.CommitAsync(CancellationToken.None);
        return result;
    }

    private async Task SeedAsync(GdprRequest existing)
    {
        await using (var schema = NewContext())
        {
            await schema.Database.EnsureCreatedAsync();
        }

        await using var ctx = NewContext();

        var user = User.CreateWithPassword(SubjectEmail, "Test-password-1!", "Milena", "Dvorakova", UserProfile.Customer);
        user.Id = SubjectUserId;
        ctx.Add(user);
        ctx.Add(existing);

        await ctx.CommitAsync(CancellationToken.None);
    }

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(TestTenants.Default));

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
