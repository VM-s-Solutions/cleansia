using Cleansia.Core.AppServices.Features.Catalog;
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
        IServicePriceRepository servicePriceRepository,
        ICurrencyRepository currencyRepository,
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

            // THE CURRENCY BEING BROWSED IN, resolved first because both filters below are asked in
            // it. Today that is the platform default; the overview carries no caller currency yet. It is
            // ONE variable feeding both the pay gate and the price lookup, so they cannot disagree.
            var currency = await currencyRepository.GetDefaultAsync(cancellationToken);

            // Bookable is IsActive AND quotable IN THIS CURRENCY. Offering an entry with no platform-wide
            // pay config books an order that shows no pay on any cleaner's board, so the wizard
            // withholds it -- the same treatment, and the same silence, a deactivated entry already
            // gets. A rate in another currency is not a rate for this order: the pay writer reads only
            // rows in the order's currency, so an entry admitted on a CZK rate would land on every EUR
            // board blank.
            var unquotable = (await PayCoverageLookup.FindGapsAsync(
                    payConfigRepository,
                    services
                        .Select(s => new PayCoverageTarget(PayCoverageTargetKind.Service, s.Id, s.Name))
                        .ToList(),
                    employeeId: null,
                    currency.Id,
                    cancellationToken))
                .Select(gap => gap.Id)
                .ToHashSet();


            // AND PRICED IN THE CURRENCY BEING QUOTED. Prices are authored per currency, so an entry
            // with no row in this one has no price in it -- not a free one. It gets exactly the
            // treatment, and the silence, that an unquotable or deactivated entry already gets: the
            // wizard never offers it, so nothing downstream has to decide what a missing price means.
            //
            // This is also what makes "the machinery is built, only CZK is reachable" provable: seed a
            // currency with no price rows and its catalogue is empty, by this line.
            var prices = await CataloguePriceLookup.ForServicesAsync(
                servicePriceRepository, services.Select(s => s.Id).ToList(), currency.Id, cancellationToken);

            return services
                .Where(service => !unquotable.Contains(service.Id) && prices.ContainsKey(service.Id))
                .Select(service => service.MapToDto(
                    prices[service.Id].BasePrice, prices[service.Id].PerRoomPrice))
                .ToList();
        }
    }
}