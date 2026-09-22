using Cleansia.Core.AppServices.Shared.DTOs.Sorting;
using DB = Cleansia.Core.Domain.Sorting;

namespace Cleansia.Core.AppServices.Mappers;

public static class SortMapper
{
    public static IEnumerable<DB.Common.SortDefinition> MapToDomain(this IEnumerable<SortDefinition>? sortDefinitions)
    {
        return sortDefinitions is null ? [] : sortDefinitions.Select(x => x.MapToDomain());
    }

    /// <summary>
    /// A money column sorted across currencies is not an order: 150 EUR files below 3 000 CZK. When
    /// the page is not pinned to one currency the money sort runs WITHIN currency, led by the currency
    /// column; with a currency filter set the page is one currency and the plain sort is honest.
    /// </summary>
    public static IEnumerable<DB.Common.SortDefinition> WithinCurrencyWhenSortedBy(
        this IEnumerable<DB.Common.SortDefinition> sort,
        string moneyField,
        string? currencyFilter)
    {
        var definitions = sort.ToList();
        var sortsByMoney = definitions.Any(s => string.Equals(s.Field, moneyField, StringComparison.OrdinalIgnoreCase));
        if (!sortsByMoney || !string.IsNullOrEmpty(currencyFilter))
        {
            return definitions;
        }

        definitions.Insert(0, new DB.Common.SortDefinition
        {
            Field = "CurrencyId",
            Direction = DB.Common.SortDirection.Ascending
        });
        return definitions;
    }

    public static DB.Common.SortDefinition MapToDomain(this SortDefinition? sortDefinition)
    {
        return sortDefinition is null
            ? new DB.Common.SortDefinition
            {
                Direction = DB.Common.SortDirection.Ascending,
                Field = sortDefinition?.Field
            }
            : new DB.Common.SortDefinition
            {
                Direction = sortDefinition.Direction,
                Field = sortDefinition.Field
            };
    }
}