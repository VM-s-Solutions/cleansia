using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.PromoCodes;

/// <summary>
/// DB-atomic cap enforcement for promo-code redemption, written TEST-FIRST (red → green)
/// per knowledge/testing.md (must-cover #3 promo, #6 idempotency S7).
///
/// The race-fix (owner BINDING decision, see ticket) moves both caps off app-layer
/// check-then-act reads onto atomic DB writes:
///   - GLOBAL cap → a conditional <c>ExecuteUpdateAsync</c> on PromoCodes (0 rows ⇒ GlobalLimitReached),
///     surfaced through <see cref="IPromoCodeRepository.TryIncrementGlobalRedemptionsAsync"/>.
///   - PER-USER cap → an atomic slot reservation that RETURNS A RESULT (not an exception),
///     surfaced through <see cref="IPromoCodeRedemptionRepository.TryReserveRedemptionSlotAsync"/>;
///     a race loser observes <c>null</c> ⇒ PerUserLimitReached. The
///     <c>(TenantId, PromoCodeId, UserId, SlotOrdinal)</c> unique index is a backstop only.
///
/// These are LOGIC-LEVEL unit tests: they mock the two atomic repository methods and assert that
/// the service maps the rows-affected / reservation-result branches to the correct
/// <see cref="PromoCodeError"/>. The TRUE-PARALLEL DB proof (real unique-index enforcement + the
/// conditional UPDATE under concurrent writers) is deferred to the integration suite —
/// the in-memory unit harness has no real DB and cannot enforce a unique constraint, so faking
/// genuine parallelism here would be theater. That deferral is flagged in the ticket.
/// </summary>
public class PromoCodeServiceRedeemTests
{
    private const string UserId = "user-1";
    private const string OrderId = "order-1";
    private const string OtherOrderId = "order-2";
    private const string Code = "WELCOME20";
    private const string CodeId = "promo-1";

    private readonly Mock<IPromoCodeRepository> _promoCodes = new();
    private readonly Mock<IPromoCodeRedemptionRepository> _redemptions = new();

    private PromoCodeService CreateService() =>
        new(_promoCodes.Object, _redemptions.Object, NullLogger<PromoCodeService>.Instance);

