using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;

namespace Cleansia.Core.AppServices.Mappers;

public static class PageDataMapper
{
    public static PagedData<T> MapToDto<T>(this IEnumerable<T> pagedData, int total, DataRangeRequest request)
    {
        return new PagedData<T>(
            PageNumber: request.Offset / request.Limit + 1,
            PageSize: request.Limit,
            Total: total,
            Data: pagedData);
    }
}