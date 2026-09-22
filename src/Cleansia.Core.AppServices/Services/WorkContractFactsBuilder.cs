using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Services;

public sealed class WorkContractFactsBuilder(IOrderRepository orderRepository) : IWorkContractFactsBuilder
{
    public async Task<WorkContractFacts?> BuildAsync(string orderId, CancellationToken cancellationToken)
    {
        // Its own projection, never the take handler's tracked aggregate: that load is the seat-race
        // arbiter's and must not widen for a read.
        var row = await orderRepository
            .GetQueryable()
            .AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new
            {
                o.DisplayOrderNumber,
                o.CleaningDateTime,
                o.EstimatedTime,
                o.TotalPrice,
                CurrencyCode = o.Currency!.Code,
                City = o.CustomerAddress!.City,
                ZipCode = o.CustomerAddress.ZipCode,
                CountryId = o.CustomerAddress.CountryId,
                o.Rooms,
                o.Bathrooms,
                Services = o.SelectedServices.Select(s => new WorkContractFactsLine(s.ServiceId, s.Service!.Name)).ToList(),
                Packages = o.SelectedPackages.Select(p => new WorkContractFactsLine(p.PackageId, p.Package!.Name)).ToList(),
                ExtraSlugs = o.SelectedExtras.Select(e => e.Slug).ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        return new WorkContractFacts(
            OrderNumber: row.DisplayOrderNumber,
            CleaningDateTimeUtc: row.CleaningDateTime,
            EstimatedMinutes: row.EstimatedTime,
            TotalPrice: row.TotalPrice,
            CurrencyCode: row.CurrencyCode,
            LocationApproximate: OrderMappers.BuildApproximateAddress(row.City, row.ZipCode),
            CountryId: row.CountryId,
            Rooms: row.Rooms,
            Bathrooms: row.Bathrooms,
            Services: row.Services,
            Packages: row.Packages,
            ExtraSlugs: row.ExtraSlugs);
    }
}
