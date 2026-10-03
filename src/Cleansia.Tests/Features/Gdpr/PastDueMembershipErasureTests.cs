using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Gdpr;

/// <summary>
/// Erasure used to cancel only a paid-up Plus subscription. A past-due one is still alive in Stripe, so
/// an erased customer's card went on being retried. It is cancelled now, with its open invoice voided;
/// a paid-up one still runs to the end of the period it was paid for.
///
/// <para>Real repositories over in-memory SQLite, mirroring <c>LiveActivityTokenErasureTests</c>; only
/// the external edges (blobs, Stripe) are doubles.</para>
/// </summary>
public sealed class PastDueMembershipErasureTests : IDisposable
{
    private const string UserId = "user-erase-plus-1";
    private const string SubscriptionId = "sub_erase_plus_1";

    private readonly SqliteConnection _connection;
    private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();
    private readonly Mock<IStripeClient> _stripe = new();

    public PastDueMembershipErasureTests()
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
    public async Task Erasure_Cancels_A_Past_Due_Subscription_Now()
    {
        await SeedAsync("past_due");

        await EraseAsync();

        _stripe.Verify(c => c.CancelSubscriptionNowAsync(SubscriptionId, It.IsAny<CancellationToken>()), Times.Once);
        _stripe.Verify(c => c.CancelSubscriptionAtPeriodEndAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(MembershipStatus.Cancelled, await StatusAsync());
    }

    [Fact]
    public async Task Erasure_Still_Runs_A_Paid_Up_Subscription_To_The_End_Of_Its_Period()
    {
        await SeedAsync("active");

        await EraseAsync();

        _stripe.Verify(c => c.CancelSubscriptionAtPeriodEndAsync(SubscriptionId, It.IsAny<CancellationToken>()), Times.Once);
        _stripe.Verify(c => c.CancelSubscriptionNowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(MembershipStatus.Active, await StatusAsync());
    }

    private async Task EraseAsync()
    {
        await using var ctx = NewContext();
        var session = new TestUserSessionProvider(UserId, $"{UserId}@cleansia.test");
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
            new SavedCardRepository(ctx),
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
            new CleanerLegalDocumentAcceptanceRepository(ctx),
            new AddressRepository(ctx),
            new GuestOrderAccessTokenIssuer(new GuestOrderAccessTokenRepository(ctx)),
            Mock.Of<IRefreshTokenService>(),
            _stripe.Object,
            _blobClientFactory.Object,
            Mock.Of<IAppConfigurationProvider>(),
            new ErasureAttempt(),
            new ArchiveWriteGate(),
            NullLogger<GdprDeletionService>.Instance);

        var result = await service.DeleteUserAccountAsync(
            UserId, "gdpr_erasure_test", _ => ("test-actor", null), deferEmployeeErasure: false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task<MembershipStatus> StatusAsync()
    {
        await using var ctx = NewContext();
        return (await ctx.Set<UserMembership>().IgnoreQueryFilters().SingleAsync()).Status;
    }

    private async Task SeedAsync(string stripeStatus)
    {
        await using (var schema = NewContext())
        {
            await schema.Database.EnsureCreatedAsync();
        }

        await using var ctx = NewContext();

        var user = User.CreateWithPassword("vera.plus@cleansia.test", "Test-password-1!", "Vera", "Plus", UserProfile.Customer);
        user.Id = UserId;
        ctx.Add(user);

        var currency = Currency.Create("CZK", "Kč", "Czech Koruna");
        currency.Id = "currency-czk";
        ctx.Add(currency);

        var plan = MembershipPlan.Create(
            code: "PLUS_MONTHLY",
            name: "Plus Monthly",
            discountPercentage: 5m,
            allowsExpressUpgrade: true);
        ctx.Add(plan);

        var membership = UserMembership.Create(
            userId: UserId,
            membershipPlanId: plan.Id,
            currencyId: currency.Id,
            stripeSubscriptionId: SubscriptionId,
            currentPeriodStart: DateTime.UtcNow.AddDays(-3),
            currentPeriodEnd: DateTime.UtcNow.AddDays(27));
        membership.UpdateFromStripeWebhook(stripeStatus, membership.CurrentPeriodStart, membership.CurrentPeriodEnd, trialEndsAtUtc: null);
        ctx.Add(membership);

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
