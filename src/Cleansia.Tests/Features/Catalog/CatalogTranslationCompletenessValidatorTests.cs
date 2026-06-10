using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Packages;
using Cleansia.Core.AppServices.Features.Services;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Catalog;

/// <summary>
/// Catalog translations are MANDATORY for every ACTIVE language — no default-language fallback.
/// The completeness set is the ACTIVE languages only, so a deactivated language stops being
/// required; and the rule applies uniformly to services AND packages (the package validators had
/// drifted — translations were entirely unchecked there). Adding a language makes existing items
/// incomplete: their next save is rejected until the new translation is supplied.
/// </summary>
public class CatalogTranslationCompletenessValidatorTests
{
    private readonly Mock<ILanguageRepository> _languageRepository = new();
    private readonly Mock<IServiceCategoryRepository> _categoryRepository = new();
    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<IPackageRepository> _packageRepository = new();

    public CatalogTranslationCompletenessValidatorTests()
    {
        var inactive = Language.Create("de", "German");
        inactive.IsActive = false;
        var languages = new List<Language>
        {
            Language.Create("en", "English"),
            Language.Create("cs", "Czech"),
            inactive,
        };
        _languageRepository.Setup(r => r.GetAll()).Returns(languages.AsQueryable().BuildMock());

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

    private static Dictionary<string, CreateService.TranslationInput> Translations(params string[] codes) =>
        codes.ToDictionary(c => c, c => new CreateService.TranslationInput($"Name {c}", $"Description {c}"));

    private CreateService.Command ServiceCommand(Dictionary<string, CreateService.TranslationInput>? translations) =>
        new("cat-1", "Windows", "Window cleaning", 100m, 10m, 30, translations);

    private UpdateService.Command UpdateServiceCommand(Dictionary<string, CreateService.TranslationInput>? translations) =>
        new("service-1", "cat-1", "Windows", "Window cleaning", 100m, 10m, 30, translations);

    private CreatePackage.Command PackageCommand(Dictionary<string, CreateService.TranslationInput>? translations) =>
        new("Deep Clean", "Full home deep clean", 500m, null, translations);

    private UpdatePackage.Command UpdatePackageCommand(Dictionary<string, CreateService.TranslationInput>? translations) =>
        new("package-1", "Deep Clean", "Full home deep clean", 500m, null, null, translations);

    [Fact]
    public async Task CreateService_CoveringAllActiveLanguages_Passes_DespiteInactiveLanguage()
    {
        var validator = new CreateService.Validator(_languageRepository.Object, _categoryRepository.Object);

        var result = await validator.ValidateAsync(ServiceCommand(Translations("en", "cs")));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task CreateService_MissingAnActiveLanguage_Fails()
    {
        var validator = new CreateService.Validator(_languageRepository.Object, _categoryRepository.Object);

        var result = await validator.ValidateAsync(ServiceCommand(Translations("en")));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.MissingTranslationForLanguage);
    }

    [Fact]
    public async Task UpdateService_CoveringAllActiveLanguages_Passes_DespiteInactiveLanguage()
    {
        var validator = new UpdateService.Validator(
            _serviceRepository.Object, _languageRepository.Object, _categoryRepository.Object);

        var result = await validator.ValidateAsync(UpdateServiceCommand(Translations("en", "cs")));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task CreatePackage_WithoutTranslations_Fails_TranslationsRequired()
    {
        var validator = new CreatePackage.Validator(_serviceRepository.Object, _languageRepository.Object);

        var result = await validator.ValidateAsync(PackageCommand(null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.TranslationsRequired);
    }

    [Fact]
    public async Task CreatePackage_MissingAnActiveLanguage_Fails()
    {
        var validator = new CreatePackage.Validator(_serviceRepository.Object, _languageRepository.Object);

        var result = await validator.ValidateAsync(PackageCommand(Translations("en")));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.MissingTranslationForLanguage);
    }

    [Fact]
    public async Task CreatePackage_CoveringAllActiveLanguages_Passes()
    {
        var validator = new CreatePackage.Validator(_serviceRepository.Object, _languageRepository.Object);

        var result = await validator.ValidateAsync(PackageCommand(Translations("en", "cs")));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task UpdatePackage_WithoutTranslations_Fails_TranslationsRequired()
    {
        var validator = new UpdatePackage.Validator(
            _packageRepository.Object, _serviceRepository.Object, _languageRepository.Object);

        var result = await validator.ValidateAsync(UpdatePackageCommand(null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.TranslationsRequired);
    }

    [Fact]
    public async Task UpdatePackage_CoveringAllActiveLanguages_Passes()
    {
        var validator = new UpdatePackage.Validator(
            _packageRepository.Object, _serviceRepository.Object, _languageRepository.Object);

        var result = await validator.ValidateAsync(UpdatePackageCommand(Translations("en", "cs")));

        Assert.True(result.IsValid);
    }
}
