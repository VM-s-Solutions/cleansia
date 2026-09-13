using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities.MockDataFactories.Currencies;
using Moq;

namespace Cleansia.TestUtilities.MockDataFactories.Memberships;

/// <summary>
/// The per-market pricing collaborators a membership handler test needs: a market that resolves to
/// CZK, and a price repository that answers with one row per (plan, currency) it was told about.
/// </summary>
public static class MembershipPricingMockFactory
{
    public const string CzkCurrencyId = "currency-czk";
    public const string EurCurrencyId = "currency-eur";

    public static Currency Czk()
    {
        var currency = CurrencyMockFactory.Generate();
        currency.Id = CzkCurrencyId;
        currency.SetAsDefault(true);
        return currency;
    }

    public static Currency Eur()
    {
        var currency = CurrencyMockFactory.Generate(new CurrencyMockFactory.CurrencyPartial { Code = "EUR", Symbol = "€", Name = "Euro" });
        currency.Id = EurCurrencyId;
        return currency;
    }

    /// <summary>The plan has this price row in this currency; every other (plan, currency) stays unpriced.</summary>
    public static MembershipPlanPrice PriceIn(
        this Mock<IMembershipPlanPriceRepository> repository,
        string planId,
        string currencyId,
        string stripePriceId,
        decimal price = 199m)
    {
        var row = MembershipPlanPrice.Create(planId, currencyId, price, stripePriceId);
        repository
            .Setup(r => r.GetForPlanAsync(planId, currencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);
        return row;
    }
}
