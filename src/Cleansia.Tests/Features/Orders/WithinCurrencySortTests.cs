using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// A money column sorted across currencies is not an order — 150 EUR would file below 3 000 CZK on an
/// admin list that shows both. When the page is not pinned to one currency, the money sort is applied
/// within currency by leading with the currency column; with a currency filter the page is one currency
/// and the sort the client asked for is honest as it stands.
/// </summary>
public class WithinCurrencySortTests
{
    [Fact]
    public void A_Money_Sort_With_No_Currency_Filter_Is_Led_By_Currency()
    {
        var sort = new[] { Sort("totalPrice", SortDirection.Descending) }
            .WithinCurrencyWhenSortedBy("TotalPrice", currencyFilter: null)
            .ToList();

        Assert.Equal(2, sort.Count);
        Assert.Equal("CurrencyId", sort[0].Field);
        Assert.Equal(SortDirection.Ascending, sort[0].Direction);
        Assert.Equal("totalPrice", sort[1].Field);
        Assert.Equal(SortDirection.Descending, sort[1].Direction);
    }

    [Fact]
    public void A_Money_Sort_Inside_One_Currency_Is_Left_As_Asked()
    {
        var sort = new[] { Sort("totalAmount", SortDirection.Descending) }
            .WithinCurrencyWhenSortedBy("TotalAmount", currencyFilter: "currency-eur")
            .ToList();

        Assert.Equal("totalAmount", Assert.Single(sort).Field);
    }

    [Fact]
    public void A_Sort_On_Anything_Else_Is_Left_As_Asked()
    {
        var sort = new[] { Sort("cleaningDateTime", SortDirection.Ascending) }
            .WithinCurrencyWhenSortedBy("TotalPrice", currencyFilter: null)
            .ToList();

        Assert.Equal("cleaningDateTime", Assert.Single(sort).Field);
    }

    private static SortDefinition Sort(string field, SortDirection direction) =>
        new() { Field = field, Direction = direction };
}
