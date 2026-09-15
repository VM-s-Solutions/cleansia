using System.Reflection;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Extras;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Extras;

/// <summary>
/// An extra is priced PER CURRENCY like a service or a package (D4), and the admin commands carry the
/// same two rules the siblings do: coverage of every ACTIVE currency at a non-negative amount, and an
/// upsert per currency that never deletes a row the payload omits. See CataloguePriceCurrencyTests for
/// the reasoning behind both; this suite pins that the third copy did not drift (T-0698).
/// </summary>
public class ExtraPriceCurrencyTests
{
    private const string ExtraId = "ext-1";
    private const string CzkId = "cur-czk";
    private const string EurId = "cur-eur";

    private readonly Currency _czk;
    private readonly Currency _eur;
    private readonly ICurrencyRepository _currencyRepository;

    private readonly Mock<ILanguageRepository> _languageRepository = new();
    private readonly Mock<IExtraRepository> _extraRepository = new();
    private readonly Mock<IExtraPriceRepository> _extraPriceRepository = new();

    public ExtraPriceCurrencyTests()
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
        _extraRepository.Setup(r => r.GetAll()).Returns(Array.Empty<Extra>().AsQueryable().BuildMock());
        _extraRepository
            .Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    // ---------------------------------------------------------------- the rules

    [Fact]
    public async Task CreateExtra_Missing_An_Active_Currency_Is_Refused()
    {
        var command = Create(new Dictionary<string, decimal> { ["EUR"] = 4m });

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.MissingPriceForCurrency);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    public async Task CreateExtra_A_Negative_Price_Is_Refused_And_Zero_Is_Allowed(int price, bool valid)
    {
        var command = Create(new Dictionary<string, decimal> { ["CZK"] = price });

        var result = await CreateValidator().ValidateAsync(command);

        Assert.Equal(valid, result.IsValid);
        Assert.Equal(!valid, result.Errors.Any(e => e.ErrorMessage == BusinessErrorMessage.MustBePositive));
    }

    [Fact]
    public async Task UpdateExtra_Carries_The_Same_Rule()
    {
        var command = Update(new Dictionary<string, decimal> { ["EUR"] = 4m });

        var result = await UpdateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.MissingPriceForCurrency);
    }

    // ---------------------------------------------------------------- the write

    [Fact]
    public async Task UpdateExtra_A_Currency_The_Payload_Omits_Keeps_Its_Price()
    {
        ArrangeExtra();
        var czkRow = ExtraPrice.Create(ExtraId, CzkId, 200m);
        var eurRow = ExtraPrice.Create(ExtraId, EurId, 8m);
        _extraPriceRepository.Setup(r => r.GetAll()).Returns(new[] { czkRow, eurRow }.AsQueryable().BuildMock());

        var result = await InvokeUpdate(Update(new Dictionary<string, decimal> { ["CZK"] = 250m }));

        Assert.True(result.IsSuccess);
        Assert.Equal(250m, czkRow.Price);
        Assert.Equal(8m, eurRow.Price);
        _extraPriceRepository.Verify(r => r.Remove(It.IsAny<ExtraPrice>()), Times.Never);
    }

    [Fact]
    public async Task CreateExtra_Writes_One_Row_Per_Currency_Sent_Including_An_Inactive_One()
    {
        var added = new List<ExtraPrice>();
        _extraPriceRepository.Setup(r => r.GetAll()).Returns(Array.Empty<ExtraPrice>().AsQueryable().BuildMock());
        _extraPriceRepository.Setup(r => r.Add(It.IsAny<ExtraPrice>())).Callback<ExtraPrice>(added.Add);
        _extraRepository.Setup(r => r.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await InvokeCreate(Create(new Dictionary<string, decimal> { ["CZK"] = 200m, ["EUR"] = 8m }));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, added.Count);
        Assert.Contains(added, p => p.CurrencyId == CzkId && p.Price == 200m);
        Assert.Contains(added, p => p.CurrencyId == EurId && p.Price == 8m);
    }

    // ---------------------------------------------------------------- arrangement

    private static Dictionary<string, CreateExtra.TranslationInput> Translations() =>
        new() { ["en"] = new CreateExtra.TranslationInput("Inside oven", "Degrease the oven") };

    private static CreateExtra.Command Create(Dictionary<string, decimal> prices) =>
        new("inside-oven", "Inside oven", null, 10, prices, Translations());

    private static UpdateExtra.Command Update(Dictionary<string, decimal> prices) =>
        new(ExtraId, "Inside oven", null, 10, prices, Translations());

    private CreateExtra.Validator CreateValidator() =>
        new(_extraRepository.Object, _languageRepository.Object, _currencyRepository);

    private UpdateExtra.Validator UpdateValidator() =>
        new(_extraRepository.Object, _languageRepository.Object, _currencyRepository);

    private Extra ArrangeExtra()
    {
        var extra = Extra.Create("inside-oven", "Inside oven", null, 10);
        extra.Id = ExtraId;
        _extraRepository
            .Setup(r => r.GetByIdAsync(ExtraId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(extra);
        return extra;
    }

    private Task<BusinessResult<UpdateExtra.Response>> InvokeUpdate(UpdateExtra.Command command) =>
        Invoke<UpdateExtra, UpdateExtra.Command, UpdateExtra.Response>(command);

    private Task<BusinessResult<CreateExtra.Response>> InvokeCreate(CreateExtra.Command command) =>
        Invoke<CreateExtra, CreateExtra.Command, CreateExtra.Response>(command);

    // The handlers are internal; build them by reflection, as CataloguePriceCurrencyTests does.
    private async Task<BusinessResult<TResponse>> Invoke<TFeature, TCommand, TResponse>(TCommand command)
    {
        var handlerType = typeof(TFeature).GetNestedType("Handler", BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(handlerType);
        var handler = Activator.CreateInstance(
            handlerType!, _extraRepository.Object, _extraPriceRepository.Object, _currencyRepository)!;
        var task = (Task<BusinessResult<TResponse>>)handlerType!
            .GetMethod("Handle")!
            .Invoke(handler, [command!, CancellationToken.None])!;
        return await task;
    }
}
