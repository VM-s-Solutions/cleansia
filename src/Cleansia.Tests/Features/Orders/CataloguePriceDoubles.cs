using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using MockQueryable;
using Moq;

namespace Cleansia.Tests;

/// <summary>
/// Catalogue price rows for ONE currency, for the suites that construct a pricing collaborator by hand.
///
/// <para>A catalogue entry has no price of its own any more — it has a price per currency — so a suite
/// that books anything now has to say what it costs AND in which currency. That second half is the
/// part worth stating: the lookup filters on <c>CurrencyId</c>, so rows authored against a different
/// <see cref="Currency"/> instance than the one in <c>CreateOrderInput</c> are simply not found. The
/// currency is therefore a required first argument here rather than a defaulted one — passing the same
/// object to both is the only way to get a hit, and making the caller name it is what makes that
/// visible.</para>
///
/// <para><b>Absent is not zero.</b> A missing row means not offerable, and the production lookups
/// throw on one, so a suite that forgets a row fails loudly instead of booking at nothing. That is why
/// there is no "prices everything at some default" helper: it would convert every such omission into a
/// green test asserting the wrong total.</para>
/// </summary>
internal static class CataloguePriceDoubles
{
    public static IServicePriceRepository Services(
        Currency currency, params (string ServiceId, decimal BasePrice, decimal PerRoomPrice)[] rows)
    {
        var mock = new Mock<IServicePriceRepository>();
        mock.Setup(r => r.GetAll()).Returns(rows
            .Select(r => ServicePrice.Create(r.ServiceId, currency.Id, r.BasePrice, r.PerRoomPrice))
            .AsQueryable()
            .BuildMock());
        return mock.Object;
    }

    public static IPackagePriceRepository Packages(
        Currency currency, params (string PackageId, decimal Price)[] rows)
    {
        var mock = new Mock<IPackagePriceRepository>();
        mock.Setup(r => r.GetAll()).Returns(rows
            .Select(r => PackagePrice.Create(r.PackageId, currency.Id, r.Price))
            .AsQueryable()
            .BuildMock());
        return mock.Object;
    }

    public static IExtraPriceRepository Extras(
        Currency currency, params (string ExtraId, decimal Price)[] rows)
    {
        var mock = new Mock<IExtraPriceRepository>();
        mock.Setup(r => r.GetAll()).Returns(rows
            .Select(r => ExtraPrice.Create(r.ExtraId, currency.Id, r.Price))
            .AsQueryable()
            .BuildMock());
        return mock.Object;
    }

    /// <summary>
    /// No rows at all — the right answer for the many suites that book neither a service, a package nor
    /// an extra and are only here because the constructor demands the dependency.
    /// </summary>
    public static IServicePriceRepository NoServices() => Services(Any);

    public static IPackagePriceRepository NoPackages() => Packages(Any);

    public static IExtraPriceRepository NoExtras() => Extras(Any);

    /// <summary>
    /// A currency repository whose default is <paramref name="currency"/> — the companion the customer
    /// catalogue handlers need, because they resolve the default currency to decide WHICH price rows to
    /// read. Same instance in both halves or the lookup finds nothing.
    /// </summary>
    public static ICurrencyRepository DefaultCurrency(Currency currency)
    {
        var mock = new Mock<ICurrencyRepository>();
        mock.Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(currency);
        return mock.Object;
    }

    /// <summary>A currency for the empty cases, where nothing is ever looked up against it.</summary>
    private static Currency Any => Currency.Create("CZK", "Kč", "Czech Koruna");
}
