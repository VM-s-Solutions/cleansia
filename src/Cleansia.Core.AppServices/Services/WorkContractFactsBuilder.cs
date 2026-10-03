using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Services;

public sealed class WorkContractFactsBuilder(
    IOrderRepository orderRepository,
    IEmployeePayConfigRepository payConfigRepository) : IWorkContractFactsBuilder
{
    public async Task<(WorkContractFacts Facts, (decimal jobBasePay, decimal jobExtrasPay, decimal jobMinPay, decimal jobMaxPay)? JobPay)?> BuildAsync(
        string orderId, string employeeId, CancellationToken cancellationToken)
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
                o.CurrencyId,
                CurrencyCode = o.Currency!.Code,
                City = o.CustomerAddress!.City,
                ZipCode = o.CustomerAddress.ZipCode,
                CountryId = o.CustomerAddress.CountryId,
                o.Rooms,
                o.Bathrooms,
                o.RequiredEmployees,
                o.DirtinessRate,
                Services = o.SelectedServices.Select(s => new WorkContractFactsLine(s.ServiceId, s.Service!.Name)).ToList(),
                Packages = o.SelectedPackages.Select(p => new WorkContractFactsLine(p.PackageId, p.Package!.Name)).ToList(),
                ExtraSlugs = o.SelectedExtras.Select(e => e.Slug).ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var serviceIds = row.Services.Select(s => s.Id).ToHashSet();
        var packageIds = row.Packages.Select(p => p.Id).ToHashSet();
        IReadOnlyList<EmployeePayConfig> serviceConfigs = serviceIds.Count == 0
            ? []
            : await payConfigRepository.GetServiceConfigsForOrderAsync(serviceIds, employeeId, [row.CurrencyId], cancellationToken);
        IReadOnlyList<EmployeePayConfig> packageConfigs = packageIds.Count == 0
            ? []
            : await payConfigRepository.GetPackageConfigsForOrderAsync(packageIds, employeeId, [row.CurrencyId], cancellationToken);

        // No rate in the order's currency is also what the pay run would find, and it writes nothing for
        // the seat; booking and pay-config deletion both refuse to leave a live order in that state.
        var jobPay = OrderPayEstimator.JobPay(
            serviceIds, packageIds, row.Rooms, row.Bathrooms, row.CurrencyId, employeeId, serviceConfigs, packageConfigs);
        var reward = jobPay is { } priced
            ? OrderPayEstimator.SeatReward(priced, row.DirtinessRate, row.RequiredEmployees)
            : 0m;

        var facts = new WorkContractFacts(
            OrderNumber: row.DisplayOrderNumber,
            CleaningDateTimeUtc: row.CleaningDateTime,
            EstimatedMinutes: row.EstimatedTime,
            TotalPrice: reward,
            CurrencyCode: row.CurrencyCode,
            LocationApproximate: OrderMappers.BuildApproximateAddress(row.City, row.ZipCode),
            CountryId: row.CountryId,
            Rooms: row.Rooms,
            Bathrooms: row.Bathrooms,
            Services: row.Services,
            Packages: row.Packages,
            ExtraSlugs: row.ExtraSlugs);

        return (facts, jobPay);
    }
}
