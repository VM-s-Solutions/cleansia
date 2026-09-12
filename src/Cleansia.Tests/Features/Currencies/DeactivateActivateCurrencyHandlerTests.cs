using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Currencies;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Currencies;

/// <summary>
/// THE MARKET SWITCH, which until now had no writer: nothing in the platform could set a currency's
/// <c>IsActive</c> to true, and every currency an admin created arrived active with no prices — so
/// the seeded EUR could only be switched on by SQL, and a freshly created one could be starred into an
/// empty catalogue.
///
/// <para>Contract, mirrored from the service precedent: deactivating an IN-USE currency is allowed
/// (the switch only stops NEW sales; orders, pay, invoices and credit already denominated in it are
/// untouched), deactivating the DEFAULT is refused (the default is what every quote falls back to,
/// and an inactive default is the empty-catalogue state the switch exists to prevent), and both
/// directions are idempotent.</para>
/// </summary>
public class DeactivateActivateCurrencyHandlerTests
{
    private const string CurrencyId = "currency-eur";
    private const string ActorId = "admin-1";

    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<IUserSessionProvider> _userSessionProvider = new();

    private Currency ArrangeCurrency(bool isActive, bool isDefault = false)
    {
        var currency = Currency.Create("EUR", "€", "Euro");
        currency.Id = CurrencyId;
        currency.IsActive = isActive;
        currency.SetAsDefault(isDefault);

        _currencyRepository
            .Setup(r => r.GetByIdAsync(CurrencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);
        _currencyRepository
            .Setup(r => r.ExistsAsync(CurrencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _userSessionProvider.Setup(s => s.GetUserId()).Returns(ActorId);
        return currency;
    }

    private DeactivateCurrency.Handler Deactivate() =>
        new(_currencyRepository.Object, _userSessionProvider.Object);

    private ActivateCurrency.Handler Activate() => new(_currencyRepository.Object);

    // ---------------------------------------------------------------- born switched off

    /// <summary>
    /// The entity-level half of the fix. A currency an admin has just created has no prices, so it is
    /// not operated until someone says so; <c>ActivateCurrency</c> is the only writer of true.
    /// </summary>
    [Fact]
    public void A_New_Currency_Is_Born_Switched_Off()
    {
        var currency = Currency.Create("HUF", "Ft", "Forint");

        Assert.False(currency.IsActive);
    }

    // ---------------------------------------------------------------- deactivate

    [Fact]
    public async Task Deactivate_Switches_Off_And_Records_The_Actor()
    {
        var currency = ArrangeCurrency(isActive: true);

        var result = await Deactivate().Handle(new DeactivateCurrency.Command(CurrencyId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(currency.IsActive);
        Assert.Equal(ActorId, currency.DeactivatedBy);
        Assert.NotNull(currency.DeactivatedOn);
    }

    [Fact]
    public async Task Deactivate_Refuses_The_Default_Currency()
    {
        var currency = ArrangeCurrency(isActive: true, isDefault: true);

        var result = await Deactivate().Handle(new DeactivateCurrency.Command(CurrencyId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.CannotDeactivateDefaultCurrency, result.Error!.Message);
        Assert.True(currency.IsActive);
    }

    [Fact]
    public async Task Deactivate_Refuses_The_Default_At_The_Validator_Too()
    {
        ArrangeCurrency(isActive: true, isDefault: true);

        var result = await new DeactivateCurrency.Validator(_currencyRepository.Object)
            .ValidateAsync(new DeactivateCurrency.Command(CurrencyId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.CannotDeactivateDefaultCurrency);
    }

    /// <summary>In use is not a reason: the switch stops new sales and touches nothing denominated already.</summary>
    [Fact]
    public async Task Deactivate_Never_Consults_In_Use()
    {
        ArrangeCurrency(isActive: true);

        await Deactivate().Handle(new DeactivateCurrency.Command(CurrencyId), CancellationToken.None);

        _currencyRepository.Verify(
            r => r.IsInUseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Deactivate_Is_Idempotent_And_Keeps_The_Original_Actor()
    {
        var currency = ArrangeCurrency(isActive: false);
        currency.Deactivated("earlier-admin", DateTimeOffset.UtcNow.AddDays(-1));

        var result = await Deactivate().Handle(new DeactivateCurrency.Command(CurrencyId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("earlier-admin", currency.DeactivatedBy);
    }

    // ---------------------------------------------------------------- activate

    [Fact]
    public async Task Activate_Switches_On_And_Keeps_The_Deactivation_Trail()
    {
        var currency = ArrangeCurrency(isActive: false);
        currency.Deactivated("earlier-admin", DateTimeOffset.UtcNow.AddDays(-1));

        var result = await Activate().Handle(new ActivateCurrency.Command(CurrencyId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(currency.IsActive);
        Assert.Equal("earlier-admin", currency.DeactivatedBy);
    }

    /// <summary>
    /// Deliberately NOT gated on prices. An active, unpriced currency is simply not offerable, and the
    /// catalogue price rule is the forcing function that gets it priced from the next save on.
    /// </summary>
    [Fact]
    public async Task Activate_Does_Not_Require_Prices()
    {
        var currency = ArrangeCurrency(isActive: false);

        var result = await Activate().Handle(new ActivateCurrency.Command(CurrencyId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(currency.IsActive);
        _currencyRepository.Verify(
            r => r.IsOfferableAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Activate_Is_Idempotent()
    {
        var currency = ArrangeCurrency(isActive: true);

        var result = await Activate().Handle(new ActivateCurrency.Command(CurrencyId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(currency.IsActive);
    }

    [Fact]
    public async Task Either_Direction_On_An_Unknown_Currency_Fails_With_Not_Found()
    {
        _currencyRepository
            .Setup(r => r.GetByIdAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Currency?)null);

        var off = await Deactivate().Handle(new DeactivateCurrency.Command("missing"), CancellationToken.None);
        var on = await Activate().Handle(new ActivateCurrency.Command("missing"), CancellationToken.None);

        Assert.Equal(BusinessErrorMessage.CurrencyNotFound, off.Error!.Message);
        Assert.Equal(BusinessErrorMessage.CurrencyNotFound, on.Error!.Message);
    }
}
