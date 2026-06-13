using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
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
    private readonly Mock<IUserSessionProvider> _session = new();

    public CreateOrderValidatorCharacterizationTests()
    {
        _serviceRepository
            .Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _packageRepository
            .Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _currencyRepository
            .Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderTestData.MatchingPricing());
    }

    private CreateOrder.Validator CreateValidator() =>
        new(
            _packageRepository.Object,
            _serviceRepository.Object,
            _currencyRepository.Object,
            _pricingCalculator.Object,
            _orderRepository.Object,
            _session.Object);

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
            command.CurrencyId,
            command.CleaningDate,
            It.IsAny<CancellationToken>()), Times.Once);
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
    public async Task AC6_PreferredEmployeeSet_NoUserId_RuleDoesNotFire()
    {
        _session.Setup(s => s.GetUserId()).Returns((string?)null);

        var command = CreateOrderTestData.ValidCommand(preferredEmployeeId: "emp-1");

        var result = await CreateValidator().ValidateAsync(command);

        Assert.True(result.IsValid);
        _orderRepository.Verify(r => r.UserHasCompletedOrderWithEmployeeAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AC6_PreferredEmployeeEligible_WithLoggedInUser_Passes()
    {
        _session.Setup(s => s.GetUserId()).Returns("user-1");
        _orderRepository
            .Setup(r => r.UserHasCompletedOrderWithEmployeeAsync(
                "user-1", "emp-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = CreateOrderTestData.ValidCommand(preferredEmployeeId: "emp-1");

        var result = await CreateValidator().ValidateAsync(command);

        Assert.True(result.IsValid);
    }
}
