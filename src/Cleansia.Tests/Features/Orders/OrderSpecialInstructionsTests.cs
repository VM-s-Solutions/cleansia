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
/// The customer's booking-time free text used to be captured by both mobile
/// clients and dropped on the floor: <c>Order.SpecialInstructions</c> existed and
/// was read by every partner/admin surface, but nothing ever wrote it. These pin
/// the write path end-to-end — <c>CreateOrder.Command</c> → <c>CreateOrderInput</c>
/// → <c>Order.SpecialInstructions</c> — so the field cannot silently go dark again.
/// </summary>
public class OrderSpecialInstructionsTests
{
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<ICompanyInfoRepository> _companyInfoRepository = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();
    private readonly Mock<IVatCalculator> _vatCalculator = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<IUserMembershipRepository> _userMembershipRepository = new();

    public OrderSpecialInstructionsTests()
    {
        _serviceRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Service>().AsQueryable().BuildMock());
        _packageRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Package>().AsQueryable().BuildMock());
    }

    private OrderFactory CreateFactory() =>
        new(
            _orderRepository.Object,
            _serviceRepository.Object,
            _packageRepository.Object,
            _companyInfoRepository.Object,
            _countryConfigurationRepository.Object,
            _vatCalculator.Object,
            _loyaltyService.Object,
            _userMembershipRepository.Object);

    /// <summary>
    /// Anonymous (no user id) keeps the factory off the loyalty/membership
    /// lookups — this suite is about the note, not the discount math.
    /// </summary>
    private static CreateOrderInput Input(string? specialInstructions) =>
        new(
            UserId: null,
            CustomerName: "Test Customer",
            CustomerEmail: "customer@example.com",
            CustomerPhone: "+420123456789",
            Address: AddressMockFactory.Generate(),
            Rooms: 2,
            Bathrooms: 1,
            Extras: new Dictionary<string, bool>(),
            CleaningDate: DateTime.UtcNow.AddDays(3),
            PaymentType: PaymentType.Cash,
            Currency: Currency.Create("CZK", "Kč", "Czech Koruna", 1m),
            SelectedServiceIds: ["service-1"],
            SelectedPackageIds: [],
            RawSubtotal: 1500m,
            SpecialInstructions: specialInstructions);

    [Fact]
    public async Task Factory_PersistsSpecialInstructionsOnTheOrder()
    {
        var order = await CreateFactory().CreateAsync(
            Input("Gate code is 1234, the dog is friendly."), CancellationToken.None);

        Assert.Equal("Gate code is 1234, the dog is friendly.", order.SpecialInstructions);
    }

    [Fact]
    public async Task Factory_TrimsSurroundingWhitespace()
    {
        var order = await CreateFactory().CreateAsync(
            Input("  Ring twice.\n"), CancellationToken.None);

        Assert.Equal("Ring twice.", order.SpecialInstructions);
    }

    /// <summary>
    /// Both mobile clients bind a multi-line text field straight to state, so a
    /// user who taps into it and back out sends whitespace, not null. Collapsing
    /// to null here keeps the partner apps from rendering an empty notes card.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t ")]
    public async Task Factory_CollapsesBlankInstructionsToNull(string? blank)
    {
        var order = await CreateFactory().CreateAsync(Input(blank), CancellationToken.None);

        Assert.Null(order.SpecialInstructions);
    }

    /// <summary>
    /// The recurring-bookings materializer builds its own CreateOrderInput and
    /// has no per-occurrence note; the parameter defaults so it stays untouched.
    /// </summary>
    [Fact]
    public async Task Factory_DefaultsToNullWhenTheCallerOmitsTheField()
    {
        var input = new CreateOrderInput(
            UserId: null,
            CustomerName: "Test Customer",
            CustomerEmail: "customer@example.com",
            CustomerPhone: "+420123456789",
            Address: AddressMockFactory.Generate(),
            Rooms: 2,
            Bathrooms: 1,
            Extras: new Dictionary<string, bool>(),
            CleaningDate: DateTime.UtcNow.AddDays(3),
            PaymentType: PaymentType.Cash,
            Currency: Currency.Create("CZK", "Kč", "Czech Koruna", 1m),
            SelectedServiceIds: ["service-1"],
            SelectedPackageIds: [],
            RawSubtotal: 1500m);

        var order = await CreateFactory().CreateAsync(input, CancellationToken.None);

        Assert.Null(order.SpecialInstructions);
    }
}
