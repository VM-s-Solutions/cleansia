using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.PayConfig.DTOs;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.PayConfig;

public class GetEmployeePayConfigSummary
{
    public record Query(string EmployeeId) : IQuery<EmployeePayConfigSummaryDto>;

    public class Validator : AbstractValidator<Query>
    {
        public Validator(IEmployeeRepository employeeRepository)
        {
            RuleFor(x => x.EmployeeId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(employeeRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotFound);
        }
    }

    internal class Handler(
        IEmployeePayConfigRepository payConfigRepository,
        IServiceRepository serviceRepository,
        IPackageRepository packageRepository)
        : IQueryHandler<Query, EmployeePayConfigSummaryDto>
    {
        public async Task<BusinessResult<EmployeePayConfigSummaryDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var services = await serviceRepository.GetAll()
                .Select(s => new { s.Id, s.Name })
                .ToListAsync(cancellationToken);

            var packages = await packageRepository.GetAll()
                .Select(p => new { p.Id, p.Name })
                .ToListAsync(cancellationToken);

            var employeeConfigs = await payConfigRepository
                .GetByEmployeeIdAsync(query.EmployeeId, cancellationToken);

            var serviceItems = services.Select(s =>
            {
                var config = employeeConfigs.FirstOrDefault(c => c.ServiceId == s.Id);
                return new EmployeePayConfigSummaryItemDto(
                    ConfigId: config?.Id,
                    ServiceId: s.Id,
                    ServiceName: s.Name,
                    PackageId: null,
                    PackageName: null,
                    HasConfig: config != null,
                    BasePay: config?.BasePay ?? 0,
                    ExtraPerRoom: config?.ExtraPerRoom ?? 0,
                    ExtraPerBathroom: config?.ExtraPerBathroom ?? 0,
                    DistanceRatePerKm: config?.DistanceRatePerKm ?? 0,
                    MinimumPay: config?.MinimumPay ?? 0,
                    MaximumPay: config?.MaximumPay ?? 0,
                    CurrencyId: config?.CurrencyId,
                    CurrencyCode: config?.Currency?.Code);
            }).ToList();

            var packageItems = packages.Select(p =>
            {
                var config = employeeConfigs.FirstOrDefault(c => c.PackageId == p.Id);
                return new EmployeePayConfigSummaryItemDto(
                    ConfigId: config?.Id,
                    ServiceId: null,
                    ServiceName: null,
                    PackageId: p.Id,
                    PackageName: p.Name,
                    HasConfig: config != null,
                    BasePay: config?.BasePay ?? 0,
                    ExtraPerRoom: config?.ExtraPerRoom ?? 0,
                    ExtraPerBathroom: config?.ExtraPerBathroom ?? 0,
                    DistanceRatePerKm: config?.DistanceRatePerKm ?? 0,
                    MinimumPay: config?.MinimumPay ?? 0,
                    MaximumPay: config?.MaximumPay ?? 0,
                    CurrencyId: config?.CurrencyId,
                    CurrencyCode: config?.Currency?.Code);
            }).ToList();

            var dto = new EmployeePayConfigSummaryDto(
                EmployeeId: query.EmployeeId,
                TotalServices: services.Count,
                TotalPackages: packages.Count,
                ConfiguredServices: serviceItems.Count(i => i.HasConfig),
                ConfiguredPackages: packageItems.Count(i => i.HasConfig),
                Services: serviceItems,
                Packages: packageItems);

            return BusinessResult.Success(dto);
        }
    }
}
