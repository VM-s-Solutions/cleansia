using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Memberships.Admin;
using Cleansia.Core.AppServices.Features.Memberships.Admin.DTOs;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities.MockDataFactories.Memberships;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Memberships.Admin;

/// <summary>
/// The update validator applies the same price-entry rules as create, with one difference that
/// matters: a Stripe Price id already on THIS plan's row in THIS currency is the row keeping its
/// id, not a collision — while the same id on the plan's other currency, or twice in one payload, is.
/// </summary>
public class UpdateMembershipPlanValidatorTests
{
    private const string PlanId = "plan-1";

    private readonly Mock<IMembershipPlanRepository> _planRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<IMembershipPlanPriceRepository> _priceRepository = new();

    public UpdateMembershipPlanValidatorTests()
    {
        _planRepository.Setup(r => r.ExistsAsync(PlanId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _currencyRepository
            .Setup(r => r.GetAll())
            .Returns(new[] { MembershipPricingMockFactory.Czk(), MembershipPricingMockFactory.Eur() }.AsQueryable().BuildMock());
        _priceRepository
            .Setup(r => r.IsStripePriceIdUsedAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private UpdateMembershipPlan.Validator Validator() =>
        new(_planRepository.Object, _currencyRepository.Object, _priceRepository.Object);

    private static UpdateMembershipPlan.Command Valid(Dictionary<string, MembershipPlanPriceInput>? prices) =>
        new(PlanId, "Plus Monthly", prices, 5m, 4, 0, true);

    [Fact]
    public async Task OnlySomeCurrencies_Passes()
    {
        var result = await Validator().ValidateAsync(Valid(new Dictionary<string, MembershipPlanPriceInput>
        {
            ["CZK"] = new(199m, "price_czk"),
        }));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task TheDuplicateCheck_ExcludesOnlyThePlansOwnRowInThatCurrency()
    {
        var result = await Validator().ValidateAsync(Valid(new Dictionary<string, MembershipPlanPriceInput>
        {
            ["CZK"] = new(199m, "price_czk"),
            ["EUR"] = new(7.99m, "price_eur"),
        }));

        Assert.True(result.IsValid);
        _priceRepository.Verify(r => r.IsStripePriceIdUsedAsync("price_czk", PlanId, "CZK", It.IsAny<CancellationToken>()), Times.Once);
        _priceRepository.Verify(r => r.IsStripePriceIdUsedAsync("price_eur", PlanId, "EUR", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AStripePriceIdOnThePlansOtherCurrency_Fails_StripePriceAlreadyUsed()
    {
        _priceRepository
            .Setup(r => r.IsStripePriceIdUsedAsync("price_czk", PlanId, "EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await Validator().ValidateAsync(Valid(new Dictionary<string, MembershipPlanPriceInput>
        {
            ["EUR"] = new(7.99m, "price_czk"),
        }));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.MembershipPlanStripePriceAlreadyUsed);
    }

    [Fact]
    public async Task TheSameStripePriceIdTwiceInOnePayload_Fails_StripePriceAlreadyUsed_BeforeTheDatabaseIsAsked()
    {
        var result = await Validator().ValidateAsync(Valid(new Dictionary<string, MembershipPlanPriceInput>
        {
            ["CZK"] = new(199m, "price_shared"),
            ["EUR"] = new(7.99m, "price_shared"),
        }));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(UpdateMembershipPlan.Command.Prices)
            && e.ErrorMessage == BusinessErrorMessage.MembershipPlanStripePriceAlreadyUsed);
    }

    [Fact]
    public async Task AStripePriceIdOnAnotherPlan_Fails_StripePriceAlreadyUsed()
    {
        _priceRepository
            .Setup(r => r.IsStripePriceIdUsedAsync("price_A", PlanId, "CZK", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await Validator().ValidateAsync(Valid(new Dictionary<string, MembershipPlanPriceInput>
        {
            ["CZK"] = new(199m, "price_A"),
        }));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.MembershipPlanStripePriceAlreadyUsed);
    }

    [Fact]
    public async Task AKeyNamingNoCurrency_Fails_CurrencyNotFound()
    {
        var result = await Validator().ValidateAsync(Valid(new Dictionary<string, MembershipPlanPriceInput>
        {
            ["XXX"] = new(1m, "price_xxx"),
        }));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.CurrencyNotFound);
    }
}
