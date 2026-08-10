using Cleansia.Core.AppServices.Features.PayConfig;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.AppServices.Features.Services.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Services;

public class GetServiceOverview
{
    public record Request : IRequest<IEnumerable<ServiceListItem>>;

    public class Handler(
        IServiceRepository serviceRepository,
        IEmployeePayConfigRepository payConfigRepository)
        : IRequestHandler<Request, IEnumerable<ServiceListItem>>
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

            // Bookable is IsActive AND quotable. Offering an entry with no platform-wide pay config
            // books an order that shows no pay on any cleaner's board, so the wizard withholds it —
            // the same treatment, and the same silence, a deactivated entry already gets.
            var unquotable = (await PayCoverageLookup.FindGapsAsync(
                    payConfigRepository,
                    services
                        .Select(s => new PayCoverageTarget(PayCoverageTargetKind.Service, s.Id, s.Name))
                        .ToList(),
                    employeeId: null,
                    cancellationToken))
                .Select(gap => gap.Id)
                .ToHashSet();

            return services
                .Where(service => !unquotable.Contains(service.Id))
                .Select(service => service.MapToDto());
        }
    }
}