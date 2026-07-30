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
/// <c>Order.AccessInstructions</c> shipped as a column that every partner/admin
/// surface already renders, but which nothing ever wrote — the entry text the
/// customer typed had nowhere to go. These pin the write path end-to-end —
/// <c>CreateOrder.Command</c> → <c>CreateOrderInput</c> →
/// <c>Order.AccessInstructions</c> — so it cannot silently go dark again.
/// </summary>
public class OrderAccessInstructionsTests
{
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<ICompanyInfoRepository> _companyInfoRepository = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();
    private readonly Mock<IVatCalculator> _vatCalculator = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<IUserMembershipRepository> _userMembershipRepository = new();

    public OrderAccessInstructionsTests()
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
    /// lookups — this suite is about the entry text, not the discount math.
    /// </summary>
    private static CreateOrderInput Input(string? accessInstructions) =>
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
            AccessInstructions: accessInstructions);

    [Fact]
    public async Task Factory_PersistsAccessInstructionsOnTheOrder()
    {
        var order = await CreateFactory().CreateAsync(
            Input("Key is in the lockbox, code 4455."), CancellationToken.None);

        Assert.Equal("Key is in the lockbox, code 4455.", order.AccessInstructions);
    }

    [Fact]
    public async Task Factory_TrimsSurroundingWhitespace()
    {
        var order = await CreateFactory().CreateAsync(
            Input("  Side gate, not the front door.\n"), CancellationToken.None);

        Assert.Equal("Side gate, not the front door.", order.AccessInstructions);
    }

    /// <summary>
    /// The booking forms bind a multi-line text field straight to state, so a
    /// user who taps into it and back out sends whitespace, not null. Collapsing
    /// to null here keeps the partner apps' access card from rendering blank —
    /// they gate it on <c>isNullOrBlank</c>, but the customer surfaces don't.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t ")]
    public async Task Factory_CollapsesBlankInstructionsToNull(string? blank)
    {
        var order = await CreateFactory().CreateAsync(Input(blank), CancellationToken.None);

        Assert.Null(order.AccessInstructions);
    }

    /// <summary>
    /// The recurring-bookings materializer builds its own CreateOrderInput and
    /// has no per-occurrence entry text; the parameter defaults so it stays
    /// untouched — as does every pre-existing caller of <c>Order.Create</c>.
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

        Assert.Null(order.AccessInstructions);
    }

    /// <summary>
    /// The two free-text fields travel side by side and are gated differently on
    /// the partner apps, so a swap would be invisible to every other test here.
    /// </summary>
    [Fact]
    public async Task Factory_KeepsAccessAndSpecialInstructionsDistinct()
    {
        var input = Input("Lockbox code 4455.") with { SpecialInstructions = "The dog is friendly." };

        var order = await CreateFactory().CreateAsync(input, CancellationToken.None);

        Assert.Equal("Lockbox code 4455.", order.AccessInstructions);
        Assert.Equal("The dog is friendly.", order.SpecialInstructions);
    }
}
