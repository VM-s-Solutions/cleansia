using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The quote half of ADR-0039 D3.4. <see cref="OrderSpanCapTests"/> pins the cap where it is
/// ENFORCED (<c>OrderFactory</c>) and where the customer is told (<c>CreateOrder.Validator</c>);
/// these pin it where the customer is told <b>in time</b> — a selection the platform will refuse to
/// book must not first come back priced.
///
/// <para>The boundary theory is the load-bearing one. Quote and create sum the same catalog through
/// two separate copies of the same aggregate, so "they agree" has to be asserted, not assumed: a
/// quote that accepts what create rejects is a customer walked to checkout and turned away there,
/// and a quote that rejects what create accepts is a sale refused for nothing.</para>
/// </summary>
public class QuoteOrderSpanCapTests
{
    private const string CategoryId = "category-span-quote";
    private const string ServiceId = CreateOrderTestData.ServiceId;
    private const string PackageId = CreateOrderTestData.PackageId;

    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<IOrderPricingCalculator> _pricingCalculator = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public QuoteOrderSpanCapTests()
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
                It.IsAny<string?>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderTestData.MatchingPricing());

        SeedCatalog(serviceMinutes: 0, packageServiceMinutes: 0);
    }

    [Fact]
    public async Task A_Quote_Exactly_At_The_Cap_Is_Priced()
    {
        SeedCatalog(serviceMinutes: BookingPolicy.MaxBookableOrderSpanMinutes, packageServiceMinutes: 0);

        var result = await QuoteValidator().ValidateAsync(QuoteCommand());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task A_Quote_One_Minute_Over_The_Cap_Is_Refused_With_The_Create_Paths_Key()
    {
        SeedCatalog(serviceMinutes: BookingPolicy.MaxBookableOrderSpanMinutes + 1, packageServiceMinutes: 0);

        var result = await QuoteValidator().ValidateAsync(QuoteCommand());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OrderSpanExceedsMaximum);
    }

    /// <summary>
    /// The package arm is a <c>SelectMany</c> over included services and is the half a re-implementation
    /// forgets — a selection can cross the cap without a single directly-selected service doing so.
    /// </summary>
    [Fact]
    public async Task A_Quote_Pushed_Over_The_Cap_By_A_Package_Is_Refused()
    {
        SeedCatalog(serviceMinutes: BookingPolicy.MaxBookableOrderSpanMinutes, packageServiceMinutes: 1);

        var result = await QuoteValidator().ValidateAsync(QuoteCommand());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OrderSpanExceedsMaximum);
    }

    /// <summary>
    /// The wizard quotes before anything is selected. The cap must not turn that first call — nor any
    /// step where the customer has cleared their basket — into an error; create's emptiness rule stays
    /// create's.
    /// </summary>
    [Fact]
    public async Task An_Empty_Selection_Still_Quotes()
    {
        SeedCatalog(serviceMinutes: BookingPolicy.MaxBookableOrderSpanMinutes + 1, packageServiceMinutes: 0);

        var result = await QuoteValidator().ValidateAsync(
            new QuoteOrder.Command([], [], Rooms: 2, Bathrooms: 1, CurrencyId: CreateOrderTestData.CurrencyId));

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// The whole point of the ticket. Quote and create must draw the line on the same minute, or the
    /// customer is either quoted for a booking they cannot place or refused one they could have.
    /// </summary>
    [Theory]
    [InlineData(BookingPolicy.MaxBookableOrderSpanMinutes - 1, true)]
    [InlineData(BookingPolicy.MaxBookableOrderSpanMinutes, true)]
    [InlineData(BookingPolicy.MaxBookableOrderSpanMinutes + 1, false)]
    public async Task The_Quote_And_The_Create_Path_Draw_The_Boundary_In_The_Same_Place(
        int totalMinutes, bool accepted)
    {
        SeedCatalog(serviceMinutes: totalMinutes - 60, packageServiceMinutes: 60);

        var quote = await QuoteValidator().ValidateAsync(QuoteCommand());
        var create = await CreateValidator().ValidateAsync(CreateOrderTestData.ValidCommand());

        Assert.Equal(accepted, quote.IsValid);
        Assert.Equal(accepted, create.IsValid);
    }

    /// <summary>Both validators are handed the same catalog under the ids both commands select.</summary>
    private void SeedCatalog(int serviceMinutes, int packageServiceMinutes)
    {
        var service = Service.Create(CategoryId, "Span Service", "Under test", 1000m, 0m, serviceMinutes);
        service.Id = ServiceId;

        var packagedService = Service.Create(
            CategoryId, "Packaged Service", "Inside the bundle", 500m, 0m, packageServiceMinutes);
        packagedService.Id = $"{ServiceId}-packaged";

        var package = Package.Create("Span Package", "Under test", 500m);
        package.Id = PackageId;
        package.AddService(packagedService);

        // Filtered by id rather than returned wholesale, so the empty-selection case is a real empty
        // query and not an artefact of the mock.
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

    private QuoteOrder.Validator QuoteValidator() =>
        new(_serviceRepository.Object, _packageRepository.Object, _currencyRepository.Object);

    private CreateOrder.Validator CreateValidator() =>
        new(
            _packageRepository.Object,
            _serviceRepository.Object,
            _currencyRepository.Object,
            _pricingCalculator.Object,
            _orderRepository.Object,
            _session.Object);

    private static QuoteOrder.Command QuoteCommand() =>
        new([ServiceId], [PackageId], Rooms: 2, Bathrooms: 1, CurrencyId: CreateOrderTestData.CurrencyId);
}
