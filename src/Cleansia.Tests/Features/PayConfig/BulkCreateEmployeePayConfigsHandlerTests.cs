using Cleansia.Core.AppServices.Features.PayConfig;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Infra.Common.Validations;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.PayConfig;

/// <summary>
/// The admin's one-button pay generator, which had no tests at all while it was the fastest way in the
/// product to pay a cleaner the wrong number.
///
/// <para><b>The defect it closes.</b> The command takes a <c>CurrencyId</c> and stamps every generated
/// row with it, but the amount was derived from the catalogue's own price column — one number, no
/// currency. Generating EUR configs therefore multiplied CZK figures by the grade rate and labelled the
/// result EUR: a cleaner "paid" 400 EUR for a job quoted at 400 CZK, roughly 24x, from a button whose
/// output nobody reads line by line because generating it in bulk is the entire point. The two-column
/// arithmetic was correct throughout, which is why a suite over the arithmetic would have stayed
/// green — the unit was wrong, not the maths.</para>
///
/// <para>Every expected value below is hand-derived from the fixture, never the production expression
/// re-run: 800 x 0.75 = 600, and if the handler starts rounding differently that is a failure rather
/// than a matching mistake on both sides.</para>
/// </summary>
public class BulkCreateEmployeePayConfigsHandlerTests
{
    private const string EmployeeId = "emp-1";
    private const string ServiceId = "svc-1";
    private const string PackageId = "pkg-1";
    private const string Czk = "cur-czk";
    private const string Eur = "cur-eur";

    private readonly Mock<IEmployeePayConfigRepository> _payConfigs = new();
    private readonly Mock<IServiceRepository> _services = new();
    private readonly Mock<IPackageRepository> _packages = new();
    private readonly Mock<IServicePriceRepository> _servicePrices = new();
    private readonly Mock<IPackagePriceRepository> _packagePrices = new();

    private List<EmployeePayConfig> _added = [];

    public BulkCreateEmployeePayConfigsHandlerTests()
    {
        var service = Service.Create("cat-1", "Deep Cleaning", "d");
        service.Id = ServiceId;
        var package = Package.Create("Essential Clean", "d");
        package.Id = PackageId;

        _services.Setup(r => r.GetAll()).Returns(new[] { service }.AsQueryable().BuildMock());
        _packages.Setup(r => r.GetAll()).Returns(new[] { package }.AsQueryable().BuildMock());

        // CZK only, which is the seeded state: the catalogue is priced in the operated currency and in
        // nothing else. EUR exists as a currency and has no prices.
        _servicePrices.Setup(r => r.GetAll()).Returns(new[]
        {
            ServicePrice.Create(ServiceId, Czk, basePrice: 800m, perRoomPrice: 250m)
        }.AsQueryable().BuildMock());
        _packagePrices.Setup(r => r.GetAll()).Returns(new[]
        {
            PackagePrice.Create(PackageId, Czk, price: 1200m)
        }.AsQueryable().BuildMock());

        ArrangeExisting();
        _payConfigs
            .Setup(r => r.AddRange(It.IsAny<IEnumerable<EmployeePayConfig>>()))
            .Callback<IEnumerable<EmployeePayConfig>>(configs => _added = configs.ToList());
    }

    private void ArrangeExisting(params EmployeePayConfig[] existing) =>
        _payConfigs
            .Setup(r => r.GetByEmployeeIdAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

    private BulkCreateEmployeePayConfigs.Handler CreateHandler() => new(
        _payConfigs.Object,
        _services.Object,
        _servicePrices.Object,
        _packagePrices.Object,
        _packages.Object);

    private Task<BusinessResult<BulkCreateEmployeePayConfigs.Response>>
        GenerateAsync(string currencyId, string grade = "medior", bool overwrite = false) =>
        CreateHandler().Handle(
            new BulkCreateEmployeePayConfigs.Command(EmployeeId, grade, currencyId, overwrite),
            CancellationToken.None);

    /// <summary>
    /// Medior is 0.75. Hand-derived: 800 x 0.75 = 600 base, 250 x 0.75 = 187.50 per room, 1200 x 0.75
    /// = 900 for the package.
    /// </summary>
    [Fact]
    public async Task It_Derives_The_Pay_From_The_Price_Row_In_The_Requested_Currency()
    {
        var result = await GenerateAsync(Czk);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.CreatedCount);
        Assert.Equal(0, result.Value.SkippedCount);

        var serviceConfig = Assert.Single(_added, c => c.ServiceId == ServiceId);
        Assert.Equal(600m, serviceConfig.BasePay);
        Assert.Equal(187.50m, serviceConfig.ExtraPerRoom);
        Assert.Equal(Czk, serviceConfig.CurrencyId);

        var packageConfig = Assert.Single(_added, c => c.PackageId == PackageId);
        Assert.Equal(900m, packageConfig.BasePay);
        Assert.Equal(Czk, packageConfig.CurrencyId);
    }

