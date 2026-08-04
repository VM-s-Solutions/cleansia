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
/// The express surcharge and the total must be quoted in the SAME currency. The calculator scales the
/// total by <c>Currency.ExchangeRate</c>, so a surcharge left in base units makes
/// <c>CreateOrder.Handler</c>'s <c>calc.TotalPrice - calc.ExpressSurchargeAmount</c> subtract an
/// unscaled figure from a scaled one — silently inflating the discount base (and therefore the
/// persisted price) the moment a currency with a rate other than 1 goes live. Every assertion here is
/// invisible at rate 1, which is exactly why the bug survived: the suite only ever priced in CZK.
/// </summary>
public class OrderPricingCalculatorExchangeRateTests
{
    private const string ServiceId = "service-1";
    private const string CurrencyId = "currency-eur";
    private const decimal BaseSubtotal = 1000m;
    private const decimal ExchangeRate = 0.04m;

    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<IExtraRepository> _extraRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<IExpressWaiverResolver> _expressWaiverResolver = ExpressWaiverMocks.NoWaiver();

    private OrderPricingCalculator CreateCalculator(decimal exchangeRate)
    {
        var service = Service.Create("category-1", "Standard clean", "desc", BaseSubtotal, perRoomPrice: 0m);
        service.Id = ServiceId;
        _serviceRepository.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(new List<Service> { service }.BuildMock());
        _packageRepository.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(new List<Package>().BuildMock());
        _extraRepository.Setup(r => r.GetAll())
            .Returns(new List<Extra>().BuildMock());

        var currency = Currency.Create("EUR", "€", "Euro", exchangeRate);
        currency.Id = CurrencyId;
        _currencyRepository.Setup(r => r.GetByIdAsync(CurrencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);
        _currencyRepository.Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);

        return new OrderPricingCalculator(
            _serviceRepository.Object,
            _packageRepository.Object,
            _extraRepository.Object,
            _currencyRepository.Object,
            _expressWaiverResolver.Object);
    }

    private Task<Cleansia.Core.AppServices.Services.Interfaces.OrderPricingResult> PriceExpressSlotAsync(
        decimal exchangeRate)
        => CreateCalculator(exchangeRate).CalculateAsync(
            [ServiceId], [], [], rooms: 0, bathrooms: 0, currencyId: CurrencyId,
            cleaningDateUtc: DateTime.UtcNow.AddHours(3),
            userId: null,
            nowUtc: DateTime.UtcNow,
            CancellationToken.None);

    [Fact]
    public async Task ExpressSurcharge_NonUnitExchangeRate_IsQuotedInTheChargeCurrency()
    {
        var result = await PriceExpressSlotAsync(ExchangeRate);

        Assert.True(result.ExpressSurchargeApplied);
        Assert.Equal(
            BaseSubtotal * BookingPolicy.ExpressSurchargeRate * ExchangeRate,
            result.ExpressSurchargeAmount);
    }

    [Fact]
    public async Task ExpressSurcharge_NonUnitExchangeRate_TotalMinusSurchargeIsTheScaledBaseSubtotal()
    {
        var result = await PriceExpressSlotAsync(ExchangeRate);

        // The invariant CreateOrder.Handler relies on to derive the discount base.
        Assert.Equal(BaseSubtotal * ExchangeRate, result.TotalPrice - result.ExpressSurchargeAmount);
    }

    [Fact]
    public async Task ExpressSurcharge_NonUnitExchangeRate_TotalIsUnchanged()
    {
        var result = await PriceExpressSlotAsync(ExchangeRate);

        Assert.Equal(
            (BaseSubtotal + BaseSubtotal * BookingPolicy.ExpressSurchargeRate) * ExchangeRate,
            result.TotalPrice);
    }

    [Fact]
    public async Task ExpressSurcharge_UnitExchangeRate_IsUnchanged()
    {
        var result = await PriceExpressSlotAsync(1m);

        Assert.Equal(BaseSubtotal * BookingPolicy.ExpressSurchargeRate, result.ExpressSurchargeAmount);
        Assert.Equal(BaseSubtotal, result.TotalPrice - result.ExpressSurchargeAmount);
    }

    [Fact]
    public async Task NoExpressSlot_NonUnitExchangeRate_ChargesTheScaledSubtotalOnly()
    {
        var result = await CreateCalculator(ExchangeRate).CalculateAsync(
            [ServiceId], [], [], rooms: 0, bathrooms: 0, currencyId: CurrencyId,
            cleaningDateUtc: DateTime.UtcNow.AddDays(3),
            userId: null,
            nowUtc: DateTime.UtcNow,
            CancellationToken.None);

        Assert.False(result.ExpressSurchargeApplied);
        Assert.Equal(0m, result.ExpressSurchargeAmount);
        Assert.Equal(BaseSubtotal * ExchangeRate, result.TotalPrice);
    }
}
