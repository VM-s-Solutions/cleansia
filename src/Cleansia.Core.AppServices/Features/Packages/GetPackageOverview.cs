using Cleansia.Core.AppServices.Features.Catalog;
using Cleansia.Core.AppServices.Features.PayConfig;
using Cleansia.Core.AppServices.Features.Packages.DTOs;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Packages;

public class GetPackageOverview
{
    public record Request : IRequest<IEnumerable<PackageListItem>>;

    public class Handler(
        IPackageRepository packageRepository,
        IPackagePriceRepository packagePriceRepository,
        ICurrencyRepository currencyRepository,
        IEmployeePayConfigRepository payConfigRepository)
        : IRequestHandler<Request, IEnumerable<PackageListItem>>
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

            // Bookable is IsActive AND quotable — see GetServiceOverview for the reasoning.
            var unquotable = (await PayCoverageLookup.FindGapsAsync(
                    payConfigRepository,
                    packages
                        .Select(p => new PayCoverageTarget(PayCoverageTargetKind.Package, p.Id, p.Name))
                        .ToList(),
                    employeeId: null,
                    cancellationToken))
                .Select(gap => gap.Id)
                .ToHashSet();

            // AND priced in the currency being quoted -- see GetServiceOverview for the reasoning.
            var currency = await currencyRepository.GetDefaultAsync(cancellationToken);
            var prices = await CataloguePriceLookup.ForPackagesAsync(
                packagePriceRepository, packages.Select(p => p.Id).ToList(), currency.Id, cancellationToken);

            return packages
                .Where(package => !unquotable.Contains(package.Id) && prices.ContainsKey(package.Id))
                .Select(package => package.MapToDto(prices[package.Id]))
                .ToList();
        }
    }
}