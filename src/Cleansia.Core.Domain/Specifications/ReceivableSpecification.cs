using System.Linq.Expressions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Payments;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class ReceivableSpecification : Specification<Receivable>, ISpecification<Receivable>
{
    public ReceivableStatus? Status { get; set; }

    public ReceivableKind? Kind { get; set; }

    public string? UserId { get; set; }

    public string? OrderId { get; set; }

    public override Expression<Func<Receivable, bool>> SatisfiedBy()
    {
        Specification<Receivable> specification = new TrueSpecification<Receivable>();

        if (Status.HasValue)
        {
            specification &= new DirectSpecification<Receivable>(x => x.Status == Status.Value);
        }

        if (Kind.HasValue)
        {
            specification &= new DirectSpecification<Receivable>(x => x.Kind == Kind.Value);
        }

        if (!string.IsNullOrEmpty(UserId))
        {
            specification &= new DirectSpecification<Receivable>(x => x.UserId == UserId);
        }

        if (!string.IsNullOrEmpty(OrderId))
        {
            specification &= new DirectSpecification<Receivable>(x => x.OrderId == OrderId);
        }

        return specification.SatisfiedBy();
    }

    public static ReceivableSpecification Create(
        ReceivableStatus? status = null,
        ReceivableKind? kind = null,
        string? userId = null,
        string? orderId = null) =>
        new()
        {
            Status = status,
            Kind = kind,
            UserId = userId,
            OrderId = orderId,
        };
}
