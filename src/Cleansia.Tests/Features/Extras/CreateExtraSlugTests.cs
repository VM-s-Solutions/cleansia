using System.Reflection;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Extras;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Microsoft.EntityFrameworkCore;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Extras;

/// <summary>
/// The slug is the one identifier of an extra that crosses the wire: five client-side icon maps key by
/// the literal and OrderExtra snapshots it at purchase. So it is fixed at creation, spelled in one
/// alphabet (lower-case words joined by single hyphens), unique -- and, because the validator's
/// uniqueness check crosses a snapshot boundary with no lock, the unique index is the arbiter for two
/// simultaneous creations and its 23505 is answered as slug_already_exists rather than a 500 (T-0698).
/// </summary>
public class CreateExtraSlugTests
{
    private readonly Mock<IExtraRepository> _extraRepository = new();
    private readonly Mock<IExtraPriceRepository> _extraPriceRepository = new();
    private readonly Mock<ILanguageRepository> _languageRepository = new();
    private readonly ICurrencyRepository _currencyRepository;

    public CreateExtraSlugTests()
    {
        var czk = Currency.Create("CZK", "Kc", "Czech koruna");
        czk.Id = "cur-czk";
        czk.IsActive = true;
        czk.SetAsDefault(true);
        _currencyRepository = CataloguePriceDoubles.DefaultCurrency(czk);
        _languageRepository.Setup(r => r.GetAll())
            .Returns(new List<Language> { Language.Create("en", "English") }.AsQueryable().BuildMock());
        _extraRepository.Setup(r => r.GetAll()).Returns(Array.Empty<Extra>().AsQueryable().BuildMock());
        _extraPriceRepository.Setup(r => r.GetAll()).Returns(Array.Empty<ExtraPrice>().AsQueryable().BuildMock());
    }

    [Fact]
    public async Task A_Slug_Already_In_The_Catalogue_Is_Refused()
    {
        _extraRepository.Setup(r => r.GetAll())
            .Returns(new[] { Extra.Create("inside-oven", "Inside oven", null) }.AsQueryable().BuildMock());

        var result = await Validator().ValidateAsync(Command("inside-oven"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.ExtraSlugAlreadyExists);
    }

    [Theory]
    [InlineData("Inside Oven", false)]
    [InlineData("inside_oven", false)]
    [InlineData("-inside-oven", false)]
    [InlineData("inside--oven", false)]
    [InlineData("inside-oven-", false)]
    [InlineData("pet-hair-supplement", true)]
    public async Task A_Slug_Outside_The_Kebab_Alphabet_Is_Refused(string slug, bool valid)
    {
        var result = await Validator().ValidateAsync(Command(slug));

        Assert.Equal(valid, result.IsValid);
        Assert.Equal(!valid, result.Errors.Any(e => e.ErrorMessage == BusinessErrorMessage.ExtraSlugInvalid));
    }

    [Fact]
    public async Task A_Race_Losing_Insert_Maps_The_Unique_Violation_To_SlugAlreadyExists()
    {
        _extraRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("dup", new FakePostgresException("23505")));

        var result = await InvokeCreate(Command("inside-oven"));

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.ExtraSlugAlreadyExists, result.Error!.Message);
    }

    // ---------------------------------------------------------------- arrangement

    private static CreateExtra.Command Command(string slug) =>
        new(slug, "Inside oven", null, 10,
            new Dictionary<string, decimal> { ["CZK"] = 200m },
            new Dictionary<string, CreateExtra.TranslationInput> { ["en"] = new("Inside oven", null) });

    private CreateExtra.Validator Validator() =>
        new(_extraRepository.Object, _languageRepository.Object, _currencyRepository);

    private async Task<BusinessResult<CreateExtra.Response>> InvokeCreate(CreateExtra.Command command)
    {
        var handlerType = typeof(CreateExtra).GetNestedType("Handler", BindingFlags.NonPublic | BindingFlags.Public)!;
        var handler = Activator.CreateInstance(
            handlerType, _extraRepository.Object, _extraPriceRepository.Object, _currencyRepository)!;
        return await (Task<BusinessResult<CreateExtra.Response>>)handlerType
            .GetMethod("Handle")!
            .Invoke(handler, [command, CancellationToken.None])!;
    }

    private sealed class FakePostgresException(string sqlState) : Exception
    {
        public string SqlState { get; } = sqlState;
    }
}
