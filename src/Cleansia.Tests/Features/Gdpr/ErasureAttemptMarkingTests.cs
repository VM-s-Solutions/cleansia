using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
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
/// Where the deletion service marks the attempt, and what the retry entry point does with a row. A first
/// deletion marks it only once the blocking checks have passed — a refusal is a business answer with
/// nothing to record — under the id of the request row it staged, so the out-of-band record lands under
/// the same id. A retry marks it BEFORE the checks, because the row already exists and every outcome
/// belongs on it; it completes that row rather than filing another, stamps the retry reason on the
/// subject, and answers not-found for an id that is not a request. Real repositories over SQLite, the
/// <c>ErasureBlockingOrderStatusTests</c> shape.
/// </summary>
public sealed class ErasureAttemptMarkingTests : IDisposable
{
    private const string SubjectUserId = "user-attempt-1";
    private const string SubjectEmail = "marek.dvorak@cleansia.test";
    private const string FailedRequestId = "request-failed-1";

    private readonly SqliteConnection _connection;
    private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();

    public ErasureAttemptMarkingTests()
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
    public async Task A_First_Deletion_Marks_The_Attempt_Under_The_Staged_Rows_Id_With_The_Actor()
    {
        await SeedAsync();
        var attempt = new ErasureAttempt();

        await using var ctx = NewContext();
        var result = await Service(ctx, attempt).DeleteUserAccountAsync(
            SubjectUserId, GdprAuditReasons.SelfDeletion, _ => (GdprAuditReasons.SelfActor, null), deferEmployeeErasure: true, CancellationToken.None);
        await ctx.CommitAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(attempt.Started);
        Assert.Equal(SubjectUserId, attempt.SubjectUserId);
        Assert.Equal(GdprAuditReasons.SelfActor, attempt.ProcessedBy);
        Assert.DoesNotContain("@", attempt.ProcessedBy);
        var request = Assert.Single(await ctx.GdprRequests.IgnoreQueryFilters().ToListAsync());
        Assert.Equal(attempt.RequestId, request.Id);
        Assert.Equal(GdprRequestStatus.Completed, request.Status);
        Assert.Equal(GdprAuditReasons.SelfActor, request.ProcessedBy);
    }

    [Fact]
    public async Task A_Refused_First_Deletion_Marks_Nothing()
    {
        await SeedAsync(blockingOrderStatus: OrderStatus.InProgress);
        var attempt = new ErasureAttempt();

        await using var ctx = NewContext();
        var result = await Service(ctx, attempt).DeleteUserAccountAsync(
            SubjectUserId, GdprAuditReasons.SelfDeletion, _ => (GdprAuditReasons.SelfActor, null), deferEmployeeErasure: true, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.GdprDeletionBlockedByOrder, result.Error!.Message);
        Assert.False(attempt.Started);
    }

    [Fact]
    public async Task A_Retry_Completes_The_Failed_Row_Itself_And_Stamps_The_Retry_Reason_On_The_Subject()
    {
        await SeedAsync(failedRequest: true);
        var attempt = new ErasureAttempt();

        await using var ctx = NewContext();
        var result = await Service(ctx, attempt).RetryDeletionAsync(
            FailedRequestId, _ => ("admin@cleansia.test", "Retried by admin@cleansia.test"), CancellationToken.None);
        await ctx.CommitAsync(CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal((SubjectUserId, FailedRequestId, "admin@cleansia.test"), (attempt.SubjectUserId, attempt.RequestId, attempt.ProcessedBy));

        await using var verify = NewContext();
        var request = Assert.Single(await verify.GdprRequests.IgnoreQueryFilters().ToListAsync());
        Assert.Equal(FailedRequestId, request.Id);
        Assert.Equal(GdprRequestStatus.Completed, request.Status);
        Assert.Equal("admin@cleansia.test", request.ProcessedBy);
        Assert.Equal("DbUpdateException: boom\nRetried by admin@cleansia.test", request.Notes);

        var user = await verify.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == SubjectUserId);
        Assert.NotEqual(SubjectEmail, user.Email);
        Assert.False(user.IsActive);
        Assert.Equal(GdprAuditReasons.RetriedDeletion, user.DeactivatedBy);
        Assert.Equal("GDPR_DELETION_RETRY", user.DeactivatedBy);
    }

    [Fact]
    public async Task A_Refused_Retry_Is_Still_Marked_So_The_Refusal_Lands_On_The_Row()
    {
        await SeedAsync(blockingOrderStatus: OrderStatus.OnTheWay, failedRequest: true);
        var attempt = new ErasureAttempt();

        await using var ctx = NewContext();
        var result = await Service(ctx, attempt).RetryDeletionAsync(
            FailedRequestId, _ => ("system", "Retried by system"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.GdprDeletionBlockedByOrder, result.Error!.Message);
        Assert.True(attempt.Started);
        Assert.Equal(FailedRequestId, attempt.RequestId);
        Assert.Equal("system", attempt.ProcessedBy);
    }

    [Fact]
    public async Task A_Retry_Of_An_Unknown_Request_Is_Not_Found_And_Marks_Nothing()
    {
        await SeedAsync();
        var attempt = new ErasureAttempt();

        await using var ctx = NewContext();
        var result = await Service(ctx, attempt).RetryDeletionAsync(
            "request-nope", _ => ("system", null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.GdprRequestNotFound, result.Error!.Message);
        Assert.False(attempt.Started);
    }

    private GdprDeletionService Service(CleansiaDbContext ctx, IErasureAttempt attempt)
    {
        var session = new TestUserSessionProvider(SubjectUserId, SubjectEmail);
        return new GdprDeletionService(
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
            new AddressRepository(ctx),
            new GuestOrderAccessTokenIssuer(new GuestOrderAccessTokenRepository(ctx)),
            Mock.Of<IRefreshTokenService>(),
            Mock.Of<IStripeClient>(),
            _blobClientFactory.Object,
            Mock.Of<IAppConfigurationProvider>(),
            attempt,
            new ArchiveWriteGate(),
            NullLogger<GdprDeletionService>.Instance);
    }

    private async Task SeedAsync(OrderStatus? blockingOrderStatus = null, bool failedRequest = false)
    {
        await using (var schema = NewContext())
        {
            await schema.Database.EnsureCreatedAsync();
        }

        await using var ctx = NewContext();

        var user = User.CreateWithPassword(SubjectEmail, "Test-password-1!", "Marek", "Dvorak", UserProfile.Customer);
        user.Id = SubjectUserId;
        ctx.Add(user);

        if (blockingOrderStatus is { } status)
        {
            var order = Order.Create(
                customerName: "Marek Dvorak",
                customerEmail: SubjectEmail,
                customerPhone: "+420777222333",
                customerAddress: Address.Create("Attempt St 1", "Praha", "11000", "cz"),
                rooms: 2,
                bathrooms: 1,
                cleaningDateTime: DateTime.UtcNow.AddHours(6),
                paymentType: PaymentType.Cash,
                totalPrice: 1500m,
                currencyId: "czk",
                paymentStatus: PaymentStatus.Pending,
                userId: SubjectUserId);
            order.Id = "order-attempt-1";
            order.AddOrderStatus(OrderStatusTrack.Create(status, order));
            ctx.Add(order);
        }

        if (failedRequest)
        {
            var request = GdprRequest.Create(SubjectUserId, GdprRequest.DeletionRequestType);
            request.Id = FailedRequestId;
            request.MarkFailed(GdprAuditReasons.SelfActor, "DbUpdateException: boom");
            ctx.Add(request);
        }

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
