using System.Linq.Expressions;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class EmployeePayConfigSpecification : Specification<EmployeePayConfig>, ISpecification<EmployeePayConfig>
{
    public string? ServiceId { get; set; }
    public string? PackageId { get; set; }
    public string? CurrencyId { get; set; }

    public override Expression<Func<EmployeePayConfig, bool>> SatisfiedBy()
    {
        Specification<EmployeePayConfig> specification = new TrueSpecification<EmployeePayConfig>();

        if (!string.IsNullOrEmpty(ServiceId))
        {
            specification &= new DirectSpecification<EmployeePayConfig>(x => x.ServiceId == ServiceId);
        }

        if (!string.IsNullOrEmpty(PackageId))
        {
            specification &= new DirectSpecification<EmployeePayConfig>(x => x.PackageId == PackageId);
        }

        if (!string.IsNullOrEmpty(CurrencyId))
        {
            specification &= new DirectSpecification<EmployeePayConfig>(x => x.CurrencyId == CurrencyId);
        }

        return specification.SatisfiedBy();
    }

    public static EmployeePayConfigSpecification Create(
        string? serviceId = null,
        string? packageId = null,
        string? currencyId = null) =>
        new()
        {
            ServiceId = serviceId,
            PackageId = packageId,
            CurrencyId = currencyId
        };
}
