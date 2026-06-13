using Cleansia.Core.AppServices.Features.Payments;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.Payments.Webhooks;

/// <summary>
/// Active-membership idempotency and signature-rejection for the <b>subscription</b> webhook, driven
/// END-TO-END through the real host (<c>IMediator.Send(HandlePaymentNotification.Command)</c> → the
/// subscription branch → <c>StripeSubscriptionWebhookHandler.ProvisionFromCreatedEventAsync</c>) over a
/// REAL Postgres (Testcontainers). This is the integration depth above the mocked-repo unit suite: the
/// active-membership pre-check and the FILTERED unique index on
/// <c>(TenantId, UserId) WHERE Status = Active</c> are asserted by the REAL DB, by counting persisted
/// <see cref="UserMembership"/> rows after a real commit, not by a mock's Add count.
///
/// The webhook is anonymous: provisioning reads the owning user tenant-ignoring and sets the tenant
/// override before <c>GetActiveForUserAsync</c> + the insert, so the active-check and the filtered index
/// both resolve in the user's tenant scope (S8).
/// </summary>
[Collection("PostgresCollection")]
public class SubscriptionWebhookIntegrationTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string TenantId = "tenant-sub-webhook";
    private const string PlanCode = "PLUS_MONTHLY";

    private static string _userId = default!;
    private static string _planId = default!;

    // ── AC2 — a clean user's subscription.created creates EXACTLY ONE active membership (happy path) ──

    [Fact]
    public async Task CleanUser_SubscriptionCreated_CreatesExactlyOneActiveMembership()
    {
        await TestMethod(
            arrange: ctx => SeedUserAndPlan(ctx, seedActiveMembership: false),
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(SignedCreatedCommand("evt_sub_clean", "sub_clean"));
            },
            assert: async (CleansiaDbContext context, BusinessResult<string> result) =>
            {
                Assert.True(result.IsSuccess);
                var memberships = await MembershipsForUserAsync(context);
                var membership = Assert.Single(memberships);
                Assert.Equal(MembershipStatus.Active, membership.Status);
                Assert.Equal("sub_clean", membership.StripeSubscriptionId);
                Assert.Equal(TenantId, membership.TenantId);
            });
    }

    // ── AC2 — an already-active user does NOT get a 2nd active row (the active-membership pre-check) ──

    [Fact]
    public async Task AlreadyActiveUser_SubscriptionCreated_DoesNotCreateSecondActiveMembership()
    {
        await TestMethod(
            arrange: ctx => SeedUserAndPlan(ctx, seedActiveMembership: true),
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                // A fresh subscription id reaching us for a user who already has an active membership
                // (stale tab / Dashboard / re-checkout) — the active-check must reconcile to a no-op.
                return await mediator.Send(SignedCreatedCommand("evt_sub_dup", "sub_brand_new"));
            },
            assert: async (CleansiaDbContext context, BusinessResult<string> result) =>
            {
                Assert.True(result.IsSuccess);
                var active = await context.Set<UserMembership>()
                    .IgnoreQueryFilters()
                    .CountAsync(m => m.UserId == _userId && m.Status == MembershipStatus.Active);
                // Still exactly one active membership for (TenantId, UserId) — no second row inserted.
                Assert.Equal(1, active);
            });
    }

    // ── AC7 — the same valid first-delivery event is processed exactly once (no false lock) ──

    [Fact]
    public async Task ValidSubscriptionCreated_FirstDelivery_IsProcessedExactlyOnce()
    {
        await TestMethod(
            arrange: ctx => SeedUserAndPlan(ctx, seedActiveMembership: false),
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(SignedCreatedCommand("evt_sub_happy", "sub_happy"));
            },
            assert: async (CleansiaDbContext context, BusinessResult<string> result) =>
            {
                Assert.True(result.IsSuccess);
                Assert.Single(await MembershipsForUserAsync(context));
            });
    }

    // ── AC4/AC5 — a missing and a forged signature are rejected: no membership row is created ──

    [Fact]
    public async Task MissingSignature_IsRejected_NoMembershipCreated()
    {
        await TestMethod(
            arrange: ctx => SeedUserAndPlan(ctx, seedActiveMembership: false),
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var body = Cleansia.IntegrationTests.Features.Payments.Webhooks.StripeWebhookTestPayloads
                    .SubscriptionCreatedBody("evt_sub_nosig", "sub_nosig", _userId, PlanCode);
                return await mediator.Send(new HandlePaymentNotification.Command(body, string.Empty));
            },
            assert: async (CleansiaDbContext context, BusinessResult<string> result) =>
            {
                Assert.True(result.IsFailure);
                Assert.Empty(await MembershipsForUserAsync(context));
            });
    }

    [Fact]
    public async Task ForgedSignature_WrongSecret_IsRejected_NoMembershipCreated()
    {
        await TestMethod(
            arrange: ctx => SeedUserAndPlan(ctx, seedActiveMembership: false),
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var body = StripeWebhookTestPayloads
                    .SubscriptionCreatedBody("evt_sub_forged", "sub_forged", _userId, PlanCode);
                var forged = StripeWebhookTestPayloads.Sign(body, StripeWebhookTestPayloads.WrongWebhookSecret);
                return await mediator.Send(new HandlePaymentNotification.Command(body, forged));
            },
            assert: async (CleansiaDbContext context, BusinessResult<string> result) =>
            {
                Assert.True(result.IsFailure);
                Assert.Empty(await MembershipsForUserAsync(context));
            });
    }

    // ── AC3 — filtered-unique-index backstop holds end-to-end: two near-simultaneous deliveries for a
    //    clean user land EXACTLY ONE Active row; the loser hits PG 23505 and is reconciled (not a 2nd row). ──

    [Fact]
    public async Task TwoConcurrentSubscriptionCreated_CleanUser_LandsExactlyOneActiveMembership()
    {
        await TestMethod(
            arrange: ctx => SeedUserAndPlan(ctx, seedActiveMembership: false),
            act: async provider =>
            {
                // Two independent request scopes (own DbContext each), committed concurrently — the active
                // check alone cannot close the TOCTOU window, so the filtered unique index is the backstop.
                var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

                async Task<BusinessResult<string>> Deliver(string eventId, string subscriptionId)
                {
                    using var scope = scopeFactory.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    return await mediator.Send(SignedCreatedCommand(eventId, subscriptionId));
                }

                var first = Deliver("evt_sub_race_a", "sub_race_a");
                var second = Deliver("evt_sub_race_b", "sub_race_b");
                return await Task.WhenAll(first, second);
            },
            assert: async (CleansiaDbContext context, BusinessResult<string>[] results) =>
            {
                // Neither delivery 500s — the loser's 23505 is caught and resolved to the winner (S7b),
                // so Stripe is not handed a retry-inducing failure.
                Assert.All(results, r => Assert.True(r.IsSuccess));

                var active = await context.Set<UserMembership>()
                    .IgnoreQueryFilters()
                    .CountAsync(m => m.UserId == _userId && m.Status == MembershipStatus.Active);
                Assert.Equal(1, active);
            },
            transactional: false);
    }

    // ── AC3 — the index is FILTERED: a Cancelled row + a new Active row for the same user is PERMITTED ──

    [Fact]
    public async Task CancelledMembership_PlusNewSubscriptionCreated_PermitsANewActiveRow()
    {
        await TestMethod(
            arrange: async ctx =>
            {
                await SeedUserAndPlan(ctx, seedActiveMembership: false);
                var cancelled = UserMembership.Create(
                    userId: _userId,
                    membershipPlanId: _planId,
                    stripeSubscriptionId: "sub_cancelled",
                    currentPeriodStart: DateTime.UtcNow.AddMonths(-2),
                    currentPeriodEnd: DateTime.UtcNow.AddMonths(-1));
                // Drop the row OUT of the filtered (Status = Active) index predicate.
                cancelled.UpdateFromStripeWebhook("canceled", cancelled.CurrentPeriodStart, cancelled.CurrentPeriodEnd);
                cancelled.TenantId = TenantId;
                ctx.Add(cancelled);
                await ctx.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(SignedCreatedCommand("evt_sub_resub", "sub_resub"));
            },
            assert: async (CleansiaDbContext context, BusinessResult<string> result) =>
            {
                Assert.True(result.IsSuccess);
                var rows = await MembershipsForUserAsync(context);
                // The re-subscribe-after-cancel case: cancelled row + new active row both persist.
                Assert.Equal(2, rows.Count);
                Assert.Single(rows, m => m.Status == MembershipStatus.Active);
            });
    }

    private static HandlePaymentNotification.Command SignedCreatedCommand(string eventId, string subscriptionId)
    {
        var body = StripeWebhookTestPayloads.SubscriptionCreatedBody(eventId, subscriptionId, _userId, PlanCode);
        var signature = StripeWebhookTestPayloads.Sign(body, StripeWebhookTestPayloads.ConfiguredWebhookSecret);
        return new HandlePaymentNotification.Command(body, signature);
    }

    private static Task<List<UserMembership>> MembershipsForUserAsync(CleansiaDbContext context) =>
        context.Set<UserMembership>()
            .IgnoreQueryFilters()
            .Where(m => m.UserId == _userId)
            .ToListAsync();

    private static async Task SeedUserAndPlan(CleansiaDbContext context, bool seedActiveMembership)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var user = User.CreateWithPassword("sub-webhook@cleansia.test", "12345678Test!", "Sub", "Scriber");
        user.ConfirmEmail();
        user.TenantId = TenantId;
        user.Created(Cleansia.TestUtilities.Constants.TestUserSession.TestUserId, DateTime.UtcNow);
        context.Users.Add(user);

        var plan = MembershipPlan.Create(
            code: PlanCode,
            name: "Plus Monthly",
            monthlyPriceCzk: 199m,
            stripePriceId: "price_test_plus",
            discountPercentage: 5m,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true,
            billingInterval: BillingInterval.Monthly,
            trialPeriodDays: 0);
        plan.TenantId = TenantId;
        context.MembershipPlans.Add(plan);

        await context.CommitAsync(CancellationToken.None);

        _userId = user.Id;
        _planId = plan.Id;

        if (seedActiveMembership)
        {
            var existing = UserMembership.Create(
                userId: _userId,
                membershipPlanId: _planId,
                stripeSubscriptionId: "sub_existing_active",
                currentPeriodStart: DateTime.UtcNow.AddDays(-5),
                currentPeriodEnd: DateTime.UtcNow.AddMonths(1));
            existing.TenantId = TenantId;
            context.Add(existing);
            await context.CommitAsync(CancellationToken.None);
        }
    }
}
