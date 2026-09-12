using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The two collaborators that turn a booking's country into its currency, for the suites that build
/// an order validator or handler by hand. A booking is priced in the currency of the country its
/// service address is in, so every such suite now has to say which country the command's address
/// resolves to and which currency that country trades in — the same two answers the production
/// <c>OrderAddressResolver</c> and <c>CurrencyResolutionService</c> give from the database.
/// </summary>
internal static class OrderMarketDoubles
{
    /// <summary>Every country, and no country, resolves to <paramref name="currency"/>.</summary>
    public static ICurrencyResolutionService Trading(Currency currency) => Trading(currency, []);

    /// <summary>
    /// The named countries trade in their own currency; every other country, and no country, falls to
    /// <paramref name="fallback"/> — the platform default, as the production chain falls.
    /// </summary>
    public static ICurrencyResolutionService Trading(
        Currency fallback, params (string CountryId, Currency Currency)[] markets)
    {
        var mock = new Mock<ICurrencyResolutionService>();
        mock.Setup(s => s.ResolveCurrencyForCountryAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string? countryId, CancellationToken _) =>
                markets.FirstOrDefault(m => m.CountryId == countryId).Currency ?? fallback);
        return mock.Object;
    }

    /// <summary>The command's address resolves to <paramref name="countryId"/>, whatever it says.</summary>
    public static IOrderAddressResolver AddressIn(string? countryId)
    {
        var mock = new Mock<IOrderAddressResolver>();
        mock.Setup(r => r.ResolveCountryIdAsync(
                It.IsAny<CreateOrder.Command>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(countryId);
        return mock.Object;
    }

    /// <summary>The command's inline address country, as the production resolver reads it.</summary>
    public static IOrderAddressResolver AddressAsGiven()
    {
        var mock = new Mock<IOrderAddressResolver>();
        mock.Setup(r => r.ResolveCountryIdAsync(
                It.IsAny<CreateOrder.Command>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateOrder.Command command, string? _, CancellationToken _) =>
                command.CustomerAddress?.CountryId);
        return mock.Object;
    }

    /// <summary>A country repository servicing exactly the given ids.</summary>
    public static ICountryRepository Servicing(params string[] countryIds)
    {
        var mock = new Mock<ICountryRepository>();
        mock.Setup(r => r.IsServicedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => countryIds.Contains(id));
        return mock.Object;
    }
}
