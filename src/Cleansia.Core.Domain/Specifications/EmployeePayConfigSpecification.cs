using System.Linq.Expressions;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class EmployeePayConfigSpecification : Specification<EmployeePayConfig>, ISpecification<EmployeePayConfig>
{
    public string? EmployeeId { get; set; }
    public bool? GlobalOnly { get; set; }
    public string? ServiceId { get; set; }
    public string? PackageId { get; set; }
    public string? CurrencyId { get; set; }

    public override Expression<Func<EmployeePayConfig, bool>> SatisfiedBy()
    {
        Specification<EmployeePayConfig> specification = new TrueSpecification<EmployeePayConfig>();

        if (!string.IsNullOrEmpty(EmployeeId))
        {
            specification &= new DirectSpecification<EmployeePayConfig>(x => x.EmployeeId == EmployeeId);
        }

        if (GlobalOnly == true)
        {
            specification &= new DirectSpecification<EmployeePayConfig>(x => x.EmployeeId == null);
        }

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
        string? employeeId = null,
        bool? globalOnly = null,
        string? serviceId = null,
        string? packageId = null,
        string? currencyId = null) =>
        new()
        {
            EmployeeId = employeeId,
            GlobalOnly = globalOnly,
            ServiceId = serviceId,
            PackageId = packageId,
            CurrencyId = currencyId
        };
}
