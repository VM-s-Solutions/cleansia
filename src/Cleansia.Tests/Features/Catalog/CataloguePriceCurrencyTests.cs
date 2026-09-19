using System.Reflection;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Packages;
using Cleansia.Core.AppServices.Features.Packages.DTOs;
using Cleansia.Core.AppServices.Features.Services;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Infra.Common.Validations;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Catalog;

/// <summary>
/// A catalogue entry is priced PER CURRENCY, and the admin form authors those rows directly rather
/// than converting one number at a rate. Two things follow, and this suite pins both.
///
/// <para><b>Coverage is the ACTIVE currencies.</b> An entry with no row in a currency is not
/// offerable in it, so saving one that covers only some of the operated currencies takes it off sale
/// in the rest. An INACTIVE currency may be priced and is not required: on a Currency, IsActive is the
/// market switch rather than a soft-delete flag — the seed carries CZK active and EUR inactive, and
/// EUR gains price rows in the same change that flips it, so pricing ahead of the flip is the intended
/// path rather than an error.</para>
///
/// <para><b>Saving is an upsert, per currency, and never a delete.</b> The form sends back the blocks
/// it was rendered; a code it omits is a currency this entry is not priced in, which the row's absence
/// already says. Wiping a price an admin authored, as a side effect of an unrelated save, is the
/// failure this discriminates against — so the omitted-currency case here asserts the surviving row's
/// AMOUNT, not merely that a row is still there.</para>
/// </summary>
public class CataloguePriceCurrencyTests
{
    private const string ServiceId = "svc-1";
    private const string PackageId = "pkg-1";
    private const string CzkId = "cur-czk";
    private const string EurId = "cur-eur";

    private readonly Currency _czk;
    private readonly Currency _eur;
    private readonly ICurrencyRepository _currencyRepository;

    private readonly Mock<ILanguageRepository> _languageRepository = new();
    private readonly Mock<IServiceCategoryRepository> _categoryRepository = new();
    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<IServicePriceRepository> _servicePriceRepository = new();
    private readonly Mock<IPackagePriceRepository> _packagePriceRepository = new();

    public CataloguePriceCurrencyTests()
    {
        _czk = Currency.Create("CZK", "Kc", "Czech koruna");
        _czk.Id = CzkId;
        _czk.IsActive = true;
        _czk.SetAsDefault(true);
        // As seeded: CZK operated, EUR present but not yet switched on.
        _eur = Currency.Create("EUR", "E", "Euro");
        _eur.Id = EurId;
        _eur.IsActive = false;
        _currencyRepository = CataloguePriceDoubles.Currencies(_czk, _czk, _eur);

        _languageRepository.Setup(r => r.GetAll())
            .Returns(new List<Language> { Language.Create("en", "English") }.AsQueryable().BuildMock());
        _categoryRepository
            .Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _serviceRepository
            .Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _serviceRepository
            .Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _packageRepository
            .Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    // ---------------------------------------------------------------- arrangement

    private static Dictionary<string, CreateService.TranslationInput> Translations() =>
        new() { ["en"] = new CreateService.TranslationInput("Name", "Description") };

    private static Dictionary<string, PackageTranslationInput> PackageTranslations() =>
        new() { ["en"] = new PackageTranslationInput("Name", "Description", null) };

    private CreateService.Validator ServiceValidator() => new(
        _languageRepository.Object, _categoryRepository.Object, _currencyRepository);

    private UpdateService.Validator UpdateServiceValidator() => new(
        _serviceRepository.Object, _languageRepository.Object, _categoryRepository.Object,
        _currencyRepository);

    private CreatePackage.Validator PackageValidator() => new(
        _serviceRepository.Object, _languageRepository.Object, _currencyRepository);

    private UpdatePackage.Validator UpdatePackageValidator() => new(
        _packageRepository.Object, _serviceRepository.Object, _languageRepository.Object,
        _currencyRepository);

    private static CreateService.Command ServiceCommand(
        Dictionary<string, CreateService.ServicePriceInput>? prices) =>
        new("cat-1", "Windows", "Window cleaning", 30, prices, Translations());

    private static UpdateService.Command UpdateServiceCommand(
        Dictionary<string, CreateService.ServicePriceInput>? prices) =>
        new(ServiceId, "cat-1", "Windows", "Window cleaning", 30, prices, Translations());

    private static CreatePackage.Command PackageCommand(Dictionary<string, decimal>? prices) =>
        new("Deep Clean", "Full home deep clean", null, false, prices, null, PackageTranslations());

    private static UpdatePackage.Command UpdatePackageCommand(Dictionary<string, decimal>? prices) =>
        new(PackageId, "Deep Clean", "Full home deep clean", null, false, prices, null, null,
            PackageTranslations());

    private static Dictionary<string, CreateService.ServicePriceInput> Czk(
        decimal basePrice = 100m, decimal perRoom = 10m) =>
        new() { ["CZK"] = new CreateService.ServicePriceInput(basePrice, perRoom) };

    // ---------------------------------------------------------------- the rule

    [Fact]
    public async Task Pricing_Every_Active_Currency_Is_Enough()
    {
        var result = await ServiceValidator().ValidateAsync(ServiceCommand(Czk()));

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// The path EUR takes into service: it is priced while still inactive, and only then switched on.
    /// A rule that required every currency in the table would make this the one thing an admin cannot
    /// do, and there would be no way to give EUR prices before activating it.
    /// </summary>
    [Fact]
    public async Task An_Inactive_Currency_May_Be_Priced_Ahead_Of_Being_Switched_On()
    {
        var prices = Czk();
        prices["EUR"] = new CreateService.ServicePriceInput(4m, 0.4m);

        var result = await ServiceValidator().ValidateAsync(ServiceCommand(prices));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task An_Inactive_Currency_Is_Not_Required()
    {
        var result = await ServiceValidator().ValidateAsync(ServiceCommand(Czk()));

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.MissingPriceForCurrency);
    }

    [Fact]
    public async Task Missing_An_Active_Currency_Is_Refused()
    {
        var prices = new Dictionary<string, CreateService.ServicePriceInput>
        {
            ["EUR"] = new(4m, 0.4m),
        };

        var result = await ServiceValidator().ValidateAsync(ServiceCommand(prices));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.MissingPriceForCurrency);
    }

    /// <summary>
    /// The moment the programme is building towards. Once EUR is switched on, an entry priced only in
    /// CZK is one the EUR market cannot buy, and its next save has to say so rather than quietly
    /// leaving it unbookable — the same shape as adding a language.
    /// </summary>
    [Fact]
    public async Task Switching_A_Currency_On_Makes_Entries_Missing_It_Unsaveable()
    {
        _eur.IsActive = true;

        var result = await ServiceValidator().ValidateAsync(ServiceCommand(Czk()));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.MissingPriceForCurrency);
    }

