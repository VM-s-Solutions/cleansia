using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Tests.Common;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// AN EXTRA WITH NO PRICE ROW IN THE QUOTE'S CURRENCY IS DROPPED, exactly as an inactive one is — not
/// charged at zero, not thrown on. A service or package in that state is refused by the order
/// validators with a selection key; an extra has no order-level key to be refused by, and the
/// catalogue re-read in the address's currency is what keeps it from being selected again. Before this
/// the calculator threw <see cref="InvalidOperationException"/> on it, and a customer who picked an
/// extra from the CZK catalogue and then entered a Slovak address got a 500 on the next quote.
/// </summary>
public class OrderPricingCalculatorUnpricedExtraTests
{
    private const string ServiceId = "service-1";
    private const string PricedExtraSlug = "inside-oven";
    private const string UnpricedExtraSlug = "inside-fridge";
    private const string CurrencyId = "currency-eur";

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
        _servicePriceRepository.Setup(r => r.GetAll()).Returns(new List<ServicePrice>
        {
            ServicePrice.Create(ServiceId, CurrencyId, 100m, perRoomPrice: 0m)
        }.BuildMock());
        _packagePriceRepository.Setup(r => r.GetAll()).Returns(new List<PackagePrice>().BuildMock());
        _serviceRepository.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(new List<Service> { service }.BuildMock());
        _packageRepository.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(new List<Package>().BuildMock());

        var priced = Extra.Create(PricedExtraSlug, "Inside oven", null);
        var unpriced = Extra.Create(UnpricedExtraSlug, "Inside fridge", null);
        _extraRepository.Setup(r => r.GetAll())
            .Returns(new List<Extra> { priced, unpriced }.BuildMock());
        _extraPriceRepository.Setup(r => r.GetAll()).Returns(new List<ExtraPrice>
        {
            ExtraPrice.Create(priced.Id, CurrencyId, 6m)
        }.BuildMock());

        var currency = Currency.Create("EUR", "€", "Euro");
        currency.Id = CurrencyId;
        _currencyRepository.Setup(r => r.GetByIdAsync(CurrencyId, It.IsAny<CancellationToken>()))
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

    [Fact]
    public async Task An_Extra_With_No_Price_Row_In_The_Currency_Is_Dropped_Not_Thrown_On()
    {
        var result = await CreateCalculator().CalculateAsync(
            [ServiceId], [], [PricedExtraSlug, UnpricedExtraSlug], rooms: 0, bathrooms: 0,
            currencyId: CurrencyId, cleaningDateUtc: null, userId: null, nowUtc: DateTime.UtcNow,
            CancellationToken.None);

        Assert.Equal(6m, result.ExtrasSubtotal);
        Assert.Equal(106m, result.TotalPrice);
        var extraLine = Assert.Single(result.Lines!, l => l.Kind == "extra");
        Assert.Equal(PricedExtraSlug, extraLine.ItemId);
    }
}
