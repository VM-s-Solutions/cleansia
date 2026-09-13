using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Memberships.Admin;
using Cleansia.Core.AppServices.Features.Memberships.Admin.DTOs;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities.MockDataFactories.Memberships;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Memberships.Admin;

/// <summary>
/// Field-level validation on create, now with a price per currency: an empty or partial price map is
/// legal (a plan unpriced in a market is "Plus not on sale there", ADR-0059 D4), an unknown currency
/// code is refused, each entry needs a non-negative price and a Stripe Price id no other plan uses.
/// </summary>
public class CreateMembershipPlanValidatorTests
{
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<IMembershipPlanPriceRepository> _priceRepository = new();

    public CreateMembershipPlanValidatorTests()
    {
        _currencyRepository
            .Setup(r => r.GetAll())
            .Returns(new[] { MembershipPricingMockFactory.Czk(), MembershipPricingMockFactory.Eur() }.AsQueryable().BuildMock());
        _priceRepository
            .Setup(r => r.IsStripePriceIdUsedAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private CreateMembershipPlan.Validator Validator() => new(_currencyRepository.Object, _priceRepository.Object);

    private static CreateMembershipPlan.Command Valid(Dictionary<string, MembershipPlanPriceInput>? prices = null) =>
        new(
            Code: "PLUS_MONTHLY",
            Name: "Plus Monthly",
            BillingInterval: BillingInterval.Monthly,
            Prices: prices ?? new Dictionary<string, MembershipPlanPriceInput> { ["CZK"] = new(199m, "price_plus_monthly") },
            DiscountPercentage: 5m,
            FreeCancellationWindowHours: 4,
            TrialPeriodDays: 0,
            AllowsExpressUpgrade: true);

    [Fact]
    public async Task Valid_Command_Passes()
    {
        var result = await Validator().ValidateAsync(Valid());
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task NoPricesAtAll_Passes_APlanMayBeOnSaleNowhere()
    {
        Assert.True((await Validator().ValidateAsync(Valid(new Dictionary<string, MembershipPlanPriceInput>()))).IsValid);
        Assert.True((await Validator().ValidateAsync(Valid() with { Prices = null })).IsValid);
    }

    [Fact]
    public async Task APartialPriceMap_Passes_EvenWithAnotherCurrencyActive()
    {
        var result = await Validator().ValidateAsync(Valid());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task AKeyNamingNoCurrency_Fails_CurrencyNotFound()
    {
        var result = await Validator().ValidateAsync(Valid(new Dictionary<string, MembershipPlanPriceInput>
        {
            ["CZK"] = new(199m, "price_czk"),
            ["XXX"] = new(1m, "price_xxx"),
        }));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateMembershipPlan.Command.Prices)
            && e.ErrorMessage == BusinessErrorMessage.CurrencyNotFound);
    }

    [Fact]
    public async Task NegativePrice_Fails_MustBePositive()
    {
        var result = await Validator().ValidateAsync(Valid(new Dictionary<string, MembershipPlanPriceInput>
        {
            ["CZK"] = new(-1m, "price_czk"),
        }));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.MustBePositive);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task EmptyStripePriceId_Fails_Required(string? stripePriceId)
    {
        var result = await Validator().ValidateAsync(Valid(new Dictionary<string, MembershipPlanPriceInput>
        {
            ["CZK"] = new(199m, stripePriceId!),
        }));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.Required);
    }

    [Fact]
    public async Task AStripePriceIdAlreadyChargingAnotherPlan_Fails_StripePriceAlreadyUsed()
    {
        _priceRepository
            .Setup(r => r.IsStripePriceIdUsedAsync("price_A", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await Validator().ValidateAsync(Valid(new Dictionary<string, MembershipPlanPriceInput>
        {
            ["CZK"] = new(199m, "price_A"),
        }));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.MembershipPlanStripePriceAlreadyUsed);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task DiscountOutOfRange_Fails(decimal discount)
    {
        var result = await Validator().ValidateAsync(Valid() with { DiscountPercentage = discount });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateMembershipPlan.Command.DiscountPercentage)
            && e.ErrorMessage == BusinessErrorMessage.MembershipPlanDiscountOutOfRange);
    }

    [Fact]
    public async Task NegativeTrial_Fails()
    {
        var result = await Validator().ValidateAsync(Valid() with { TrialPeriodDays = -1 });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateMembershipPlan.Command.TrialPeriodDays));
    }

    [Fact]
    public async Task NegativeCancellationWindow_Fails_MustBePositive()
    {
        var result = await Validator().ValidateAsync(Valid() with { FreeCancellationWindowHours = -1 });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateMembershipPlan.Command.FreeCancellationWindowHours));
    }

    [Fact]
    public async Task OutOfRangeBillingInterval_Fails_InvalidEnum()
    {
        var result = await Validator().ValidateAsync(Valid() with { BillingInterval = (BillingInterval)99 });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateMembershipPlan.Command.BillingInterval)
            && e.ErrorMessage == BusinessErrorMessage.InvalidEnumValue);
    }
}
