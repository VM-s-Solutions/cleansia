using Cleansia.Core.AppServices.Features.Packages.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Packages;

public class GetPackageOverview
{
    public record Request : IRequest<IEnumerable<PackageListItem>>;

    public class Handler(IPackageRepository packageRepository) : IRequestHandler<Request, IEnumerable<PackageListItem>>
    {
        public async Task<IEnumerable<PackageListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            // Customer-facing — only return packages the admin has marked
            // IsActive. Deactivated packages are admin-only state and must
            // not appear in the booking wizard catalog.
            var packages = await packageRepository.GetAll()
                .Where(p => p.IsActive)
                .Include(p => p.IncludedServices)
                    .ThenInclude(ps => ps.Service)
                .ToListAsync(cancellationToken);

            return packages.Select(package => package.MapToDto());
        }
    }
}