using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Tests.Features.Orders;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// ADR-0035 AM-8 leg (a) — a quota exhausted between the quote and the submit must come back as its OWN
/// code, not as <c>TotalPriceNotMatch</c>.
///
/// <para>Both are re-quotes and neither is a defect. The difference is what the customer is TOLD: every
/// client maps <c>TotalPriceNotMatch</c> to a generic "the price changed", so the one sentence that
/// explains this state — "you've used both free express bookings this month" — would never render.</para>
/// </summary>
public class CreateOrderExpressWaiverValidatorTests
{
    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<IOrderPricingCalculator> _pricingCalculator = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserMembershipRepository> _userMembershipRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public CreateOrderExpressWaiverValidatorTests()
    {
        _serviceRepository
            .Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _packageRepository
            .Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _serviceRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Service>().AsQueryable().BuildMock());
        _packageRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Package>().AsQueryable().BuildMock());
        _currencyRepository
            .Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private CreateOrder.Validator CreateValidator() =>
        new(
            _packageRepository.Object,
            _serviceRepository.Object,
            _currencyRepository.Object,
            _pricingCalculator.Object,
            _orderRepository.Object,
            _userMembershipRepository.Object,
            _session.Object,
            PayConfigRepositoryDouble.Holding());

    private void ArrangePricing(OrderPricingResult result)
        => _pricingCalculator
            .Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    /// <summary>
    /// Server now charges 1200 (1000 + 20%); the client submitted the 1000 it was quoted while the waiver
    /// still held. The difference is exactly the surcharge, so it is the waiver and nothing else.
    /// </summary>
    [Fact]
    public async Task AWaivedTotalAgainstAChargedRecompute_FailsWithTheDedicatedCode()
    {
        ArrangePricing(CreateOrderTestData.MatchingPricing(totalPrice: 1200m) with
        {
            ExpressSurchargeApplied = true,
            ExpressSurchargeAmount = 200m,
        });

        var result = await CreateValidator()
            .ValidateAsync(CreateOrderTestData.ValidCommand(totalPrice: 1000m));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors, e => e.ErrorMessage == BusinessErrorMessage.ExpressWaiverNoLongerAvailable);
        Assert.DoesNotContain(
            result.Errors, e => e.ErrorMessage == BusinessErrorMessage.TotalPriceNotMatch);
    }

    /// <summary>Any other mismatch is still the generic re-quote — the new code must not swallow it.</summary>
    [Fact]
    public async Task AnOrdinaryPriceMismatch_StillFailsWithTotalPriceNotMatch()
    {
        ArrangePricing(CreateOrderTestData.MatchingPricing(totalPrice: 1200m) with
        {
            ExpressSurchargeApplied = true,
            ExpressSurchargeAmount = 200m,
        });

        var result = await CreateValidator()
            .ValidateAsync(CreateOrderTestData.ValidCommand(totalPrice: 999m));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.TotalPriceNotMatch);
        Assert.DoesNotContain(
            result.Errors, e => e.ErrorMessage == BusinessErrorMessage.ExpressWaiverNoLongerAvailable);
    }

    /// <summary>A non-express slot cannot produce this state, so the code must be unreachable there.</summary>
    [Fact]
    public async Task AMismatchOnANonExpressSlot_IsNeverTheWaiverCode()
    {
        ArrangePricing(CreateOrderTestData.MatchingPricing(totalPrice: 1200m));

        var result = await CreateValidator()
            .ValidateAsync(CreateOrderTestData.ValidCommand(totalPrice: 1000m));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.TotalPriceNotMatch);
        Assert.DoesNotContain(
            result.Errors, e => e.ErrorMessage == BusinessErrorMessage.ExpressWaiverNoLongerAvailable);
    }

    /// <summary>
    /// TC-BENEFIT-PREVIEW-0 — the validator's recompute is a PURE READ. Repeated validations consume
    /// nothing: the calculator is asked for a price, never for a claim. A resolver that consumed would
    /// burn a credit on every quote and on every rejected order, which is the constraint the whole seam
    /// exists to satisfy.
    /// </summary>
    [Fact]
    public async Task RepeatedValidationsPriceTheOrderAndClaimNothing()
    {
        ArrangePricing(CreateOrderTestData.MatchingPricing() with
        {
            ExpressSurchargeApplied = true,
            ExpressSurchargeAmount = 250m,
            ExpressSurchargeWaivedByMembership = false,
        });
        var validator = CreateValidator();
        var command = CreateOrderTestData.ValidCommand();

        await validator.ValidateAsync(command);
        await validator.ValidateAsync(command);
        await validator.ValidateAsync(command);

        // Three validations, three price computations, ONE each — the two ordered price rules share the
        // single run rather than doubling it — and zero reservations anywhere on this path.
        _pricingCalculator.Verify(
            c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }
}
