using System.Linq.Expressions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Payments;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class RefundSpecification : Specification<Refund>, ISpecification<Refund>
{
    public RefundState[]? States { get; set; }

    public string? UserId { get; set; }

    public string? OrderId { get; set; }

    public DateTimeOffset? RequestedFrom { get; set; }

    public DateTimeOffset? RequestedTo { get; set; }

    public override Expression<Func<Refund, bool>> SatisfiedBy()
    {
        Specification<Refund> specification = new TrueSpecification<Refund>();

        if (States is { Length: > 0 })
        {
            Specification<Refund>? any = null;
            foreach (var state in States.Distinct())
            {
                var one = new DirectSpecification<Refund>(InState(state));
                any = any is null ? one : any | one;
            }

            specification &= any!;
        }

        if (!string.IsNullOrEmpty(UserId))
        {
            specification &= new DirectSpecification<Refund>(x => x.Order!.UserId == UserId);
        }

        if (!string.IsNullOrEmpty(OrderId))
        {
            specification &= new DirectSpecification<Refund>(x => x.OrderId == OrderId);
        }

        if (RequestedFrom.HasValue)
        {
            specification &= new DirectSpecification<Refund>(x => x.CreatedOn >= RequestedFrom.Value);
        }

        if (RequestedTo.HasValue)
        {
            specification &= new DirectSpecification<Refund>(x => x.CreatedOn <= RequestedTo.Value);
        }

        return specification.SatisfiedBy();
    }

    private static Expression<Func<Refund, bool>> InState(RefundState state) => state switch
    {
        RefundState.NotSent => x => x.RepaidOn == null && x.Status == RefundStatus.Pending,
        RefundState.Refunded => x => x.RepaidOn == null && x.Status == RefundStatus.Succeeded && x.FailedOn == null,
        RefundState.Bounced => x => x.RepaidOn == null && x.Status == RefundStatus.Succeeded && x.FailedOn != null,
        RefundState.Failed => x => x.RepaidOn == null && x.Status == RefundStatus.Failed && x.FailedOn != null,
        RefundState.Repaid => x => x.RepaidOn != null,
        RefundState.Closed => x => x.RepaidOn == null && x.Status == RefundStatus.Failed && x.FailedOn == null,
        _ => x => false,
    };

    public static RefundSpecification Create(
        RefundState[]? states = null,
        string? userId = null,
        string? orderId = null,
        DateTimeOffset? requestedFrom = null,
        DateTimeOffset? requestedTo = null) =>
        new()
        {
            States = states,
            UserId = userId,
            OrderId = orderId,
            RequestedFrom = requestedFrom,
            RequestedTo = requestedTo,
        };
}
