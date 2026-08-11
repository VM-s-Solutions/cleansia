using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.Memberships;

/// <summary>
/// <c>TC-BENEFIT-SQL-0</c>, <c>TC-BENEFIT-SLOTREUSE-0</c>, <c>TC-BENEFIT-DOWNGRADE-0</c> and
/// <c>TC-BENEFIT-RACE-0</c> — the reservation statement against REAL PostgreSQL.
///
/// <para>These cannot be written against a mock or against SQLite. The statement is <i>adapted</i> from
/// the promo archetype, not copied: <c>generate_series</c>, <c>IS NOT DISTINCT FROM</c>,
/// <c>ON CONFLICT DO NOTHING</c> over a filtered <c>NULLS NOT DISTINCT</c> partial index and an
/// explicitly-typed nullable text parameter are all PostgreSQL behaviours. The last one is why the
/// promo path shipped a <c>42P08</c> that only fired in single-tenant mode and survived a tenanted
/// test run — so every case here runs with a NULL tenant, which is the platform's default deployment.</para>
///
/// <para>Non-transactional on purpose: the reservation auto-commits outside the caller's unit of work,
/// which is the whole point of Mode A, and folding it into an ambient test scope would test something
/// else.</para>
/// </summary>
[Collection("PostgresCollection")]
public class MembershipBenefitReservationTests(PostgresContainerFixture fixture)
    : BaseIntegrationTest(fixture)
{
    private const string UserId = "user-benefit-reserve";
    private const string PlanId = "plan-benefit-reserve";
    private const string MembershipId = "membership-benefit-reserve";
    private const string PeriodKey = "C:2026-08";
    private const string OtherPeriodKey = "C:2026-09";
    private const string Email = "benefit-reserve@cleansia.test";

    private static readonly DateTime NowUtc = new(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// <c>TC-BENEFIT-SLOTREUSE-0</c> — the wording is load-bearing: reserve <b>2 of 2</b>, release the
    /// <b>LOWER</b> ordinal, and assert the next reservation takes ordinal <b>0</b>.
    ///
    /// <para>A count-of-live ordinal passes the naive version of this test (reserve one → release →
    /// reserve, whose live set is empty and whose freed ordinal is already 0) and fails this one: with
    /// live = {1}, a count derives 1, collides with the live row, <c>ON CONFLICT DO NOTHING</c> returns
    /// nothing, and capacity is never restored for the rest of the period — while the read path
    /// truthfully reports "1 left".</para>
    /// </summary>
    [Fact]
    public async Task TheOrdinalIsTheSmallestFreeOne_NotACountOfLiveRows()
    {
        await TestMethod(
            arrange: SeedAsync,
            act: async provider =>
            {
                var repository = Repository(provider);
                var first = await ReserveAsync(repository, maxPerPeriod: 2);
                var second = await ReserveAsync(repository, maxPerPeriod: 2);

                await ReleaseAsync(provider, first!.SlotOrdinal);

                var third = await ReserveAsync(repository, maxPerPeriod: 2);
                return new[] { first.SlotOrdinal, second!.SlotOrdinal, third?.SlotOrdinal ?? -1 };
            },
            assert: (CleansiaDbContext _, int[] ordinals) =>
            {
                Assert.Equal(0, ordinals[0]);
                Assert.Equal(1, ordinals[1]);
                Assert.Equal(
                    0,
                    ordinals[2]);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    /// <summary>
    /// <c>TC-BENEFIT-DOWNGRADE-0</c> — the case AM-19 reinstated a bound for.
    ///
    /// <para>Quota was 4 and ordinals {0, 1, 2} are live. The plan swaps down to 2 (the counter carries
    /// across the switch, by owner ruling), then a cleaner-less cancellation releases ordinal 0.
    /// <c>generate_series(0, 1)</c> now finds ordinal 0 free, so the smallest-free-ordinal derivation
    /// alone GRANTS a fourth waiver on a two-waiver plan — while the read path truthfully reports
    /// <c>max(0, 2 - 2) = 0</c> remaining. Read and claim disagree, silently, in the over-granting
    /// direction. The independent cardinality bound in the same statement is what refuses it.</para>
    /// </summary>
    [Fact]
    public async Task AMidPeriodDowngradeCannotGrantBeyondTheNewQuota()
    {
        await TestMethod(
            arrange: SeedAsync,
            act: async provider =>
            {
                var repository = Repository(provider);
                var first = await ReserveAsync(repository, maxPerPeriod: 4);
                await ReserveAsync(repository, maxPerPeriod: 4);
                await ReserveAsync(repository, maxPerPeriod: 4);

                // The plan is downgraded to 2. The already-granted waivers are NOT clawed back
                // (the freeze principle), and the quota now read at every claim is the new one.
                await ReleaseAsync(provider, first!.SlotOrdinal);

                var afterDowngrade = await ReserveAsync(repository, maxPerPeriod: 2);
                var remaining = Math.Max(
                    0,
                    2 - await repository.CountLiveInPeriodAsync(
                        UserId, MembershipBenefitKind.ExpressUpgrade, PeriodKey, CancellationToken.None));

                return new DowngradeOutcome(afterDowngrade?.SlotOrdinal, remaining);
            },
            assert: (CleansiaDbContext _, DowngradeOutcome outcome) =>
            {
                Assert.Null(outcome.GrantedOrdinal);
                Assert.Equal(0, outcome.Remaining);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task AFullQuotaReturnsNullRatherThanThrowing()
    {
        await TestMethod(
            arrange: SeedAsync,
            act: async provider =>
            {
                var repository = Repository(provider);
                await ReserveAsync(repository, maxPerPeriod: 2);
                await ReserveAsync(repository, maxPerPeriod: 2);
                return await ReserveAsync(repository, maxPerPeriod: 2);
            },
            assert: (CleansiaDbContext _, MembershipBenefitUsage? third) =>
            {
                Assert.Null(third);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    /// <summary>
    /// <c>TC-BENEFIT-RACE-0</c> — two concurrent claims with ONE slot left yield exactly one non-null
    /// result and exactly one live row, <b>with a NULL tenant</b>.
    ///
    /// <para>Run only in a tenanted fixture this would pass against a nulls-DISTINCT index that does not
    /// work — i.e. it would prove the opposite of what it claims. Single-tenant mode <i>is</i>
    /// <c>TenantId == null</c>, and PostgreSQL treats NULLs as distinct in a unique index by default, so
    /// without <c>NULLS NOT DISTINCT</c> both rows land and quota 2 becomes quota 3.</para>
    /// </summary>
    [Fact]
    public async Task TwoConcurrentClaimsForTheLastSlotYieldExactlyOneRow()
    {
        await TestMethod(
            arrange: SeedAsync,
            act: async provider =>
            {
                await ReserveAsync(Repository(provider), maxPerPeriod: 2);

                // Separate scopes: two independent DbContexts and two independent connections, which is
                // what two concurrent requests actually are.
                using var scopeA = provider.CreateScope();
                using var scopeB = provider.CreateScope();
                var taskA = ReserveAsync(Repository(scopeA.ServiceProvider), maxPerPeriod: 2);
                var taskB = ReserveAsync(Repository(scopeB.ServiceProvider), maxPerPeriod: 2);
                var results = await Task.WhenAll(taskA, taskB);

                return results.Count(r => r != null);
            },
            assert: async (CleansiaDbContext context, int winners) =>
            {
                Assert.Equal(1, winners);

                var live = await context.Set<MembershipBenefitUsage>()
                    .IgnoreQueryFilters()
                    .CountAsync(u => u.UserId == UserId && u.PeriodKey == PeriodKey && u.IsActive);
                Assert.Equal(2, live);
            },
            transactional: false);
    }

    /// <summary>The period key is the boundary: a new key is a fresh quota, with no sweep and no job.</summary>
    [Fact]
    public async Task ANewPeriodKeyStartsAtOrdinalZeroAgain()
    {
        await TestMethod(
            arrange: SeedAsync,
            act: async provider =>
            {
                var repository = Repository(provider);
                await ReserveAsync(repository, maxPerPeriod: 2);
                await ReserveAsync(repository, maxPerPeriod: 2);
                var nextMonth = await ReserveAsync(repository, maxPerPeriod: 2, periodKey: OtherPeriodKey);
                return nextMonth?.SlotOrdinal ?? -1;
            },
            assert: (CleansiaDbContext _, int ordinal) =>
            {
                Assert.Equal(0, ordinal);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    /// <summary>
    /// <c>TC-BENEFIT-ORPHAN-0</c> — a reservation whose order never committed is <c>OrderId IS NULL</c>
    /// and is reclaimed by the sweep. <c>CleanupStalePendingOrders</c> reads <c>Orders</c> and cannot see
    /// this row; without a sweep of its own a member who abandons two payment sheets loses the month.
    /// </summary>
    [Fact]
    public async Task OrphanedReservationsAreReclaimedAfterTheCutoff()
    {
        await TestMethod(
            arrange: SeedAsync,
            act: async provider =>
            {
                var repository = Repository(provider);
                await ReserveAsync(repository, maxPerPeriod: 2, nowUtc: DateTime.UtcNow.AddHours(-3));
                await ReserveAsync(repository, maxPerPeriod: 2, nowUtc: DateTime.UtcNow);

                var released = await repository.ReleaseOrphanedReservationsAsync(
                    DateTime.UtcNow.AddHours(-1), CancellationToken.None);
                var stillLive = await repository.CountLiveInPeriodAsync(
                    UserId, MembershipBenefitKind.ExpressUpgrade, PeriodKey, CancellationToken.None);

                return new OrphanOutcome(released, stillLive);
            },
            assert: (CleansiaDbContext _, OrphanOutcome outcome) =>
            {
                Assert.Equal(1, outcome.Released);
                Assert.Equal(1, outcome.StillLive);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    private static IMembershipBenefitUsageRepository Repository(IServiceProvider provider)
        => provider.GetRequiredService<IMembershipBenefitUsageRepository>();

    private static Task<MembershipBenefitUsage?> ReserveAsync(
        IMembershipBenefitUsageRepository repository,
        int maxPerPeriod,
        string periodKey = PeriodKey,
        DateTime? nowUtc = null)
        => repository.TryReserveSlotAsync(
            UserId,
            MembershipBenefitKind.ExpressUpgrade,
            periodKey,
            MembershipId,
            maxPerPeriod,
            nowUtc ?? NowUtc,
            CancellationToken.None);

    private static async Task ReleaseAsync(IServiceProvider provider, int slotOrdinal)
    {
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CleansiaDbContext>();
        await context.Set<MembershipBenefitUsage>()
            .IgnoreQueryFilters()
            .Where(u => u.UserId == UserId
                && u.PeriodKey == PeriodKey
                && u.SlotOrdinal == slotOrdinal
                && u.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
    }

    private static async Task SeedAsync(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var plan = MembershipPlan.Create(
            code: "PLUS_MONTHLY",
            name: "Plus Monthly",
            monthlyPriceCzk: 199m,
            stripePriceId: "price_plus_monthly",
            discountPercentage: 5m,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true,
            expressUpgradesPerMonth: 2);
        plan.Id = PlanId;
        context.Add(plan);

        var user = User.CreateWithPassword(
            Email, Constants.TestUserSession.TestUserPassword, "Benefit", "Reserver", UserProfile.Customer);
        user.Id = UserId;
        user.ConfirmEmail();
        context.Add(user);

        var membership = UserMembership.Create(
            UserId, PlanId, "sub_benefit_reserve", NowUtc.AddDays(-10), NowUtc.AddDays(20));
        membership.Id = MembershipId;
        context.Add(membership);

        await context.CommitAsync(CancellationToken.None);
    }

    private sealed record DowngradeOutcome(int? GrantedOrdinal, int Remaining);

    private sealed record OrphanOutcome(int Released, int StillLive);
}
