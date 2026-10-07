#nullable enable
using System.ComponentModel.DataAnnotations;
using Cleansia.Core.AppServices.Shared.DTOs.Sorting;

namespace Cleansia.Core.AppServices.Shared.DTOs.RequestModels;

public class DataRangeRequest
{
    public const int MaximumLimit = 100000;
    // Existing consumers calculate Offset + Limit and Offset / Limit + 1 as integers.
    public const int MaximumOffset = int.MaxValue - MaximumLimit;

    public IEnumerable<SortDefinition>? Sort { get; init; } = null;

    [Range(0, MaximumOffset)]
    public int Offset { get; init; } = 0;

    [Range(1, MaximumLimit)]
    public int Limit { get; init; } = 50;
}
