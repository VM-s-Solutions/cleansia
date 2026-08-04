using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.TestUtilities.MockDataFactories.Users;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0036 D2 — the factory hands the resolver's answer to the aggregate and writes neither hold
/// column itself. The preference and the deadline are two columns with two lifetimes: a declined hold
/// still stores what the customer asked for, because "we stored your preference but could not act on
/// it" has to be expressible.
/// </summary>
public class OrderFactoryPreferredHoldTests
{
    private const string PreferredEmployeeId = "employee-favourite";

    private static readonly DateTime Now = new(2026, 8, 4, 9, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<ICompanyInfoRepository> _companyInfoRepository = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();
    private readonly Mock<IVatCalculator> _vatCalculator = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<IUserMembershipRepository> _userMembershipRepository = new();

    public OrderFactoryPreferredHoldTests()
    {
        _serviceRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Service>().AsQueryable().BuildMock());
        _packageRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Package>().AsQueryable().BuildMock());
    }

    [Fact]
    public async Task A_Granted_Hold_Is_Written_As_The_Pair()
    {
        var holdUntilUtc = Now.AddHours(2);

        var order = await CreateAsync(NoPreferredCleanerHold.Grants(holdUntilUtc));

        Assert.Equal(PreferredEmployeeId, order.PreferredEmployeeId);
        Assert.Equal(holdUntilUtc, order.PreferredHoldUntilUtc);
    }

    [Fact]
    public async Task A_Declined_Hold_Still_Stores_The_Preference()
    {
        var order = await CreateAsync(NoPreferredCleanerHold.Resolver);

        Assert.Equal(PreferredEmployeeId, order.PreferredEmployeeId);
        Assert.Null(order.PreferredHoldUntilUtc);
    }

    /// <summary>
    /// The window the resolver is asked about is the booking's own — its country, its instant, its
    /// derived length, and the caller's clock rather than a second reading of it.
    /// </summary>
    [Fact]
    public async Task The_Resolver_Is_Asked_About_The_Booking_Being_Created()
    {
        var resolver = new Mock<IPreferredCleanerHoldResolver>();
        resolver
            .Setup(r => r.ResolveAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PreferredCleanerOutcome.Declined(HoldDeclineReason.NoMembership));

        var input = Input();
        await NewFactory(resolver.Object).CreateAsync(input, CancellationToken.None);

        resolver.Verify(
            r => r.ResolveAsync(
                input.UserId,
                PreferredEmployeeId,
                input.Address.CountryId,
                input.CleaningDate,
                0,
                input.NowUtc,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private async Task<Cleansia.Core.Domain.Orders.Order> CreateAsync(IPreferredCleanerHoldResolver resolver) =>
        await NewFactory(resolver).CreateAsync(Input(), CancellationToken.None);

    private OrderFactory NewFactory(IPreferredCleanerHoldResolver resolver) =>
        new(
            _orderRepository.Object,
            _serviceRepository.Object,
            _packageRepository.Object,
            _companyInfoRepository.Object,
            _countryConfigurationRepository.Object,
            _vatCalculator.Object,
            _loyaltyService.Object,
            _userMembershipRepository.Object,
            resolver);

    private static CreateOrderInput Input() =>
        new(
            UserId: null,
            CustomerName: "Test Customer",
            CustomerEmail: "customer@example.com",
            CustomerPhone: "+420123456789",
            Address: AddressMockFactory.Generate(),
            Rooms: 2,
            Bathrooms: 1,
            Extras: new Dictionary<string, bool>(),
            CleaningDate: Now.AddDays(3),
            PaymentType: PaymentType.Cash,
            Currency: Currency.Create("CZK", "Kč", "Czech Koruna", 1m),
            SelectedServiceIds: ["service-1"],
            SelectedPackageIds: [],
            RawSubtotal: 1500m,
            NowUtc: Now,
            ReservedExpressWaiver: null,
            PreferredEmployeeId: PreferredEmployeeId);
}
