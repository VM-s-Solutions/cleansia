using Cleansia.Core.Domain.Internalization;

namespace Cleansia.TestUtilities.MockDataFactories.Currencies;

public class CurrencyMockFactory
{
    public class CurrencyPartial
    {
        public string? Code { get; set; }

        public string? Symbol { get; set; }

        public string? Name { get; set; }

        public decimal? ExchangeRate { get; set; }
    }

    public static Currency Generate(CurrencyPartial? mergeFrom = null)
    {
        var currency = Currency.Create(
            "CZK",
            "Kč",
            "Czech Koruna",
            1);
        currency.Created(Constants.TestUserSession.TestUserName, DateTime.UtcNow);

        return currency.Merge(mergeFrom);
    }
}