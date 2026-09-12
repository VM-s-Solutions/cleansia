using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Characterization of <c>CreateOrder.Validator</c> as it stands after the Wave-0 F2 change and before
/// the AUD-06 decomposition. Pins the observable validation contract — the happy pass and every rule's
/// <see cref="BusinessErrorMessage"/> code — so the future handler split can be proven behavior-preserving.
/// </summary>
public class CreateOrderValidatorCharacterizationTests
{
    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<IOrderPricingCalculator> _pricingCalculator = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserMembershipRepository> _userMembershipRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public CreateOrderValidatorCharacterizationTests()
    {
        _serviceRepository
            .Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _packageRepository
            .Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        // The span-cap rule reads the catalog's durations; an empty catalog is 0 minutes, which keeps
        // every case here on the side of the cap it was written for. OrderSpanCapTests owns the bound.
        _serviceRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Service>().AsQueryable().BuildMock());
        _packageRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Package>().AsQueryable().BuildMock());
        _currencyRepository
            .Setup(r => r.IsOfferableAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _pricingCalculator
            .Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderTestData.MatchingPricing());
    }

    private CreateOrder.Validator CreateValidator() =>
        new(
            _packageRepository.Object,
            _serviceRepository.Object,
            _pricingCalculator.Object,
            _orderRepository.Object,
            _userMembershipRepository.Object,
            _session.Object,
            PayConfigRepositoryDouble.Holding(),
            _currencyRepository.Object);

    [Fact]
    public async Task AC1_HappyPath_Passes()
    {
        var result = await CreateValidator().ValidateAsync(CreateOrderTestData.ValidCommand());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task AC2_BothAddressInputsSet_FailsAddressExactlyOneRequired()
    {
        var command = CreateOrderTestData.ValidCommand() with
        {
            CustomerAddress = CreateOrderTestData.InlineAddress(),
            SavedAddressId = "saved-1",
        };

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateOrder.Command.CustomerAddress)
            && e.ErrorMessage == BusinessErrorMessage.OrderAddressExactlyOneRequired);
    }

    [Fact]
    public async Task AC2_NeitherAddressInputSet_FailsAddressExactlyOneRequired()
    {
        var command = CreateOrderTestData.ValidCommand() with
        {
            CustomerAddress = null,
            SavedAddressId = null,
        };

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateOrder.Command.CustomerAddress)
            && e.ErrorMessage == BusinessErrorMessage.OrderAddressExactlyOneRequired);
    }

    [Fact]
    public async Task AC3_EmptyOrder_FailsEmptyOrder_BeforePriceCheck()
    {
        var command = CreateOrderTestData.ValidCommand(
            serviceIds: Array.Empty<string>(),
            packageIds: Array.Empty<string>());

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmptyOrder);
        // Cascade.Stop on the (empty-then-price) rule: the price check never runs for an empty order.
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.TotalPriceNotMatch);
    }

    [Fact]
    public async Task AC4_PriceMismatch_FailsTotalPriceNotMatch()
    {
        _pricingCalculator
            .Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderTestData.MatchingPricing(totalPrice: 1500m));

        var command = CreateOrderTestData.ValidCommand(totalPrice: 1499m);

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.TotalPriceNotMatch);
    }

    [Fact]
    public async Task AC4_PriceMatch_PassesPriceCheck_WithCleaningDatePassedToCalculator()
    {
        var command = CreateOrderTestData.ValidCommand();

        var result = await CreateValidator().ValidateAsync(command);

        Assert.True(result.IsValid);
        _pricingCalculator.Verify(c => c.CalculateAsync(
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            // The caller's currency — the same field the quote priced with.
            command.CurrencyId,
            command.CleaningDate,
            It.IsAny<string?>(),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------------------------------------------------------- the caller's currency

    /// <summary>
    /// THE CALLER NAMES THE CURRENCY, AND THE PLATFORM CHECKS IT CAN QUOTE IN IT. This inverts the test
    /// that used to sit here, which asserted the caller's currency never reached the calculator. That
    /// was the right guard against the Wave A hole — "exists" was the only check, every seeded currency
    /// existed, and a named HUF multiplied the CZK catalogue by its stored rate. Nothing converts any
    /// more: a currency selects which price ROWS are read, so honouring it is safe once the only
    /// currencies honoured are OFFERABLE ones — switched on and priced. That predicate is the guard
    /// now, and this pins that it is consulted and that its answer decides.
    /// </summary>
    [Fact]
    public async Task An_Offerable_Currency_Reaches_The_Calculator()
    {
        var command = CreateOrderTestData.ValidCommand() with { CurrencyId = "currency-eur" };
        _currencyRepository
            .Setup(r => r.IsOfferableAsync("currency-eur", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateValidator().ValidateAsync(command);

        _pricingCalculator.Verify(c => c.CalculateAsync(
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            "currency-eur",
            It.IsAny<DateTime?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    /// <summary>
    /// The refusal is keyed to the field, and the calculator is never run: it throws on a currency it
    /// cannot price in, so reaching it with a bad one would turn a 400 into a 500. That is why the rule
    /// heads the price chain rather than standing alone — the class cascade is Continue.
    /// </summary>
    [Fact]
    public async Task A_Currency_The_Platform_Cannot_Quote_In_Is_Refused_Before_Pricing()
    {
        var command = CreateOrderTestData.ValidCommand() with { CurrencyId = "currency-huf" };
        _currencyRepository
            .Setup(r => r.IsOfferableAsync("currency-huf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        var failure = Assert.Single(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidCurrency);
        Assert.Equal(nameof(CreateOrder.Command.CurrencyId), failure.ErrorCode);
        _pricingCalculator.Verify(c => c.CalculateAsync(
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<string?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Null is the platform default, and needs no lookup to be one.</summary>
    [Fact]
    public async Task No_Currency_Named_Is_The_Platform_Default_And_Consults_Nothing()
    {
        var command = CreateOrderTestData.ValidCommand() with { CurrencyId = null };

        var result = await CreateValidator().ValidateAsync(command);

        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidCurrency);
        _currencyRepository.Verify(
            r => r.IsOfferableAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AC5_PastCleaningDate_FailsCleaningDateInFuture()
    {
        var command = CreateOrderTestData.ValidCommand(cleaningDate: DateTime.UtcNow.AddHours(-1));

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.CleaningDateInFuture);
    }

    [Fact]
    public async Task AC5_FutureDateBelowLeadTime_FailsCleaningDateBelowLeadTime()
    {
        // ExpressLeadTimeHours is 2h — one hour out is in the future but below the minimum lead time.
        var command = CreateOrderTestData.ValidCommand(cleaningDate: DateTime.UtcNow.AddHours(1));

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.CleaningDateBelowLeadTime);
    }

    [Fact]
    public async Task AC6_PreferredEmployeeIneligible_WithLoggedInUser_FailsPreferredEmployeeNotEligible()
    {
        _session.Setup(s => s.GetUserId()).Returns("user-1");
        ArrangeActiveMembership("user-1");
        _orderRepository
            .Setup(r => r.UserHasCompletedOrderWithEmployeeAsync(
                "user-1", "emp-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = CreateOrderTestData.ValidCommand(preferredEmployeeId: "emp-1");

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateOrder.Command.PreferredEmployeeId)
            && e.ErrorMessage == BusinessErrorMessage.PreferredEmployeeNotEligible);
    }

    [Fact]
    public async Task AC6_PreferredEmployeeSet_NoUserId_FailsMembershipRequired()
    {
        _session.Setup(s => s.GetUserId()).Returns((string?)null);

        var command = CreateOrderTestData.ValidCommand(preferredEmployeeId: "emp-1");

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateOrder.Command.PreferredEmployeeId)
            && e.ErrorMessage == BusinessErrorMessage.PreferredEmployeeMembershipRequired);
        _orderRepository.Verify(r => r.UserHasCompletedOrderWithEmployeeAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AC6_PreferredEmployeeSet_NoMembership_FailsMembershipRequired()
    {
        _session.Setup(s => s.GetUserId()).Returns("user-1");
        _userMembershipRepository
            .Setup(r => r.GetEntitledForUserNoTrackingAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);

        var command = CreateOrderTestData.ValidCommand(preferredEmployeeId: "emp-1");

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateOrder.Command.PreferredEmployeeId)
            && e.ErrorMessage == BusinessErrorMessage.PreferredEmployeeMembershipRequired);
        _orderRepository.Verify(r => r.UserHasCompletedOrderWithEmployeeAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AC6_PreferredEmployeeEligible_WithLoggedInUser_Passes()
    {
        _session.Setup(s => s.GetUserId()).Returns("user-1");
        ArrangeActiveMembership("user-1");
        _orderRepository
            .Setup(r => r.UserHasCompletedOrderWithEmployeeAsync(
                "user-1", "emp-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = CreateOrderTestData.ValidCommand(preferredEmployeeId: "emp-1");

        var result = await CreateValidator().ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    // ── SpecialInstructions — optional free-text, capped at 2000 ──

    [Fact]
    public async Task SpecialInstructions_Omitted_Passes()
    {
        var result = await CreateValidator().ValidateAsync(
            CreateOrderTestData.ValidCommand(specialInstructions: null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task SpecialInstructions_AtMaxLength_Passes()
    {
        var command = CreateOrderTestData.ValidCommand(
            specialInstructions: new string('x', 2000));

        var result = await CreateValidator().ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task SpecialInstructions_OverMaxLength_FailsMaxLength()
    {
        var command = CreateOrderTestData.ValidCommand(
            specialInstructions: new string('x', 2001));

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateOrder.Command.SpecialInstructions)
            && e.ErrorMessage == BusinessErrorMessage.MaxLength);
    }

    // ── AccessInstructions — optional free-text, capped at 2000 ──

    [Fact]
    public async Task AccessInstructions_Omitted_Passes()
    {
        var result = await CreateValidator().ValidateAsync(
            CreateOrderTestData.ValidCommand(accessInstructions: null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task AccessInstructions_AtMaxLength_Passes()
    {
        var command = CreateOrderTestData.ValidCommand(
            accessInstructions: new string('x', 2000));

        var result = await CreateValidator().ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task AccessInstructions_OverMaxLength_FailsMaxLength()
    {
        var command = CreateOrderTestData.ValidCommand(
            accessInstructions: new string('x', 2001));

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateOrder.Command.AccessInstructions)
            && e.ErrorMessage == BusinessErrorMessage.MaxLength);
    }

    private void ArrangeActiveMembership(string userId)
    {
        var plan = MembershipPlan.Create(
            code: "PLUS",
            name: "Cleansia Plus",
            monthlyPriceCzk: 199m,
            stripePriceId: "price_plus",
            discountPercentage: 10m,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true);

        _userMembershipRepository
            .Setup(r => r.GetEntitledForUserNoTrackingAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserMembership.Create(
                userId: userId,
                membershipPlanId: plan.Id,
                stripeSubscriptionId: "sub_1",
                currentPeriodStart: DateTime.UtcNow.AddDays(-1),
                currentPeriodEnd: DateTime.UtcNow.AddMonths(1)));
    }
}
