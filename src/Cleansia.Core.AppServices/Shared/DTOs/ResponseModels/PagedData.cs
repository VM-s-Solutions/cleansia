namespace Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;

public record PagedData<T>(
    int PageNumber,
    int PageSize,
    int Total,
    IEnumerable<T> Data);
