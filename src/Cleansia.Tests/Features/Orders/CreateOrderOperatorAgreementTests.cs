using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Tests.Features.PayConfig;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0061 D6 — tenant and currency are two reads of one country. The order's currency is the
/// service address's country's; its tenant is the ambient one (the claim, or for a guest the operator
/// the scope behaviour resolved). This rule refuses the booking when the address's country is
/// operated by another company than the ambient tenant, for guests and customers alike (CH-3), and it
/// keys on the OPERATOR, not the country: one company serving two countries books either.
/// </summary>
public sealed class CreateOrderOperatorAgreementTests
{
    private const string Czechia = "cz";
    private const string Slovakia = "sk";

    private readonly Mock<IOperatorTenantResolver> _operators = new();
    private readonly Mock<ITenantProvider> _tenant = new();
    private readonly Mock<IOrderAddressResolver> _addresses = new();

    private static readonly Currency Czk = CreateOrderTestData.DefaultCurrency();

    public CreateOrderOperatorAgreementTests()
    {
        _addresses
            .Setup(r => r.ResolveCountryIdAsync(It.IsAny<CreateOrder.Command>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateOrder.Command command, string? _, CancellationToken _) => command.CustomerAddress?.CountryId);
    }

    private void OperatedBy(string countryId, string? operatorTenantId) =>
        _operators
            .Setup(r => r.ResolveAsync(countryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperatorResolution(operatorTenantId is not null, operatorTenantId));

    private void AmbientTenant(string? tenantId) => _tenant.Setup(t => t.GetCurrentTenantId()).Returns(tenantId);

    private CreateOrder.Validator Validator()
    {
        var services = new Mock<IServiceRepository>();
        services.Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        services.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>())).Returns(Array.Empty<Service>().AsQueryable().BuildMock());
        var packages = new Mock<IPackageRepository>();
        packages.Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        packages.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>())).Returns(Array.Empty<Package>().AsQueryable().BuildMock());
        var currencies = new Mock<ICurrencyRepository>();
        currencies.Setup(r => r.IsOfferableAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var calculator = new Mock<IOrderPricingCalculator>();
        calculator
            .Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderTestData.MatchingPricing());
        var servicePrices = new Mock<IServicePriceRepository>();
        servicePrices.Setup(r => r.GetAll()).Returns(new[] { ServicePrice.Create(CreateOrderTestData.ServiceId, Czk.Id, 500m, 100m) }.AsQueryable().BuildMock());
        var packagePrices = new Mock<IPackagePriceRepository>();
        packagePrices.Setup(r => r.GetAll()).Returns(new[] { PackagePrice.Create(CreateOrderTestData.PackageId, Czk.Id, 1000m) }.AsQueryable().BuildMock());

        return new CreateOrder.Validator(
            packages.Object,
            services.Object,
            calculator.Object,
            Mock.Of<IOrderRepository>(),
            Mock.Of<IUserMembershipRepository>(),
            Mock.Of<IUserSessionProvider>(),
            PayConfigRepositoryDouble.Holding(),
            currencies.Object,
            _addresses.Object,
            OrderMarketDoubles.Trading(Czk),
            servicePrices.Object,
            packagePrices.Object,
            Mock.Of<IPromoCodeService>(),
            _operators.Object,
            _tenant.Object);
    }

    private static CreateOrder.Command AddressIn(string countryId) =>
        CreateOrderTestData.ValidCommand(customerAddress: CreateOrderTestData.InlineAddress(countryId));

    [Fact]
    public async Task A_Customer_Of_One_Operator_Booking_A_Country_Another_Operates_Is_Refused_On_The_Address()
    {
        AmbientTenant("cleansia-cz");
        OperatedBy(Slovakia, "cleansia-sk");

        var result = await Validator().ValidateAsync(AddressIn(Slovakia));

        var failure = Assert.Single(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OrderCountryOperatorMismatch);
        Assert.Equal(nameof(CreateOrder.Command.CustomerAddress), failure.ErrorCode);
    }

    [Fact]
    public async Task One_Operator_Serving_Two_Countries_Books_Either()
    {
        AmbientTenant("cleansia-cz");
        OperatedBy(Slovakia, "cleansia-cz");

        var result = await Validator().ValidateAsync(AddressIn(Slovakia));

        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OrderCountryOperatorMismatch);
    }

    [Fact]
    public async Task A_Guest_Is_Held_To_The_Same_Rule_As_A_Customer()
    {
        // The scope behaviour set the default operator from a request that named no market, while the
        // address resolves to a country another company operates.
        AmbientTenant("cleansia-cz");
        OperatedBy(Slovakia, "cleansia-sk");

        var result = await Validator().ValidateAsync(AddressIn(Slovakia));

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OrderCountryOperatorMismatch);
    }

    [Fact]
    public async Task A_Country_The_Command_Does_Not_Determine_Is_Left_To_The_Handlers_Own_Refusal()
    {
        AmbientTenant("cleansia-cz");
        _addresses
            .Setup(r => r.ResolveCountryIdAsync(It.IsAny<CreateOrder.Command>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await Validator().ValidateAsync(AddressIn("xx"));

        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OrderCountryOperatorMismatch);
        _operators.Verify(r => r.ResolveAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The currency rule and the operator rule judge the SAME country: the address is resolved once per
    /// validation and cached beside the currency, so the two cannot drift apart between two reads.
    /// </summary>
    [Fact]
    public async Task The_Address_Country_Is_Resolved_Once_For_Both_The_Currency_And_The_Operator_Rule()
    {
        AmbientTenant("cleansia-cz");
        OperatedBy(Czechia, "cleansia-cz");

        var result = await Validator().ValidateAsync(AddressIn(Czechia));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
        _addresses.Verify(
            r => r.ResolveCountryIdAsync(It.IsAny<CreateOrder.Command>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _operators.Verify(r => r.ResolveAsync(Czechia, It.IsAny<CancellationToken>()), Times.Once);
    }
}
