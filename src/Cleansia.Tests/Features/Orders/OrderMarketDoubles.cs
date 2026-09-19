using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Tenancy;
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
    public static ITenantProvider TenantAt(string tenantId)
    {
        var mock = new Mock<ITenantProvider>();
        mock.Setup(t => t.GetCurrentTenantId()).Returns(tenantId);
        return mock.Object;
    }
    /// <summary>
    /// A single-market platform: every country, and no country, resolves to <paramref name="currency"/>,
    /// and every cleaner is paid in it.
    /// </summary>
    public static ICurrencyResolutionService Trading(Currency currency)
    {
        var mock = new Mock<ICurrencyResolutionService>();
        mock.Setup(s => s.ResolveCurrencyForCountryAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);
        mock.Setup(s => s.ResolveCurrencyForEmployeeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);
        mock.Setup(s => s.ResolveCurrencyForServingEmployeeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);
        return mock.Object;
    }

    /// <summary>
    /// The named countries trade in their own currency; no country is <paramref name="platformDefault"/>,
    /// the customer wizard before an address is known. Any OTHER named country throws, as the production
    /// resolver does for a country it cannot resolve (owner ruling 2026-09-12) -- a suite has to say which
    /// market every address it books into trades in. Every cleaner is paid in the platform default's
    /// market, so a suite that names a preferred cleaner keeps its single-market meaning.
    /// </summary>
    public static ICurrencyResolutionService Trading(
        Currency platformDefault, params (string CountryId, Currency Currency)[] markets)
        => TradingAndPaying(platformDefault, markets);

    /// <summary>
    /// <see cref="Trading(Currency, ValueTuple{string, Currency}[])"/>, and the named cleaners are paid in
    /// their own currency — the shape of a cleaner working in another market than the booking's.
    /// </summary>
    public static ICurrencyResolutionService TradingAndPaying(
        Currency platformDefault,
        (string CountryId, Currency Currency)[] markets,
        params (string EmployeeId, Currency Currency)[] cleaners)
    {
        var mock = new Mock<ICurrencyResolutionService>();
        mock.Setup(s => s.ResolveCurrencyForCountryAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string? countryId, CancellationToken _) =>
                countryId is null
                    ? platformDefault
                    : markets.FirstOrDefault(m => m.CountryId == countryId).Currency
                      ?? throw new InvalidOperationException(
                          $"Country '{countryId}' has no default currency configured (DefaultCurrencyCode '')."));
        mock.Setup(s => s.ResolveCurrencyForEmployeeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string employeeId, CancellationToken _) =>
                cleaners.FirstOrDefault(c => c.EmployeeId == employeeId).Currency ?? platformDefault);
        mock.Setup(s => s.ResolveCurrencyForServingEmployeeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string employeeId, CancellationToken _) =>
                cleaners.FirstOrDefault(c => c.EmployeeId == employeeId).Currency ?? platformDefault);
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

    /// <summary>One operating company serving every country: the single-market platform's operator map.</summary>
    public static IOperatorTenantResolver OperatedBy(string tenantId)
    {
        var mock = new Mock<IOperatorTenantResolver>();
        mock.Setup(r => r.ResolveAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperatorResolution(true, tenantId));
        return mock.Object;
    }

    /// <summary>
    /// The named countries are markets served by the named companies; any other country is not a market
    /// — the shape of a holding with one company per country.
    /// </summary>
    public static IOperatorTenantResolver Operators(params (string CountryId, string TenantId)[] operators)
    {
        var mock = new Mock<IOperatorTenantResolver>();
        mock.Setup(r => r.ResolveAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string? countryId, CancellationToken _) =>
                operators.FirstOrDefault(o => o.CountryId == countryId) is { TenantId: { } tenantId }
                    ? new OperatorResolution(true, tenantId)
                    : OperatorResolution.NotAMarket);
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
