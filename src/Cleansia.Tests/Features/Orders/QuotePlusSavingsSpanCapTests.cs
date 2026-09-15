using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The Plus-savings preview prices the basket QuoteOrder prices, so it draws QuoteOrder's span cap
/// too (ADR-0039 D3.4): savings must not be shown on a selection the booking will refuse on span.
/// The wizard asks for the preview only after a successful quote, which is why this went unnoticed;
/// the two validators still have to agree on the minute, as <see cref="QuoteOrderSpanCapTests"/>
/// pins for quote and create.
/// </summary>
public class QuotePlusSavingsSpanCapTests
{
    private const string CategoryId = "category-span-plus";
    private const string ServiceId = "service-span";
    private const string PackageId = "package-span";
    private const string Czechia = "country-cze";

    private static readonly Currency Czk = CreateOrderTestData.DefaultCurrency();

    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly ICurrencyResolutionService _markets = OrderMarketDoubles.Trading(Czk, (Czechia, Czk));
    private readonly ICountryRepository _countries = OrderMarketDoubles.Servicing(Czechia);

    public QuotePlusSavingsSpanCapTests()
    {
        _serviceRepository
            .Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _packageRepository
            .Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _currencyRepository
            .Setup(r => r.IsOfferableAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _currencyRepository
            .Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Czk);
    }

    [Theory]
    [InlineData(BookingPolicy.MaxBookableOrderSpanMinutes, 0, true)]
    [InlineData(BookingPolicy.MaxBookableOrderSpanMinutes + 1, 0, false)]
    [InlineData(BookingPolicy.MaxBookableOrderSpanMinutes, 1, false)]
    public async Task The_Preview_Draws_The_Cap_Where_The_Quote_Does(int serviceMinutes, int packagedMinutes, bool accepted)
    {
        SeedCatalog(serviceMinutes, packagedMinutes);

        var result = await Validator().ValidateAsync(Query([ServiceId], [PackageId]));

        Assert.Equal(accepted, result.IsValid);
        Assert.Equal(!accepted, result.Errors.Any(e => e.ErrorMessage == BusinessErrorMessage.OrderSpanExceedsMaximum));
    }

    /// <summary>An empty basket previews as it quotes; the cap is not an emptiness rule.</summary>
    [Fact]
    public async Task An_Empty_Selection_Still_Previews()
    {
        SeedCatalog(BookingPolicy.MaxBookableOrderSpanMinutes + 1, 0);

        var result = await Validator().ValidateAsync(Query([], []));

        Assert.True(result.IsValid);
    }

    private QuotePlusSavings.Validator Validator() =>
        new(
            _serviceRepository.Object,
            _packageRepository.Object,
            _currencyRepository.Object,
            _countries,
            _markets,
            CataloguePriceDoubles.Services(Czk, (ServiceId, 500m, 100m)),
            CataloguePriceDoubles.Packages(Czk, (PackageId, 1000m)));

    private static QuotePlusSavings.Query Query(IEnumerable<string> serviceIds, IEnumerable<string> packageIds) =>
        new(serviceIds, packageIds, Rooms: 2, Bathrooms: 1, PlanCode: "plus-monthly", CurrencyId: null, CountryId: Czechia);

    private void SeedCatalog(int serviceMinutes, int packageServiceMinutes)
    {
        var service = Service.Create(CategoryId, "Span Service", "Under test", serviceMinutes);
        service.Id = ServiceId;

        var packagedService = Service.Create(CategoryId, "Packaged Service", "Inside the bundle", packageServiceMinutes);
        packagedService.Id = $"{ServiceId}-packaged";

        var package = Package.Create("Span Package", "Under test");
        package.Id = PackageId;
        package.AddService(packagedService);

        _serviceRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns((IEnumerable<string> ids) => Matching(ids, service));
        _packageRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns((IEnumerable<string> ids) => Matching(ids, package));
    }

    private static IQueryable<TEntity> Matching<TEntity>(IEnumerable<string> ids, params TEntity[] candidates)
        where TEntity : Cleansia.Core.Domain.Common.Auditable
    {
        var requested = ids.ToHashSet();
        return candidates.Where(c => requested.Contains(c.Id)).AsQueryable().BuildMock();
    }
}