    private PromoCode ArrangeCode(int maxPerUser = 1, int? globalMax = null, int globalCount = 0)
    {
        var code = PromoCode.CreatePercent(
            Code,
            percent: 0.20m,
            minimumOrderAmount: null,
            maxRedemptionsPerUser: maxPerUser,
            globalMaxRedemptions: globalMax);
        code.Id = CodeId;
        // Drive the denormalised counter to the requested starting value.
        for (var i = 0; i < globalCount; i++)
        {
            code.IncrementRedemptions("test");
        }

        _promoCodes.Setup(r => r.GetByCodeAsync(Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(code);
        return code;
    }

    // No prior redemption row for this order (the non-idempotent path).
    private void ArrangeNoExistingOrderRow()
    {
        _redemptions.Setup(r => r.GetByOrderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PromoCodeRedemption?)null);
    }

    // Global conditional-increment succeeds (rows affected > 0).
    private void ArrangeGlobalIncrementSucceeds()
    {
        _promoCodes.Setup(r => r.TryIncrementGlobalRedemptionsAsync(CodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    // Per-user slot reservation returns a row carrying the given 0-based SlotOrdinal.
    private void ArrangeReservationGrantsSlot(int slotOrdinal)
    {
        _redemptions
            .Setup(r => r.TryReserveRedemptionSlotAsync(
                UserId, CodeId, It.IsAny<int>(), It.IsAny<string>(), It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string user, string codeId, int maxPerUser, string orderId, decimal discount, CancellationToken _) =>
            {
                var row = PromoCodeRedemption.CreateReserved(codeId, user, orderId, discount, slotOrdinal);
                return row;
            });
    }

    // Per-user cap is DB-enforced (M=1, already redeemed once).
    [Fact]
    public async Task AC1_SecondRedemption_For_OneShot_Code_Returns_PerUserLimitReached()
    {
        ArrangeCode(maxPerUser: 1);
        ArrangeNoExistingOrderRow();
        ArrangeGlobalIncrementSucceeds();
        // The atomic reservation observes the slot already taken (M=1 used) ⇒ no slot.
        _redemptions
            .Setup(r => r.TryReserveRedemptionSlotAsync(
                UserId, CodeId, It.IsAny<int>(), It.IsAny<string>(), It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PromoCodeRedemption?)null);

        var result = await CreateService().ApplyAsync(Code, UserId, OtherOrderId, 100m, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PromoCodeError.PerUserLimitReached, result.Error);
        Assert.Equal(0m, result.AppliedDiscount);
    }

    // Concurrent per-user race: the loser maps to PerUserLimitReached.
    // Logic-level: both callers pass the in-memory fast path; the atomic reservation is the
    // arbiter — the winner gets a slot row, the loser gets null. (True-parallel DB proof in the
    // integration suite.)
    [Fact]
    public async Task AC2_RaceLoser_When_Reservation_Returns_Null_Maps_To_PerUserLimitReached()
    {
        ArrangeCode(maxPerUser: 1);
        ArrangeNoExistingOrderRow();
        ArrangeGlobalIncrementSucceeds();
        _redemptions
            .Setup(r => r.TryReserveRedemptionSlotAsync(
                UserId, CodeId, It.IsAny<int>(), It.IsAny<string>(), It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PromoCodeRedemption?)null);

        var result = await CreateService().ApplyAsync(Code, UserId, OrderId, 100m, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PromoCodeError.PerUserLimitReached, result.Error);
        // The loser must NOT also change-track-add a second row — the reservation is the only writer.
        _redemptions.Verify(r => r.Add(It.IsAny<PromoCodeRedemption>()), Times.Never);
        // The global slot reserved above MUST be released, or the global cap leaks one
        // slot per failed per-user reservation.
        _promoCodes.Verify(r => r.DecrementGlobalRedemptionsAsync(CodeId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // When the per-user reservation SUCCEEDS, the global slot is consumed and must NOT be
    // decremented (no spurious compensation).
    [Fact]
    public async Task Successful_Redemption_Does_Not_Decrement_The_Global_Counter()
    {
        ArrangeCode(maxPerUser: 1, globalMax: 100);
        ArrangeNoExistingOrderRow();
        ArrangeGlobalIncrementSucceeds();
        _redemptions
            .Setup(r => r.TryReserveRedemptionSlotAsync(
                UserId, CodeId, It.IsAny<int>(), It.IsAny<string>(), It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PromoCodeRedemption.CreateReserved(CodeId, UserId, OrderId, 20m, slotOrdinal: 0));

        var result = await CreateService().ApplyAsync(Code, UserId, OrderId, 100m, null, CancellationToken.None);

        Assert.True(result.Success);
        _promoCodes.Verify(r => r.DecrementGlobalRedemptionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AC2_RaceWinner_When_Reservation_Grants_Slot_Succeeds_With_Discount()
    {
        ArrangeCode(maxPerUser: 1);
        ArrangeNoExistingOrderRow();
        ArrangeGlobalIncrementSucceeds();
        ArrangeReservationGrantsSlot(slotOrdinal: 0);

        var result = await CreateService().ApplyAsync(Code, UserId, OrderId, 100m, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.Equal(20m, result.AppliedDiscount); // 20% of 100
        Assert.Equal(CodeId, result.PromoCodeId);
    }

    // Global cap is atomic (conditional increment affects 0 rows).
    [Fact]
    public async Task AC3_GlobalCap_Reached_ConditionalIncrement_Affects_Zero_Rows_Returns_GlobalLimitReached()
    {
        // GlobalMax=N and CurrentRedemptionsCount=N: the conditional UPDATE affects 0 rows.
        ArrangeCode(maxPerUser: 5, globalMax: 3, globalCount: 3);
        ArrangeNoExistingOrderRow();
        _promoCodes.Setup(r => r.TryIncrementGlobalRedemptionsAsync(CodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // 0 rows affected

        var result = await CreateService().ApplyAsync(Code, UserId, OrderId, 100m, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PromoCodeError.GlobalLimitReached, result.Error);
        // No redemption-slot reservation is attempted once the global cap rejects.
        _redemptions.Verify(r => r.TryReserveRedemptionSlotAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AC3_GlobalCap_RaceWinner_At_N_Minus_1_Succeeds()
    {
        // Two concurrent attempts at N-1: the atomic UPDATE lets exactly one through (rows>0),
        // the other gets 0 rows. This asserts the winner branch.
        ArrangeCode(maxPerUser: 5, globalMax: 3, globalCount: 2);
        ArrangeNoExistingOrderRow();
        ArrangeGlobalIncrementSucceeds();
        ArrangeReservationGrantsSlot(slotOrdinal: 0);

        var result = await CreateService().ApplyAsync(Code, UserId, OrderId, 100m, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(20m, result.AppliedDiscount);
    }

    // Multi-use codes still work (M>1: M succeed, M+1th rejected).
    [Fact]
    public async Task AC4_MultiUse_Code_Grants_Distinct_Ordinals_For_Each_Of_M_Redemptions()
    {
        const int max = 3;
        ArrangeCode(maxPerUser: max);
        ArrangeNoExistingOrderRow();
        ArrangeGlobalIncrementSucceeds();

        // The reservation hands back ordinals 0,1,2 on successive calls, then null on the 4th.
        var granted = 0;
        _redemptions
            .Setup(r => r.TryReserveRedemptionSlotAsync(
                UserId, CodeId, max, It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string user, string codeId, int maxPerUser, string orderId, decimal discount, CancellationToken _) =>
            {
                if (granted >= maxPerUser)
                {
                    return null;
                }
                var row = PromoCodeRedemption.CreateReserved(codeId, user, orderId, discount, granted);
                granted++;
                return row;
            });

        var service = CreateService();
        for (var i = 0; i < max; i++)
        {
            var ok = await service.ApplyAsync(Code, UserId, $"order-{i}", 100m, null, CancellationToken.None);
            Assert.True(ok.Success);
            Assert.Equal(20m, ok.AppliedDiscount);
        }

        var overCap = await service.ApplyAsync(Code, UserId, "order-overflow", 100m, null, CancellationToken.None);
        Assert.False(overCap.Success);
        Assert.Equal(PromoCodeError.PerUserLimitReached, overCap.Error);
    }

    // Per-order idempotency unchanged (existing row short-circuits).
    [Fact]
    public async Task AC6_ExistingOrderRow_ShortCircuits_To_Existing_Result_Without_Reserving()
    {
        ArrangeCode(maxPerUser: 1);
        var existing = PromoCodeRedemption.CreateReserved(CodeId, UserId, OrderId, 17m, slotOrdinal: 0);
        _redemptions.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateService().ApplyAsync(Code, UserId, OrderId, 100m, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(17m, result.AppliedDiscount);
        Assert.Equal(CodeId, result.PromoCodeId);
        // Idempotent re-call must not touch either atomic write path.
        _promoCodes.Verify(r => r.TryIncrementGlobalRedemptionsAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _redemptions.Verify(r => r.TryReserveRedemptionSlotAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
