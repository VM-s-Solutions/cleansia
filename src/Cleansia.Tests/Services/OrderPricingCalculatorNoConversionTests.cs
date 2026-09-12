using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Tests.Common;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// <b>The calculator converts nothing, and there is no longer a rate for it to ignore.</b>
///
/// <para>This class used to assert the opposite — that every money figure was SCALED by the stored
/// rate — and its fixtures are kept because they are the only ones in the suite that price against a
/// currency whose rate is not 1. The assertions are inverted: owner ruling 2026-09-08 (Option B) is
/// that a price is <b>authored</b> per currency and never converted, so a rate column must not be able
/// to move a price no matter what an admin types into it.</para>
///
/// <para>Every test here is invisible at rate 1, which is exactly the point. The suite only ever
/// priced in CZK, which is how the conversion path shipped unexercised in the first place — so the
/// guard against it coming back has to price at a rate that is not 1 and prove nothing moves.</para>
/// </summary>
public class OrderPricingCalculatorNoConversionTests
{
    private const string ServiceId = "service-1";
    private const string PackageId = "package-1";
    private const string ExtraSlug = "inside-oven";
    private const string CurrencyId = "currency-eur";
    private const decimal BaseSubtotal = 1000m;
    private const decimal PackageAmount = 250m;
    private const decimal ExtraAmount = 75m;

    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<IExtraRepository> _extraRepository = new();
    private readonly Mock<IServicePriceRepository> _servicePriceRepository = new();
    private readonly Mock<IPackagePriceRepository> _packagePriceRepository = new();
    private readonly Mock<IExtraPriceRepository> _extraPriceRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<IExpressWaiverResolver> _expressWaiverResolver = ExpressWaiverMocks.NoWaiver();

    private OrderPricingCalculator CreateCalculator()
    {
        var service = Service.Create("category-1", "Standard clean", "desc");
        service.Id = ServiceId;

        // The subtotal is a PRICE ROW in this currency now, not a column on the service. That is the
        // whole subject of the class restated: the row is what the calculator charges, and the rate on
        // the currency beside it must not be able to move it.
        _servicePriceRepository.Setup(r => r.GetAll()).Returns(new List<ServicePrice>
        {
            ServicePrice.Create(ServiceId, CurrencyId, BaseSubtotal, perRoomPrice: 0m)
        }.BuildMock());
        _packagePriceRepository.Setup(r => r.GetAll()).Returns(new List<PackagePrice>().BuildMock());
        _extraPriceRepository.Setup(r => r.GetAll()).Returns(new List<ExtraPrice>().BuildMock());
        _serviceRepository.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(new List<Service> { service }.BuildMock());
        _packageRepository.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(new List<Package>().BuildMock());
        _extraRepository.Setup(r => r.GetAll())
            .Returns(new List<Extra>().BuildMock());

        var currency = Currency.Create("EUR", "€", "Euro");
        currency.Id = CurrencyId;
        _currencyRepository.Setup(r => r.GetByIdAsync(CurrencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);
        _currencyRepository.Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);

        return new OrderPricingCalculator(
            _serviceRepository.Object,
            _packageRepository.Object,
            _extraRepository.Object,
            _servicePriceRepository.Object,
            _packagePriceRepository.Object,
            _extraPriceRepository.Object,
            _currencyRepository.Object,
            _expressWaiverResolver.Object);
    }

    private OrderPricingCalculator CreateMixedBasketCalculator()
    {
        var calculator = CreateCalculator();

        var package = Package.Create("Deep clean bundle", "desc");
        package.Id = PackageId;
        _packageRepository.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(new List<Package> { package }.BuildMock());

        var extra = Extra.Create(ExtraSlug, "Inside oven", null);
        _extraRepository.Setup(r => r.GetAll())
            .Returns(new List<Extra> { extra }.BuildMock());

        // Re-stubbed rather than arranged up front, because the doubles are shared with
        // CreateCalculator and this basket adds two priced lines to the one service it already had.
        _packagePriceRepository.Setup(r => r.GetAll()).Returns(new List<PackagePrice>
        {
            PackagePrice.Create(PackageId, CurrencyId, PackageAmount)
        }.BuildMock());
        _extraPriceRepository.Setup(r => r.GetAll()).Returns(new List<ExtraPrice>
        {
            ExtraPrice.Create(extra.Id, CurrencyId, ExtraAmount)
        }.BuildMock());

        return calculator;
    }
    private Task<Cleansia.Core.AppServices.Services.Interfaces.OrderPricingResult> PriceMixedBasketAsync()
        => CreateMixedBasketCalculator().CalculateAsync(
            [ServiceId], [PackageId], [ExtraSlug], rooms: 0, bathrooms: 0, currencyId: CurrencyId,
            cleaningDateUtc: DateTime.UtcNow.AddHours(3),
            userId: null,
            nowUtc: DateTime.UtcNow,
            CancellationToken.None);
    private Task<Cleansia.Core.AppServices.Services.Interfaces.OrderPricingResult> PriceExpressSlotAsync()
        => CreateCalculator().CalculateAsync(
            [ServiceId], [], [], rooms: 0, bathrooms: 0, currencyId: CurrencyId,
            cleaningDateUtc: DateTime.UtcNow.AddHours(3),
            userId: null,
            nowUtc: DateTime.UtcNow,
            CancellationToken.None);

