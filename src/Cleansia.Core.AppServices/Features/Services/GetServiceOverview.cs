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
            return await serviceRepository.GetAll().Select(service => service.MapToDto()).ToListAsync(cancellationToken);
        }
    }
}