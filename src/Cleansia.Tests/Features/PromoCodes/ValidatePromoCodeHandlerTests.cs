using Cleansia.Core.AppServices.Features.PromoCodes;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.PromoCodes;

/// <summary>
/// The checkout preview must ask the question the create asks. CreateOrder previews the code in the
/// order's currency -- the service address's country's -- so a preview against the platform default
/// would call a code valid that checkout refuses, and refuse one that checkout honours. The Command
/// carries the quote's currency; null is a client that quoted with none, which resolved to the
/// default. Asserted on the ARGUMENT the service receives, not on the response.
/// </summary>
public class ValidatePromoCodeHandlerTests
{
    private const string UserId = "user-1";
    private const string Code = "SAVE10";
    private const string Czk = "czk";
    private const string Eur = "eur";

    private readonly Mock<IPromoCodeService> _promoCodeService = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public ValidatePromoCodeHandlerTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = Czk;
        czk.SetAsDefault(true);
        _currencyRepository.Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(czk);
    }

    private ValidatePromoCode.Handler CreateHandler() =>
        new(_promoCodeService.Object, _currencyRepository.Object, _session.Object);

    private void ArrangePreview(string? currencyId, PromoCodePreviewResult result) =>
        _promoCodeService
            .Setup(s => s.PreviewAsync(Code, UserId, 2000m, currencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    [Fact]
    public async Task A_Named_Currency_Reaches_The_Preview_Verbatim()
    {
        ArrangePreview(Eur, new PromoCodePreviewResult(true, 200m, "promo-1", null));

        var result = await CreateHandler().Handle(new ValidatePromoCode.Command(Code, 2000m, Eur), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsValid);
        Assert.Equal(200m, result.Value.DiscountAmount);
        _promoCodeService.Verify(s => s.PreviewAsync(Code, UserId, 2000m, Eur, It.IsAny<CancellationToken>()), Times.Once);
        _currencyRepository.Verify(r => r.GetDefaultAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task No_Currency_Named_Resolves_The_Platform_Default()
    {
        ArrangePreview(Czk, new PromoCodePreviewResult(true, 200m, "promo-1", null));

        var result = await CreateHandler().Handle(new ValidatePromoCode.Command(Code, 2000m), CancellationToken.None);

        Assert.True(result.Value!.IsValid);
        _promoCodeService.Verify(s => s.PreviewAsync(Code, UserId, 2000m, Czk, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task An_Empty_Currency_Is_No_Currency()
    {
        ArrangePreview(Czk, new PromoCodePreviewResult(true, 200m, "promo-1", null));

        var result = await CreateHandler().Handle(new ValidatePromoCode.Command(Code, 2000m, string.Empty), CancellationToken.None);

        Assert.True(result.Value!.IsValid);
        _promoCodeService.Verify(s => s.PreviewAsync(Code, UserId, 2000m, Czk, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// A refusal stays a 200 with the enum name in the Response: the three clients map that name onto
    /// their own copy, and CreateOrder is where the same refusal becomes a 400 under a promo.* key.
    /// </summary>
    [Fact]
    public async Task A_Currency_Mismatch_Is_Echoed_As_The_Enum_Name()
    {
        ArrangePreview(Eur, new PromoCodePreviewResult(false, 0m, null, PromoCodeError.CurrencyMismatch));

        var result = await CreateHandler().Handle(new ValidatePromoCode.Command(Code, 2000m, Eur), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsValid);
        Assert.Null(result.Value.DiscountAmount);
        Assert.Equal("CurrencyMismatch", result.Value.ErrorCode);
    }
}
