using Cleansia.Core.AppServices.Features.Services.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Services;

public class GetServiceOverview
{
    public record Request : IRequest<IEnumerable<ServiceListItem>>;

    public class Handler(IServiceRepository serviceRepository) : IRequestHandler<Request, IEnumerable<ServiceListItem>>
    {
        public async Task<IEnumerable<ServiceListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            // Customer-facing — only return services the admin has marked
            // IsActive. Deactivated services are admin-only state and must
            // not appear in the booking wizard catalog.
            var services = await serviceRepository.GetAll()
                .Where(s => s.IsActive)
                .Include(s => s.Category)
                .ToListAsync(cancellationToken);

            return services.Select(service => service.MapToDto());
        }
    }
}