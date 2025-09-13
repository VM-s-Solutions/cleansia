using Cleansia.Core.AppServices.Shared.DTOs.Sorting;
using DB = Cleansia.Core.Domain.Sorting;

namespace Cleansia.Core.AppServices.Mappers;

public static class SortMapper
{
    public static IEnumerable<DB.Common.SortDefinition> MapToDomain(this IEnumerable<SortDefinition>? sortDefinitions)
    {
        return sortDefinitions is null ? [] : sortDefinitions.Select(x => x.MapToDomain());
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