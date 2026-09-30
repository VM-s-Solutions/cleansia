using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Cleansia.IntegrationTests.Features.Memberships;

/// <summary>
/// A past-due Plus subscription over real Postgres: still a live enrolment — the one a customer cancels,
/// the one that refuses a second subscription and the one erasure cancels — while granting no benefit,
/// and the partial unique index the migration emits refuses a second live row beside it.
/// </summary>
[Collection("PostgresCollection")]
public class PastDueMembershipLifecycleTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CurrencyId = "currency-czk-pastdue";
    private const string PlanId = "plan-monthly-pastdue";

    [Fact]
    public async Task A_Past_Due_Membership_Is_Live_But_Entitles_Nobody()
    {
        await TestMethod(
            arrange: ctx => SeedAsync(ctx, "past_due"),
            act: async provider =>
            {
                var userId = await provider.GetRequiredService<CleansiaDbContext>().Users.Select(u => u.Id).SingleAsync();
                var memberships = provider.GetRequiredService<IUserMembershipRepository>();
                return (
                    Live: await memberships.GetLifecycleForUserNoTrackingAsync(userId, CancellationToken.None),
                    Entitled: await memberships.GetEntitledForUserNoTrackingAsync(userId, CancellationToken.None));
            },
            assert: (CleansiaDbContext _, (UserMembership? Live, UserMembership? Entitled) r) =>
            {
                Assert.Equal(MembershipStatus.PastDue, Assert.IsType<UserMembership>(r.Live).Status);
                Assert.Null(r.Entitled);
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task A_Cancelled_Membership_Is_Not_Live()
    {
        await TestMethod(
            arrange: ctx => SeedAsync(ctx, "canceled"),
            act: async provider =>
            {
                var userId = await provider.GetRequiredService<CleansiaDbContext>().Users.Select(u => u.Id).SingleAsync();
                return await provider.GetRequiredService<IUserMembershipRepository>()
                    .GetLifecycleForUserNoTrackingAsync(userId, CancellationToken.None);
            },
            assert: (CleansiaDbContext _, UserMembership? live) =>
            {
                Assert.Null(live);
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task The_Schema_Refuses_A_Second_Live_Subscription_Beside_A_Past_Due_One()
    {
        await TestMethod(
            arrange: ctx => SeedAsync(ctx, "past_due"),
            act: async provider =>
            {
                var ctx = provider.GetRequiredService<CleansiaDbContext>();
                var userId = await ctx.Users.Select(u => u.Id).SingleAsync();

                ctx.UserMemberships.Add(UserMembership.Create(
                    userId, PlanId, CurrencyId, "sub_second_pastdue", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1)));
                var refused = await Assert.ThrowsAsync<DbUpdateException>(() => ctx.CommitAsync(CancellationToken.None));
                ctx.ChangeTracker.Clear();

                return (refused.InnerException as PostgresException)?.ConstraintName;
            },
            assert: (CleansiaDbContext _, string? constraint) =>
            {
                Assert.Equal("IX_UserMemberships_TenantId_UserId", constraint);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    private static async Task SeedAsync(CleansiaDbContext ctx, string stripeStatus)
    {
        ctx.Languages.Add(Language.Create("en", "English"));

        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.IsActive = true;
        czk.Id = CurrencyId;
        czk.SetAsDefault(true);
        ctx.Currencies.Add(czk);

        var plan = MembershipPlan.Create("PLUS_MONTHLY", "Cleansia Plus (Monthly)", 5m, 4, true);
        plan.Id = PlanId;
        ctx.MembershipPlans.Add(plan);

        var user = User.CreateWithPassword("pastdue@cleansia.test", "12345678Test!", "Past", "Due");
        user.ConfirmEmail();
        ctx.Users.Add(user);

        var membership = UserMembership.Create(
            user.Id, PlanId, CurrencyId, "sub_first_pastdue", DateTime.UtcNow.AddDays(-3), DateTime.UtcNow.AddDays(27));
        membership.UpdateFromStripeWebhook(stripeStatus, membership.CurrentPeriodStart, membership.CurrentPeriodEnd, trialEndsAtUtc: null);
        ctx.UserMemberships.Add(membership);

        await ctx.CommitAsync(CancellationToken.None);
    }
}
