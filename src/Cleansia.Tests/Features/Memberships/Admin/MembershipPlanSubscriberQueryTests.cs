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

namespace Cleansia.Tests.Features.Memberships.Admin;

/// <summary>
/// The question behind the plan-benefits lock, asked of the real repository over a real context so the
/// tenant filter runs. A plan is platform catalogue: a member of another company holds its terms just as
/// much, and a cancelled member subscribed on them all the same.
/// </summary>
public sealed class MembershipPlanSubscriberQueryTests : IDisposable
{
    private const string UserId = "user-plan-lock-1";

    private readonly SqliteConnection _connection;

    public MembershipPlanSubscriberQueryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task ACancelledMemberOfAnotherCompany_StillCountsAsASubscriber()
    {
        var (subscribed, untouched) = await SeedAsync();

        await using var ctx = NewContext(TestTenants.Default);
        var repository = new UserMembershipRepository(ctx);

        Assert.True(await repository.HasAnyForPlanAsync(subscribed, CancellationToken.None));
        Assert.False(await repository.HasAnyForPlanAsync(untouched, CancellationToken.None));
    }

    private async Task<(string Subscribed, string Untouched)> SeedAsync()
    {
        await using var ctx = NewContext(TestTenants.Second);
        await TestTenants.EnsureCreatedWithRegistryAsync(ctx);

        var subscribed = MembershipPlan.Create("PLUS_MONTHLY", "Plus Monthly", 5m, true, expressUpgradesPerMonth: 2);
        var untouched = MembershipPlan.Create("PLUS_YEARLY", "Plus Yearly", 5m, true, BillingInterval.Yearly);
        ctx.AddRange(subscribed, untouched);

        ctx.Add(Language.Create("en", "English"));

        var currency = MembershipPricingMockFactory.Czk();
        ctx.Add(currency);

        var user = User.CreateWithPassword("plan.lock@cleansia.test", "Password1!", "Plan", "Lock", UserProfile.Customer);
        user.Id = UserId;
        ctx.Add(user);

        var membership = UserMembership.Create(
            UserId, subscribed.Id, currency.Id, "sub_plan_lock", DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-10));
        membership.UpdateFromStripeWebhook(
            "canceled", membership.CurrentPeriodStart, membership.CurrentPeriodEnd, trialEndsAtUtc: null);
        ctx.Add(membership);

        await ctx.CommitAsync(CancellationToken.None);

        return (subscribed.Id, untouched.Id);
    }

    private CleansiaDbContext NewContext(string tenantId) =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId));

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
