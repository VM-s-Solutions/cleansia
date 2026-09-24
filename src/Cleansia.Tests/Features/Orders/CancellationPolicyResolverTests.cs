using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Cleansia.TestUtilities.MockDataFactories.Memberships;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Owner ruling 2026-09-24: the oops window after booking is 15 minutes for every customer and 60 for
/// an entitled, paid Plus member — and it is not the plan's free-cancellation HOURS, which is a
/// separate benefit that keeps working as before.
///
/// <para>Runs the REAL <c>UserMembershipRepository</c> against SQLite: whether a PastDue, paused,
/// expired or trialing enrolment is entitled is a property of the shared predicate, and a mocked
/// repository returning null would prove only that the mock returned null.</para>
/// </summary>
public sealed class CancellationPolicyResolverTests : IDisposable
{
    private const string UserId = "user-oops-1";

    private readonly SqliteConnection _connection;

    public CancellationPolicyResolverTests()
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
            new FixedTenantProvider(TestTenants.Default));
    }

    private async Task SeedAsync(
        int planFreeCancellationHours = 4,
        string? stripeStatus = null,
        DateTime? periodEnd = null,
        DateTime? trialEndsAtUtc = null)
    {
        await using var ctx = NewContext();
        await TestTenants.EnsureCreatedWithRegistryAsync(ctx);

        var plan = MembershipPlan.Create(
            code: "PLUS_MONTHLY",
            name: "Plus Monthly",
            discountPercentage: 5m,
            freeCancellationWindowHours: planFreeCancellationHours,
            allowsExpressUpgrade: true);
        ctx.Add(plan);
        ctx.Add(Language.Create("en", "English"));
        var currency = MembershipPricingMockFactory.Czk();
        ctx.Add(currency);
        var user = User.CreateWithPassword("oops@cleansia.test", "Password1!", "Oops", "Window", UserProfile.Customer);
        user.Id = UserId;
        ctx.Add(user);

        var now = DateTime.UtcNow;
        var membership = UserMembership.Create(
            UserId, plan.Id, currency.Id, "sub_oops", now.AddDays(-10), periodEnd ?? now.AddDays(20), trialEndsAtUtc);
        if (stripeStatus is not null)
        {
            membership.UpdateFromStripeWebhook(stripeStatus, now.AddDays(-10), periodEnd ?? now.AddDays(20), trialEndsAtUtc);
        }
        ctx.Add(membership);

        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task<CancellationPolicy> ResolveAsync(string? userId = UserId)
    {
        await using var ctx = NewContext();
        return await new CancellationPolicyResolver(new UserMembershipRepository(ctx))
            .ResolveForUserAsync(userId, CancellationToken.None);
    }

    private static void AssertStandard(CancellationPolicy policy)
    {
        Assert.Equal(BookingPolicy.OopsWindowMinutesStandard, policy.OopsWindowMinutes);
        Assert.Equal(BookingPolicy.FreeCancellationHours, policy.FreeCancellationHours);
        Assert.Equal(BookingPolicy.PartialCancellationHours, policy.PartialCancellationHours);
        Assert.Equal(BookingPolicy.PartialCancellationFeeRate, policy.PartialCancellationFeeRate);
        Assert.Equal(BookingPolicy.LastMinuteCancellationFeeRate, policy.LastMinuteCancellationFeeRate);
    }

    [Fact]
    public async Task A_Guest_Gets_The_Standard_Policy()
    {
        await SeedAsync();

        AssertStandard(await ResolveAsync(userId: null));
    }

    [Fact]
    public async Task An_Account_Without_A_Membership_Gets_The_Standard_Policy()
    {
        await SeedAsync();

        AssertStandard(await ResolveAsync(userId: "user-without-plus"));
    }

    [Theory]
    [InlineData(0, BookingPolicy.FreeCancellationHours)]
    [InlineData(4, 4)]
    [InlineData(24, 24)]
    public async Task An_Entitled_Member_Gets_Sixty_Minutes_Whatever_The_Plans_Free_Hours(
        int planFreeCancellationHours, int expectedFreeHours)
    {
        await SeedAsync(planFreeCancellationHours);

        var policy = await ResolveAsync();

        Assert.Equal(BookingPolicy.OopsWindowMinutesPlus, policy.OopsWindowMinutes);
        Assert.Equal(expectedFreeHours, policy.FreeCancellationHours);
        Assert.Equal(BookingPolicy.PartialCancellationHours, policy.PartialCancellationHours);
        Assert.Equal(BookingPolicy.PartialCancellationFeeRate, policy.PartialCancellationFeeRate);
        Assert.Equal(BookingPolicy.LastMinuteCancellationFeeRate, policy.LastMinuteCancellationFeeRate);
    }

    [Theory]
    [InlineData("past_due")]
    [InlineData("paused")]
    [InlineData("canceled")]
    public async Task A_Membership_That_Is_Not_Active_Gets_The_Standard_Policy(string stripeStatus)
    {
        await SeedAsync(stripeStatus: stripeStatus);

        AssertStandard(await ResolveAsync());
    }

    [Fact]
    public async Task An_Expired_Membership_Gets_The_Standard_Policy()
    {
        await SeedAsync(periodEnd: DateTime.UtcNow.AddMinutes(-1));

        AssertStandard(await ResolveAsync());
    }

    [Fact]
    public async Task A_Trialing_Membership_Gets_The_Standard_Policy()
    {
        await SeedAsync(stripeStatus: "trialing", trialEndsAtUtc: DateTime.UtcNow.AddDays(5));

        AssertStandard(await ResolveAsync());
    }

    [Fact]
    public async Task A_Member_Whose_Trial_Has_Ended_Gets_Sixty_Minutes()
    {
        await SeedAsync(trialEndsAtUtc: DateTime.UtcNow.AddDays(-1));

        var policy = await ResolveAsync();

        Assert.Equal(BookingPolicy.OopsWindowMinutesPlus, policy.OopsWindowMinutes);
        Assert.Equal(4, policy.FreeCancellationHours);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
