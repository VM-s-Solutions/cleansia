using System.Linq.Expressions;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class PayPeriodSpecification : Specification<PayPeriod>, ISpecification<PayPeriod>
{
    public PayPeriodStatus? Status { get; set; }

    public int? Year { get; set; }

    public override Expression<Func<PayPeriod, bool>> SatisfiedBy()
    {
        Specification<PayPeriod> specification = new TrueSpecification<PayPeriod>();

        if (Status.HasValue)
        {
            specification &= new DirectSpecification<PayPeriod>(x => x.Status == Status.Value);
        }

        if (Year.HasValue)
        {
            specification &= new DirectSpecification<PayPeriod>(x => x.StartDate.Year == Year.Value || x.EndDate.Year == Year.Value);
        }

        return specification.SatisfiedBy();
    }

    public static PayPeriodSpecification Create(PayPeriodStatus? status = null, int? year = null) =>
        new()
        {
            Status = status,
            Year = year
        };
}
