using Cleansia.Core.AppServices.Features.Extras.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Extras;

/// <summary>
/// Customer-facing list of bookable add-ons. Anonymous endpoint — no auth
/// required, since the booking wizard renders the catalog before sign-in.
/// Mirrors <see cref="Services.GetServiceOverview"/> in shape.
/// </summary>
public class GetExtraOverview
{
    public record Request : IRequest<IEnumerable<ExtraListItem>>;

    public class Handler(IExtraRepository extraRepository)
        : IRequestHandler<Request, IEnumerable<ExtraListItem>>
    {
        public async Task<IEnumerable<ExtraListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            // Soft-deleted extras (IsActive=false) stay referenceable by
            // historical orders but never surface in the catalog.
            var extras = await extraRepository.GetAll()
                .Where(e => e.IsActive)
                .OrderBy(e => e.DisplayOrder)
                .ToListAsync(cancellationToken);

            return extras.Select(extra => extra.MapToDto());
        }
    }
}