    [Fact]
    public async Task No_Prices_At_All_Is_Refused()
    {
        var result = await ServiceValidator().ValidateAsync(ServiceCommand(null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.PricesRequired);
    }

    [Fact]
    public async Task An_Empty_Price_Set_Is_Refused()
    {
        var result = await ServiceValidator()
            .ValidateAsync(ServiceCommand(new Dictionary<string, CreateService.ServicePriceInput>()));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.PricesRequired);
    }

    /// <summary>
    /// The handlers key their upsert by code, so a code naming no currency would be skipped and the
    /// admin would get a 200 for a price that never landed. It is refused at the door instead.
    /// </summary>
    [Fact]
    public async Task A_Code_Naming_No_Currency_Is_Refused_Rather_Than_Dropped()
    {
        var prices = Czk();
        prices["XBT"] = new CreateService.ServicePriceInput(1m, 1m);

        var result = await ServiceValidator().ValidateAsync(ServiceCommand(prices));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.CurrencyNotFound);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(100, -1)]
    public async Task A_Negative_Component_Is_Refused(decimal basePrice, decimal perRoom)
    {
        var result = await ServiceValidator().ValidateAsync(ServiceCommand(Czk(basePrice, perRoom)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.MustBePositive);
    }

    /// <summary>Free is a price. Only below zero is refused.</summary>
    [Fact]
    public async Task A_Zero_Price_Is_Allowed()
    {
        var result = await ServiceValidator().ValidateAsync(ServiceCommand(Czk(0m, 0m)));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task UpdateService_Carries_The_Same_Rule()
    {
        var prices = new Dictionary<string, CreateService.ServicePriceInput> { ["EUR"] = new(4m, 0.4m) };

        var result = await UpdateServiceValidator().ValidateAsync(UpdateServiceCommand(prices));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.MissingPriceForCurrency);
    }

    [Fact]
    public async Task CreatePackage_Carries_The_Same_Rule()
    {
        var result = await PackageValidator()
            .ValidateAsync(PackageCommand(new Dictionary<string, decimal> { ["EUR"] = 20m }));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.MissingPriceForCurrency);
    }

    [Fact]
    public async Task UpdatePackage_Carries_The_Same_Rule()
    {
        var result = await UpdatePackageValidator()
            .ValidateAsync(UpdatePackageCommand(new Dictionary<string, decimal> { ["EUR"] = 20m }));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.MissingPriceForCurrency);
    }

    [Fact]
    public async Task A_Package_Priced_In_Every_Active_Currency_Passes()
    {
        var result = await PackageValidator()
            .ValidateAsync(PackageCommand(new Dictionary<string, decimal> { ["CZK"] = 500m }));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task A_Negative_Package_Price_Is_Refused()
    {
        var result = await PackageValidator()
            .ValidateAsync(PackageCommand(new Dictionary<string, decimal> { ["CZK"] = -1m }));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.MustBePositive);
    }

    // ---------------------------------------------------------------- the write

    private async Task<BusinessResult<UpdateService.Response>> InvokeUpdateService(UpdateService.Command command)
    {
        var handlerType = typeof(UpdateService).GetNestedType("Handler", BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(handlerType);
        var handler = Activator.CreateInstance(
            handlerType!,
            _serviceRepository.Object,
            _servicePriceRepository.Object,
            _currencyRepository)!;
        var task = (Task<BusinessResult<UpdateService.Response>>)handlerType!
            .GetMethod("Handle")!
            .Invoke(handler, [command, CancellationToken.None])!;
        return await task;
    }

    private Service ArrangeService()
    {
        var service = Service.Create("cat-1", "Windows", "Window cleaning");
        service.Id = ServiceId;
        _serviceRepository
            .Setup(r => r.GetByIdAsync(ServiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(service);
        return service;
    }

    private void ArrangeExistingPrices(params ServicePrice[] rows) =>
        _servicePriceRepository.Setup(r => r.GetAll()).Returns(rows.AsQueryable().BuildMock());

    [Fact]
    public async Task A_Currency_With_No_Row_Yet_Gets_One()
    {
        ArrangeService();
        ArrangeExistingPrices();
        var added = new List<ServicePrice>();
        _servicePriceRepository.Setup(r => r.Add(It.IsAny<ServicePrice>()))
            .Callback<ServicePrice>(added.Add);

        var prices = Czk(100m, 10m);
        prices["EUR"] = new CreateService.ServicePriceInput(4m, 0.4m);
        var result = await InvokeUpdateService(UpdateServiceCommand(prices));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, added.Count);
        Assert.Contains(added, p => p.CurrencyId == CzkId && p.BasePrice == 100m && p.PerRoomPrice == 10m);
        Assert.Contains(added, p => p.CurrencyId == EurId && p.BasePrice == 4m && p.PerRoomPrice == 0.4m);
    }

    [Fact]
    public async Task A_Currency_That_Already_Has_A_Row_Is_Updated_Rather_Than_Doubled()
    {
        ArrangeService();
        var existing = ServicePrice.Create(ServiceId, CzkId, 100m, 10m);
        ArrangeExistingPrices(existing);

        var result = await InvokeUpdateService(UpdateServiceCommand(Czk(250m, 25m)));

        Assert.True(result.IsSuccess);
        _servicePriceRepository.Verify(r => r.Add(It.IsAny<ServicePrice>()), Times.Never);
        Assert.Equal(250m, existing.BasePrice);
        Assert.Equal(25m, existing.PerRoomPrice);
    }

    /// <summary>
    /// The one that matters: a save carrying only CZK must not touch the EUR row. Asserting the EUR
    /// amount rather than its existence is deliberate — a handler that cleared and rewrote the set
    /// would still leave "a EUR row" behind if the payload happened to mention it.
    /// </summary>
    [Fact]
    public async Task A_Currency_The_Payload_Omits_Keeps_Its_Price()
    {
        ArrangeService();
        var czkRow = ServicePrice.Create(ServiceId, CzkId, 100m, 10m);
        var eurRow = ServicePrice.Create(ServiceId, EurId, 4m, 0.4m);
        ArrangeExistingPrices(czkRow, eurRow);

        var result = await InvokeUpdateService(UpdateServiceCommand(Czk(250m, 25m)));

        Assert.True(result.IsSuccess);
        Assert.Equal(250m, czkRow.BasePrice);
        Assert.Equal(4m, eurRow.BasePrice);
        Assert.Equal(0.4m, eurRow.PerRoomPrice);
        _servicePriceRepository.Verify(r => r.Remove(It.IsAny<ServicePrice>()), Times.Never);
    }
}