    /// <summary>
    /// THE DEFECT, stated as the outcome rather than as the mechanism. Asking for EUR when the catalogue
    /// carries no EUR price must produce NOTHING — not CZK numbers wearing a EUR label. Written against
    /// the generated rows rather than against the lookup so that any future path back to a
    /// currency-blind price still fails here.
    /// </summary>
    [Fact]
    public async Task It_Generates_Nothing_For_A_Currency_The_Catalogue_Is_Not_Priced_In()
    {
        var result = await GenerateAsync(Eur);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.CreatedCount);
        Assert.Equal(2, result.Value.SkippedCount);
        Assert.Empty(_added);
    }

    /// <summary>
    /// Anti-vacuity for the case above: the EUR run produces nothing because the PRICE is missing, not
    /// because the fixture cannot generate anything at all. Same command, same catalogue, one EUR price
    /// row added — and that entry comes back, at its own authored EUR number rather than a converted
    /// one. 40 x 0.75 = 30, which no scaling of the 800 CZK price can reach.
    /// </summary>
    [Fact]
    public async Task An_Entry_Priced_In_The_Requested_Currency_Is_Generated_At_That_Currencys_Number()
    {
        _servicePrices.Setup(r => r.GetAll()).Returns(new[]
        {
            ServicePrice.Create(ServiceId, Czk, basePrice: 800m, perRoomPrice: 250m),
            ServicePrice.Create(ServiceId, Eur, basePrice: 40m, perRoomPrice: 12m)
        }.AsQueryable().BuildMock());

        var result = await GenerateAsync(Eur);

        Assert.Equal(1, result.Value!.CreatedCount);
        Assert.Equal(1, result.Value.SkippedCount);

        var config = Assert.Single(_added);
        Assert.Equal(ServiceId, config.ServiceId);
        Assert.Equal(30m, config.BasePay);
        Assert.Equal(9m, config.ExtraPerRoom);
        Assert.Equal(Eur, config.CurrencyId);
    }

    /// <summary>
    /// Junior is 0.5, senior 1.0 — the grade scales the price, and the seed's platform-wide row uses the
    /// junior number, so this is the one that has to agree with the seeded data.
    /// </summary>
    [Theory]
    [InlineData("junior", 400, 125)]
    [InlineData("medior", 600, 187.50)]
    [InlineData("senior", 800, 250)]
    public async Task The_Grade_Scales_The_Price(string grade, decimal expectedBase, decimal expectedPerRoom)
    {
        await GenerateAsync(Czk, grade);

        var config = Assert.Single(_added, c => c.ServiceId == ServiceId);
        Assert.Equal(expectedBase, config.BasePay);
        Assert.Equal(expectedPerRoom, config.ExtraPerRoom);
    }

    [Fact]
    public async Task An_Existing_Config_Is_Skipped_Unless_Overwrite_Is_Requested()
    {
        ArrangeExisting(EmployeePayConfig.CreateForService(ServiceId, 1m, Czk, employeeId: EmployeeId));

        var result = await GenerateAsync(Czk);

        Assert.Equal(1, result.Value!.CreatedCount);
        Assert.Equal(1, result.Value.SkippedCount);
        Assert.DoesNotContain(_added, c => c.ServiceId == ServiceId);
        _payConfigs.Verify(r => r.RemoveRange(It.IsAny<IEnumerable<EmployeePayConfig>>()), Times.Never);
    }

    [Fact]
    public async Task Overwrite_Removes_The_Existing_Config_And_Writes_The_Derived_One()
    {
        var stale = EmployeePayConfig.CreateForService(ServiceId, 1m, Czk, employeeId: EmployeeId);
        ArrangeExisting(stale);

        List<EmployeePayConfig> removed = [];
        _payConfigs
            .Setup(r => r.RemoveRange(It.IsAny<IEnumerable<EmployeePayConfig>>()))
            .Callback<IEnumerable<EmployeePayConfig>>(configs => removed = configs.ToList());

        var result = await GenerateAsync(Czk, overwrite: true);

        Assert.Equal(2, result.Value!.CreatedCount);
        Assert.Equal(0, result.Value.SkippedCount);
        Assert.Same(stale, Assert.Single(removed));
        Assert.Equal(600m, Assert.Single(_added, c => c.ServiceId == ServiceId).BasePay);
    }

    // ---------------------------------------------------------------- the currency term

    /// <summary>
    /// A cleaner may hold one rate per currency for the same service — the unique index is
    /// (EmployeeId, ServiceId, PackageId, CurrencyId). Deciding "already exists" without the currency
    /// made a EUR run see the CZK row and skip it, so the second currency could never be generated.
    /// </summary>
    [Fact]
    public async Task An_Existing_Config_In_Another_Currency_Does_Not_Block_This_One()
    {
        _servicePrices.Setup(r => r.GetAll()).Returns(new[]
        {
            ServicePrice.Create(ServiceId, Czk, basePrice: 800m, perRoomPrice: 250m),
            ServicePrice.Create(ServiceId, Eur, basePrice: 32m, perRoomPrice: 10m),
        }.AsQueryable().BuildMock());
        ArrangeExisting(EmployeePayConfig.CreateForService(ServiceId, 1m, Czk, employeeId: EmployeeId));

        var result = await GenerateAsync(Eur);

        var config = Assert.Single(_added, c => c.ServiceId == ServiceId);
        Assert.Equal(Eur, config.CurrencyId);
        Assert.Equal(24m, config.BasePay);
        _payConfigs.Verify(r => r.RemoveRange(It.IsAny<IEnumerable<EmployeePayConfig>>()), Times.Never);
    }

    /// <summary>
    /// THE DESTRUCTIVE ONE, and it needed no market switch to reach: the currency dropdown offers
    /// every currency including the unpriced ones, so an admin could pick EUR, tick overwrite, and
    /// hard-delete a cleaner's hand-tuned CZK rates while the empty EUR price lookup created nothing
    /// to replace them. Two independent faults produced it — a removal set built without the currency
    /// term, and a price guard that ran AFTER the deletion had already been staged — so this asserts
    /// the surviving row's AMOUNT, not merely that nothing was removed.
    /// </summary>
    [Fact]
    public async Task Overwriting_In_A_Currency_The_Catalogue_Has_No_Price_For_Destroys_Nothing()
    {
        var czkRate = EmployeePayConfig.CreateForService(ServiceId, 555m, Czk, employeeId: EmployeeId);
        var czkPackageRate = EmployeePayConfig.CreateForPackage(PackageId, 777m, Czk, employeeId: EmployeeId);
        ArrangeExisting(czkRate, czkPackageRate);

        List<EmployeePayConfig> removed = [];
        _payConfigs
            .Setup(r => r.RemoveRange(It.IsAny<IEnumerable<EmployeePayConfig>>()))
            .Callback<IEnumerable<EmployeePayConfig>>(configs => removed = configs.ToList());

        var result = await GenerateAsync(Eur, overwrite: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.CreatedCount);
        Assert.Empty(removed);
        Assert.Empty(_added);
        Assert.Equal(555m, czkRate.BasePay);
        Assert.Equal(777m, czkPackageRate.BasePay);
    }

    /// <summary>
    /// The same guard from the other side: overwriting the currency that IS priced must still replace
    /// only that currency's row, leaving the other one alone.
    /// </summary>
    [Fact]
    public async Task Overwrite_Replaces_Only_The_Targeted_Currencys_Row()
    {
        _servicePrices.Setup(r => r.GetAll()).Returns(new[]
        {
            ServicePrice.Create(ServiceId, Czk, basePrice: 800m, perRoomPrice: 250m),
            ServicePrice.Create(ServiceId, Eur, basePrice: 32m, perRoomPrice: 10m),
        }.AsQueryable().BuildMock());
        var czkRate = EmployeePayConfig.CreateForService(ServiceId, 555m, Czk, employeeId: EmployeeId);
        var eurRate = EmployeePayConfig.CreateForService(ServiceId, 9m, Eur, employeeId: EmployeeId);
        ArrangeExisting(czkRate, eurRate);

        List<EmployeePayConfig> removed = [];
        _payConfigs
            .Setup(r => r.RemoveRange(It.IsAny<IEnumerable<EmployeePayConfig>>()))
            .Callback<IEnumerable<EmployeePayConfig>>(configs => removed = configs.ToList());

        await GenerateAsync(Eur, overwrite: true);

        Assert.Same(eurRate, Assert.Single(removed));
        Assert.Equal(24m, Assert.Single(_added, c => c.ServiceId == ServiceId).BasePay);
        Assert.Equal(555m, czkRate.BasePay);
    }

    /// <summary>
    /// The case that makes the guard's POSITION load-bearing rather than incidental: same currency
    /// throughout, but the catalogue no longer prices this service in it — a price an admin withdrew,
    /// or a service added after the last pricing pass. Scoping the removal by currency does not help
    /// here, because the row being deleted IS in the requested currency. Only running the price guard
    /// first does: with it below the overwrite branch, the delete is staged and then the loop skips,
    /// so the cleaner's rate is destroyed and nothing is written in its place.
    /// </summary>
    [Fact]
    public async Task Overwriting_A_Service_The_Catalogue_No_Longer_Prices_Leaves_The_Existing_Rate()
    {
        // The package keeps its price; only the service's row is gone, so the run still does work and
        // the assertion is about WHICH row survived rather than about the handler doing nothing.
        _servicePrices.Setup(r => r.GetAll())
            .Returns(Array.Empty<ServicePrice>().AsQueryable().BuildMock());
        var czkRate = EmployeePayConfig.CreateForService(ServiceId, 555m, Czk, employeeId: EmployeeId);
        ArrangeExisting(czkRate);

        List<EmployeePayConfig> removed = [];
        _payConfigs
            .Setup(r => r.RemoveRange(It.IsAny<IEnumerable<EmployeePayConfig>>()))
            .Callback<IEnumerable<EmployeePayConfig>>(configs => removed = configs.ToList());

        var result = await GenerateAsync(Czk, overwrite: true);

        Assert.True(result.IsSuccess);
        Assert.Empty(removed);
        Assert.Equal(555m, czkRate.BasePay);
        // The package half of the same run still lands, so this is not passing by doing nothing.
        Assert.Equal(900m, Assert.Single(_added, c => c.PackageId == PackageId).BasePay);
    }
}
