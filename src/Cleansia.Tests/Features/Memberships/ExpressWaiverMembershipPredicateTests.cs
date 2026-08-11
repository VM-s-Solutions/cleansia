using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// <c>TC-BENEFIT-GATE-0</c>'s <c>PastDue</c> leg, and the reason it is here rather than beside the other
/// gate cases: <b>the PastDue answer is a property of the shared membership predicate, not of the
/// resolver.</b> Asserting it against a mocked repository would prove only that the mock returned null.
///
/// <para>So this runs the REAL <c>UserMembershipRepository.ActiveForUserQuery</c> against a real
/// <see cref="CleansiaDbContext"/> (SQLite in-memory, so <c>OnModelCreating</c> and the tenant filter
/// actually run) and asserts the resolver waives nothing for a member mid-dunning.</para>
///
/// <para>Owner ruling 2026-08-03: <i>"PastDue keeps NO benefits. Cut everything on first payment
/// failure."</i> No grace window. <c>MembershipStatus</c>' doc comment used to promise one; the
/// predicate never implemented it, and the predicate is what shipped.</para>
/// </summary>
public sealed class ExpressWaiverMembershipPredicateTests : IDisposable
{
    private const string UserId = "user-pastdue-1";
    private const string PeriodKey = "C:2026-08";

    private readonly SqliteConnection _connection;

    public ExpressWaiverMembershipPredicateTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(null));
    }

    private async Task SeedMembershipAsync(MembershipStatus status)
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();

        var plan = MembershipPlan.Create(
            code: "PLUS_MONTHLY",
            name: "Plus Monthly",
            monthlyPriceCzk: 199m,
            stripePriceId: "price_plus_monthly",
            discountPercentage: 5m,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true,
            expressUpgradesPerMonth: 2);
        ctx.Add(plan);

        ctx.Add(Language.Create("en", "English"));

        var user = User.CreateWithPassword(
            "pastdue@cleansia.test", "Password1!", "Past", "Due", UserProfile.Customer);
        user.Id = UserId;
        ctx.Add(user);

        var membership = UserMembership.Create(
            UserId, plan.Id, "sub_pastdue", DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(20));
        if (status != MembershipStatus.Active)
        {
            // Through the REAL writer, so the test pins production wiring rather than a field poke:
            // "past_due" is what Stripe sends when the first invoice fails.
            membership.UpdateFromStripeWebhook(
                "past_due", DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(20), trialEndsAtUtc: null);
        }
        ctx.Add(membership);

        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task<ExpressWaiver> ResolveAsync()
    {
        await using var ctx = NewContext();
        var periodKeys = new Mock<IBenefitPeriodKeyFactory>();
        periodKeys
            .Setup(f => f.GetCalendarKeyAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PeriodKey);

        var resolver = new ExpressWaiverResolver(
            new UserMembershipRepository(ctx),
            new MembershipBenefitUsageRepository(
                ctx, new FixedTenantProvider(null), new TestUserSessionProvider("system", "s@x.test")),
            periodKeys.Object);

        var nowUtc = DateTime.UtcNow;
        return await resolver.ResolveForUserAsync(
            UserId, nowUtc.AddHours(3), nowUtc, CancellationToken.None);
    }

    [Fact]
    public async Task PastDueMember_GetsNoExpressWaiver()
    {
        await SeedMembershipAsync(MembershipStatus.PastDue);

        var waiver = await ResolveAsync();

        Assert.False(waiver.Waived);
        Assert.Equal(0, waiver.Quota);
        Assert.Equal(0, waiver.RemainingBeforeThisBooking);
    }

    /// <summary>The control: the same seed with a paid status DOES waive, so the assertion above is
    /// about <c>PastDue</c> and not about the harness failing to find anything.</summary>
    [Fact]
    public async Task ActiveMember_GetsTheExpressWaiver()
    {
        await SeedMembershipAsync(MembershipStatus.Active);

        var waiver = await ResolveAsync();

        Assert.True(waiver.Waived);
        Assert.Equal(2, waiver.Quota);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
