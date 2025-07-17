#nullable enable
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.AppServices.Shared.DTOs.Sorting;

public record SortDefinition(
    string? Field,
    SortDirection Direction = SortDirection.Ascending);