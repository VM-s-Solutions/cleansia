#nullable enable
using System.ComponentModel.DataAnnotations;
using Cleansia.Core.AppServices.Shared.DTOs.Sorting;

namespace Cleansia.Core.AppServices.Shared.DTOs.RequestModels;

public class DataRangeRequest
{
    public IEnumerable<SortDefinition>? Sort { get; init; } = null;

    [Range(0, 500)]
    public int Offset { get; init; } = 0;

    [Range(1, 100000)]
    public int Limit { get; init; } = 50;
}