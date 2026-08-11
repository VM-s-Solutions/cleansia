using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Service = Cleansia.Core.Domain.Services.Service;

namespace Cleansia.IntegrationTests.Features.Memberships;

/// <summary>
/// The owner's three express-quota rulings (<c>Q-PLUS-02</c>, 2026-08-07), pinned against real
/// PostgreSQL through the real resolver, the real consumer and the real reservation statement: one
/// upgrade per month, no rollover, no reset on plan switch.
///
/// <para>Integration rather than unit for all three. The quota is arbitrated by an atomic
/// <c>INSERT … SELECT generate_series … ON CONFLICT DO NOTHING RETURNING</c> over a filtered
/// <c>NULLS NOT DISTINCT</c> partial index; a mocked repository asserts what the mock was told to
/// return, and the counting key is a property of that statement's <c>WHERE</c> clause and of nothing
/// in C#.</para>
///
/// <para>No <c>CompanyInfo</c> row is seeded on purpose, so <c>BenefitPeriodKeyFactory</c> falls back
/// to UTC and the period key is a pure function of the threaded clock rather than of the build
/// agent's zone.</para>
/// </summary>
[Collection("PostgresCollection")]
public class ExpressQuotaRulingTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CurrencyId = "currency-czk-expressquota";
    private const string CategoryId = "category-expressquota";
    private const string ServiceId = "service-expressquota";

    private const string UserId = "user-expressquota";
    private const string SpendthriftUserId = "user-expressquota-spent";
    private const string OneWaiverPlanId = "plan-expressquota-one";
    private const string TwoWaiverPlanId = "plan-expressquota-two";
    private const string FirstMembershipId = "membership-expressquota-1";
    private const string SecondMembershipId = "membership-expressquota-2";
    private const string SpendthriftMembershipId = "membership-expressq-spent";

    private const string August = "C:2026-08";
    private const string July = "C:2026-07";

    private const decimal ServiceBasePrice = 1000m;

    private static readonly DateTime AugustUtc = new(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime JulyUtc = new(2026, 7, 15, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// <b>Ruling (1) — one express upgrade per month.</b> Asserted in money, not in flags: the first
    /// booking of the month prices at the bare subtotal, and the second — same member, same calendar
    /// month, same express window — prices at subtotal + 20%.
    ///
    /// <para>The first leg is the positive control. "The surcharge was applied" is trivially true of a
    /// build where no waiver ever applies, of a plan whose quota is zero, and of a slot that was never
    /// in the express window; a waived 1000 immediately before a charged 1200 excludes all three.</para>
    /// </summary>
    [Fact]
    public async Task ASecondExpressBookingInTheSameCalendarMonthIsChargedNotWaived()
    {
        await TestMethod(
            arrange: context => SeedAsync(context, quota: 1),
            act: async provider =>
            {
                var resolver = provider.GetRequiredService<IExpressWaiverResolver>();
                var consumer = provider.GetRequiredService<IExpressWaiverConsumer>();
                var pricing = provider.GetRequiredService<IOrderPricingCalculator>();
                var cleaningUtc = AugustUtc.AddHours(3);

                var firstWaiver = await resolver.ResolveForUserAsync(
                    UserId, cleaningUtc, AugustUtc, CancellationToken.None);
                var firstPrice = await PriceAsync(pricing, cleaningUtc);

                var reservation = await consumer.TryReserveAsync(
                    firstWaiver, AugustUtc, CancellationToken.None);

                var secondWaiver = await resolver.ResolveForUserAsync(
                    UserId, cleaningUtc, AugustUtc, CancellationToken.None);
                var secondPrice = await PriceAsync(pricing, cleaningUtc);

                return new MonthlyCapOutcome(
                    firstWaiver, firstPrice, reservation?.SlotOrdinal, secondWaiver, secondPrice);
            },
            assert: async (CleansiaDbContext context, MonthlyCapOutcome outcome) =>
            {
                Assert.True(outcome.First.InExpressWindow);
                Assert.True(outcome.First.Waived);
                Assert.Equal(1, outcome.First.Quota);
                Assert.Equal(1, outcome.First.RemainingBeforeThisBooking);
                Assert.False(outcome.FirstPrice.ExpressSurchargeApplied);
                Assert.Equal(0m, outcome.FirstPrice.ExpressSurchargeAmount);
                Assert.Equal(ServiceBasePrice, outcome.FirstPrice.TotalPrice);

                Assert.Equal(0, outcome.ReservedOrdinal);

                Assert.True(outcome.Second.InExpressWindow);
                Assert.False(outcome.Second.Waived);
                Assert.Equal(0, outcome.Second.RemainingBeforeThisBooking);
                Assert.True(outcome.SecondPrice.ExpressSurchargeApplied);
                Assert.Equal(
                    ServiceBasePrice * BookingPolicy.ExpressSurchargeRate,
                    outcome.SecondPrice.ExpressSurchargeAmount);
                Assert.Equal(
                    ServiceBasePrice + ServiceBasePrice * BookingPolicy.ExpressSurchargeRate,
                    outcome.SecondPrice.TotalPrice);

                Assert.Equal(1, await LiveSlotsAsync(context, UserId, August));
            },
            transactional: false);
    }

    /// <summary>
    /// <b>Ruling (2) — no rollover, and the boundary restores exactly one.</b> Two members on identical
    /// plans with deliberately different Julys: one spent July's slot, one banked it. Both get exactly
    /// one in August.
    ///
    /// <para>The spent-July arm is the positive control. "August granted exactly one" asserted over a
    /// member with no July history is the same statement as ruling (1) and says nothing about
    /// accumulation; the July live-row counts are what make the comparison non-vacuous, and the two arms
    /// must agree.</para>
    /// </summary>
    [Fact]
    public async Task AnUnusedMonthDoesNotAccumulateAndTheBoundaryRestoresExactlyOne()
    {
        await TestMethod(
            arrange: context => SeedAsync(context, quota: 1, withSpendthrift: true),
            act: async provider =>
            {
                var repository = Repository(provider);

                var julySpend = await ReserveAsync(
                    repository, SpendthriftUserId, July, 1, JulyUtc, SpendthriftMembershipId);

                var bankedFirst = await ReserveAsync(repository, UserId, August, 1, AugustUtc);
                var bankedSecond = await ReserveAsync(repository, UserId, August, 1, AugustUtc);
                var spentFirst = await ReserveAsync(
                    repository, SpendthriftUserId, August, 1, AugustUtc, SpendthriftMembershipId);
                var spentSecond = await ReserveAsync(
                    repository, SpendthriftUserId, August, 1, AugustUtc, SpendthriftMembershipId);

                return new RolloverOutcome(
                    julySpend?.SlotOrdinal,
                    bankedFirst?.SlotOrdinal, bankedSecond?.SlotOrdinal,
                    spentFirst?.SlotOrdinal, spentSecond?.SlotOrdinal);
            },
            assert: async (CleansiaDbContext context, RolloverOutcome outcome) =>
            {
                Assert.Equal(0, outcome.JulySpend);
                Assert.Equal(0, await LiveSlotsAsync(context, UserId, July));
                Assert.Equal(1, await LiveSlotsAsync(context, SpendthriftUserId, July));

                Assert.Equal(0, outcome.BankedFirst);
                Assert.Null(outcome.BankedSecond);
                Assert.Equal(0, outcome.SpentFirst);
                Assert.Null(outcome.SpentSecond);

                Assert.Equal(1, await LiveSlotsAsync(context, UserId, August));
                Assert.Equal(1, await LiveSlotsAsync(context, SpendthriftUserId, August));
            },
            transactional: false);
    }

    /// <summary>
    /// <b>Ruling (3) — no reset on plan switch, READ side.</b> A cancel-and-resubscribe inside one
    /// calendar month is a brand-new <c>UserMembership</c> row; the balance must not move. Goes red the
    /// moment <c>CountLiveInPeriodAsync</c> grows a <c>UserMembershipId</c> term.
    ///
    /// <para><c>Quota == 1</c> is asserted next to <c>Remaining == 0</c> so the zero cannot be the
    /// trivial zero of a plan that grants nothing.</para>
    /// </summary>
    [Fact]
    public async Task ResubscribingMidMonthDoesNotRestoreTheBalance()
    {
        await TestMethod(
            arrange: context => SeedAsync(context, quota: 1),
            act: async provider =>
            {
                var resolver = provider.GetRequiredService<IExpressWaiverResolver>();
                var consumer = provider.GetRequiredService<IExpressWaiverConsumer>();
                var cleaningUtc = AugustUtc.AddHours(3);

                var before = await resolver.ResolveForUserAsync(
                    UserId, cleaningUtc, AugustUtc, CancellationToken.None);
                var reservation = await consumer.TryReserveAsync(before, AugustUtc, CancellationToken.None);

                await ResubscribeAsync(provider, OneWaiverPlanId);

                var after = await resolver.ResolveForUserAsync(
                    UserId, cleaningUtc, AugustUtc, CancellationToken.None);

                return new ResubscribeOutcome(before, reservation?.SlotOrdinal, after);
            },
            assert: async (CleansiaDbContext context, ResubscribeOutcome outcome) =>
            {
                Assert.True(outcome.Before.Waived);
                Assert.Equal(FirstMembershipId, outcome.Before.UserMembershipId);
                Assert.Equal(0, outcome.ReservedOrdinal);

                Assert.Equal(SecondMembershipId, outcome.After.UserMembershipId);
                Assert.True(outcome.After.InExpressWindow);
                Assert.Equal(1, outcome.After.Quota);
                Assert.Equal(0, outcome.After.RemainingBeforeThisBooking);
                Assert.False(outcome.After.Waived);

                var slot = await context.Set<MembershipBenefitUsage>()
                    .IgnoreQueryFilters()
                    .SingleAsync(u => u.UserId == UserId && u.PeriodKey == August);
                Assert.True(slot.IsActive);
                Assert.Equal(FirstMembershipId, slot.UserMembershipId);
            },
            transactional: false);
    }

    /// <summary>
    /// <b>Ruling (3) — no reset on plan switch, RESERVATION side.</b> The counting key inside
    /// <c>ReserveSlotSql</c> is <c>(TenantId, UserId, BenefitKind, PeriodKey)</c>; <c>UserMembershipId</c>
    /// is payload and appears in no predicate.
    ///
    /// <para>A two-waiver plan, not the owner's one, and that is load-bearing. At quota 1 the mutation
    /// this test exists to catch is invisible: a membership-scoped <c>NOT EXISTS</c> would offer ordinal
    /// 0, the filtered unique index would reject it, and <c>ON CONFLICT DO NOTHING</c> would return the
    /// same <c>null</c> as the correct statement. Only with capacity left does the scoping change an
    /// outcome — it re-offers the taken ordinal 0, loses the conflict, and denies a member the second
    /// slot they are owed. The index masks the over-grant; it cannot mask the denial.</para>
    /// </summary>
    [Fact]
    public async Task ASlotHeldUnderAPreviousEnrolmentStillBlocksItsOrdinal()
    {
        await TestMethod(
            arrange: context => SeedAsync(context, quota: 2),
            act: async provider =>
            {
                var repository = Repository(provider);
                var first = await ReserveAsync(repository, UserId, August, 2, AugustUtc);

                await ResubscribeAsync(provider, TwoWaiverPlanId);

                var second = await ReserveAsync(
                    repository, UserId, August, 2, AugustUtc, SecondMembershipId);
                var third = await ReserveAsync(
                    repository, UserId, August, 2, AugustUtc, SecondMembershipId);

                return new SwapOutcome(first?.SlotOrdinal, second?.SlotOrdinal, third?.SlotOrdinal);
            },
            assert: async (CleansiaDbContext context, SwapOutcome outcome) =>
            {
                Assert.Equal(0, outcome.First);
                Assert.Equal(1, outcome.Second);
                Assert.Null(outcome.Third);

                var slots = await context.Set<MembershipBenefitUsage>()
                    .IgnoreQueryFilters()
                    .Where(u => u.UserId == UserId && u.PeriodKey == August && u.IsActive)
                    .OrderBy(u => u.SlotOrdinal)
                    .ToListAsync();

                Assert.Equal(2, slots.Count);
                Assert.Equal(FirstMembershipId, slots[0].UserMembershipId);
                Assert.Equal(SecondMembershipId, slots[1].UserMembershipId);
            },
            transactional: false);
    }

    private static Task<OrderPricingResult> PriceAsync(
        IOrderPricingCalculator pricing, DateTime cleaningUtc)
        => pricing.CalculateAsync(
            [ServiceId], [], [], rooms: 0, bathrooms: 0, CurrencyId, cleaningUtc, UserId, AugustUtc,
            CancellationToken.None);

    private static IMembershipBenefitUsageRepository Repository(IServiceProvider provider)
        => provider.GetRequiredService<IMembershipBenefitUsageRepository>();

    private static Task<MembershipBenefitUsage?> ReserveAsync(
        IMembershipBenefitUsageRepository repository,
        string userId,
        string periodKey,
        int maxPerPeriod,
        DateTime nowUtc,
        string membershipId = FirstMembershipId)
        => repository.TryReserveSlotAsync(
            userId, MembershipBenefitKind.ExpressUpgrade, periodKey, membershipId, maxPerPeriod, nowUtc,
            CancellationToken.None);

    private static Task<int> LiveSlotsAsync(CleansiaDbContext context, string userId, string periodKey)
        => context.Set<MembershipBenefitUsage>()
            .IgnoreQueryFilters()
            .CountAsync(u => u.UserId == userId && u.PeriodKey == periodKey && u.IsActive);

    /// <summary>
    /// Cancel-and-resubscribe. The two writes commit separately because the enrolment invariant is a
    /// PostgreSQL unique INDEX on <c>(TenantId, UserId) WHERE Status = Active</c>, and an index — unlike
    /// a constraint — cannot be deferred to commit, so the replacement INSERT must not be batched ahead
    /// of the cancellation UPDATE.
    /// </summary>
    private static async Task ResubscribeAsync(IServiceProvider provider, string planId)
    {
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CleansiaDbContext>();

        var previous = await context.Set<UserMembership>()
            .IgnoreQueryFilters()
            .SingleAsync(m => m.Id == FirstMembershipId);
        previous.UpdateFromStripeWebhook(
            "canceled", DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(20), null);
        await context.CommitAsync(CancellationToken.None);

        var replacement = UserMembership.Create(
            UserId, planId, "sub_expressquota_2", DateTime.UtcNow, DateTime.UtcNow.AddDays(30));
        replacement.Id = SecondMembershipId;
        context.Add(replacement);
        await context.CommitAsync(CancellationToken.None);
    }

    private static async Task SeedAsync(CleansiaDbContext context, int quota, bool withSpendthrift = false)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var currency = Currency.Create("CZK", "Kč", "Czech koruna", 1.0m);
        currency.Id = CurrencyId;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var category = ServiceCategory.Create("express-quota", "Express Quota", "Category under test");
        category.Id = CategoryId;
        context.Add(category);

        var service = Service.Create(
            CategoryId, "Express Quota Service", "Service under test", ServiceBasePrice, 0m, 60);
        service.Id = ServiceId;
        context.Add(service);

        context.Add(CreatePlan(OneWaiverPlanId, "PLUS_MONTHLY", "price_one", 1));
        context.Add(CreatePlan(TwoWaiverPlanId, "PLUS_YEARLY", "price_two", 2));

        context.Add(CreateUser(UserId, "express-quota@cleansia.test"));
        context.Add(CreateMembership(
            FirstMembershipId, UserId, quota == 1 ? OneWaiverPlanId : TwoWaiverPlanId, "sub_expressquota_1"));

        if (withSpendthrift)
        {
            context.Add(CreateUser(SpendthriftUserId, "express-quota-spent@cleansia.test"));
            context.Add(CreateMembership(
                SpendthriftMembershipId, SpendthriftUserId, OneWaiverPlanId, "sub_expressquota_spent"));
        }

        await context.CommitAsync(CancellationToken.None);
    }

    private static MembershipPlan CreatePlan(string id, string code, string priceId, int quota)
    {
        var plan = MembershipPlan.Create(
            code: code,
            name: code,
            monthlyPriceCzk: 199m,
            stripePriceId: priceId,
            discountPercentage: 0m,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true,
            expressUpgradesPerMonth: quota);
        plan.Id = id;
        return plan;
    }

    private static UserMembership CreateMembership(
        string id, string userId, string planId, string stripeSubscriptionId)
    {
        var membership = UserMembership.Create(
            userId, planId, stripeSubscriptionId,
            DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(20));
        membership.Id = id;
        return membership;
    }

    private static User CreateUser(string id, string email)
    {
        var user = User.CreateWithPassword(
            email, Constants.TestUserSession.TestUserPassword, "Express", "Quota", UserProfile.Customer);
        user.Id = id;
        user.ConfirmEmail();
        return user;
    }

    private sealed record MonthlyCapOutcome(
        ExpressWaiver First,
        OrderPricingResult FirstPrice,
        int? ReservedOrdinal,
        ExpressWaiver Second,
        OrderPricingResult SecondPrice);

    private sealed record RolloverOutcome(
        int? JulySpend, int? BankedFirst, int? BankedSecond, int? SpentFirst, int? SpentSecond);

    private sealed record ResubscribeOutcome(
        ExpressWaiver Before, int? ReservedOrdinal, ExpressWaiver After);

    private sealed record SwapOutcome(int? First, int? Second, int? Third);
}
