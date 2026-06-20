using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.AppServices.Mappers;

public static class GdprRequestMappers
{
    public static GdprRequestDto MapToDto(this GdprRequest request)
    {
        return new GdprRequestDto(
            request.Id,
            request.UserId,
            request.RequestType,
            request.Status,
            request.ProcessedBy,
            request.CompletedAt,
            request.Notes,
            request.CreatedOn);
    }
}