    [Fact]
    public async Task ExpressSurcharge_IsUnmovedByANonUnitExchangeRate()
    {
        var result = await PriceExpressSlotAsync();

        Assert.True(result.ExpressSurchargeApplied);
        Assert.Equal(
            BaseSubtotal * BookingPolicy.ExpressSurchargeRate,
            result.ExpressSurchargeAmount);
    }

    [Fact]
    public async Task TotalMinusSurcharge_IsTheAuthoredSubtotal_AtAnyStoredRate()
    {
        var result = await PriceExpressSlotAsync();

        // The invariant CreateOrder.Handler relies on to derive the discount base.
        Assert.Equal(BaseSubtotal, result.TotalPrice - result.ExpressSurchargeAmount);
    }

    [Fact]
    public async Task Total_IsUnmovedByANonUnitExchangeRate()
    {
        var result = await PriceExpressSlotAsync();

        Assert.Equal(
            BaseSubtotal + BaseSubtotal * BookingPolicy.ExpressSurchargeRate,
            result.TotalPrice);
    }

    [Fact]
    public async Task ExpressSurcharge_UnitExchangeRate_IsUnchanged()
    {
        var result = await PriceExpressSlotAsync();

        Assert.Equal(BaseSubtotal * BookingPolicy.ExpressSurchargeRate, result.ExpressSurchargeAmount);
        Assert.Equal(BaseSubtotal, result.TotalPrice - result.ExpressSurchargeAmount);
    }

    /// <summary>
    /// The broken-out line items are the same money as the total and must carry the same unit. They are
    /// rendered under the quote's own <c>CurrencyCode</c>, so an unscaled services row against a scaled
    /// total prints a Kč figure labelled €. Same method, same class of defect as the surcharge above.
    /// </summary>
    [Fact]
    public async Task LineItemSubtotals_AreUnmovedByANonUnitExchangeRate()
    {
        var result = await PriceMixedBasketAsync();

        Assert.Equal(BaseSubtotal, result.ServicesSubtotal);
        Assert.Equal(PackageAmount, result.PackagesSubtotal);
        Assert.Equal(ExtraAmount, result.ExtrasSubtotal);
    }

    /// <summary>
    /// The wizard's breakdown has to add up: the three rows are the pre-surcharge subtotal, which is
    /// exactly what every client derives as <c>totalPrice - expressSurchargeAmount</c>.
    /// </summary>
    [Fact]
    public async Task LineItemSubtotals_StillSumToThePreSurchargeSubtotal()
    {
        var result = await PriceMixedBasketAsync();

        Assert.Equal(
            result.TotalPrice - result.ExpressSurchargeAmount,
            result.ServicesSubtotal + result.PackagesSubtotal + result.ExtrasSubtotal);
    }

    [Fact]
    public async Task LineItemSubtotals_UnitExchangeRate_AreUnchanged()
    {
        var result = await PriceMixedBasketAsync();

        Assert.Equal(BaseSubtotal, result.ServicesSubtotal);
        Assert.Equal(PackageAmount, result.PackagesSubtotal);
        Assert.Equal(ExtraAmount, result.ExtrasSubtotal);
    }

    [Fact]
    public async Task NoExpressSlot_ChargesTheAuthoredSubtotal_AndReportsRateOne()
    {
        var result = await CreateCalculator().CalculateAsync(
            [ServiceId], [], [], rooms: 0, bathrooms: 0, currencyId: CurrencyId,
            cleaningDateUtc: DateTime.UtcNow.AddDays(3),
            userId: null,
            nowUtc: DateTime.UtcNow,
            CancellationToken.None);

        Assert.False(result.ExpressSurchargeApplied);
        Assert.Equal(0m, result.ExpressSurchargeAmount);
        Assert.Equal(BaseSubtotal, result.TotalPrice);
    }
}
