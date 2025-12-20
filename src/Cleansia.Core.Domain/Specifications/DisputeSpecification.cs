using System.Linq.Expressions;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class DisputeSpecification : BaseSpecification<string?>, ISpecification<Dispute>
{
    public string? OrderId { get; set; }
    public string? UserId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public IEnumerable<DisputeStatus>? Statuses { get; set; }
    public IEnumerable<DisputeReason>? Reasons { get; set; }
    public DateTimeOffset? CreatedFrom { get; set; }
    public DateTimeOffset? CreatedTo { get; set; }
    public DateTimeOffset? ResolvedFrom { get; set; }
    public DateTimeOffset? ResolvedTo { get; set; }
    public decimal? MinRefundAmount { get; set; }
    public decimal? MaxRefundAmount { get; set; }

    public Expression<Func<Dispute, bool>> SatisfiedBy()
    {
        Specification<Dispute> specification = new TrueSpecification<Dispute>();

        if (!string.IsNullOrWhiteSpace(Id))
        {
            specification &= new DirectSpecification<Dispute>(x => x.Id == Id);
        }

        if (IsActive.HasValue)
        {
            specification &= new DirectSpecification<Dispute>(x => x.IsActive == IsActive.Value);
        }

        if (!string.IsNullOrEmpty(OrderId))
        {
            specification &= new DirectSpecification<Dispute>(x => x.OrderId == OrderId);
        }

        if (!string.IsNullOrEmpty(UserId))
        {
            specification &= new DirectSpecification<Dispute>(x => x.UserId == UserId);
        }

        if (!string.IsNullOrEmpty(CustomerName))
        {
            specification &= new DirectSpecification<Dispute>(x =>
                x.User != null && (x.User.FirstName.Contains(CustomerName) || x.User.LastName.Contains(CustomerName)));
        }

        if (!string.IsNullOrEmpty(CustomerEmail))
        {
            specification &= new DirectSpecification<Dispute>(x =>
                x.User != null && x.User.Email.Contains(CustomerEmail));
        }

        if (Statuses is not null && Statuses.Any())
        {
            specification &= new DirectSpecification<Dispute>(x => Statuses.Contains(x.Status));
        }

        if (Reasons is not null && Reasons.Any())
        {
            specification &= new DirectSpecification<Dispute>(x => Reasons.Contains(x.Reason));
        }

        if (CreatedFrom.HasValue)
        {
            specification &= new DirectSpecification<Dispute>(x => x.CreatedOn >= CreatedFrom.Value);
        }

        if (CreatedTo.HasValue)
        {
            specification &= new DirectSpecification<Dispute>(x => x.CreatedOn <= CreatedTo.Value);
        }

        if (ResolvedFrom.HasValue)
        {
            specification &= new DirectSpecification<Dispute>(x => x.ResolvedOn.HasValue && x.ResolvedOn.Value >= ResolvedFrom.Value);
        }

        if (ResolvedTo.HasValue)
        {
            specification &= new DirectSpecification<Dispute>(x => x.ResolvedOn.HasValue && x.ResolvedOn.Value <= ResolvedTo.Value);
        }

        if (MinRefundAmount.HasValue)
        {
            specification &= new DirectSpecification<Dispute>(x => x.RefundAmount.HasValue && x.RefundAmount.Value >= MinRefundAmount.Value);
        }

        if (MaxRefundAmount.HasValue)
        {
            specification &= new DirectSpecification<Dispute>(x => x.RefundAmount.HasValue && x.RefundAmount.Value <= MaxRefundAmount.Value);
        }

        return specification.SatisfiedBy();
    }

    public static DisputeSpecification Create(
        string? orderId = null,
        string? userId = null,
        string? customerName = null,
        string? customerEmail = null,
        IEnumerable<DisputeStatus>? statuses = null,
        IEnumerable<DisputeReason>? reasons = null,
        DateTimeOffset? createdFrom = null,
        DateTimeOffset? createdTo = null,
        DateTimeOffset? resolvedFrom = null,
        DateTimeOffset? resolvedTo = null,
        decimal? minRefundAmount = null,
        decimal? maxRefundAmount = null)
    {
        return new DisputeSpecification
        {
            OrderId = orderId,
            UserId = userId,
            CustomerName = customerName,
            CustomerEmail = customerEmail,
            Statuses = statuses,
            Reasons = reasons,
            CreatedFrom = createdFrom,
            CreatedTo = createdTo,
            ResolvedFrom = resolvedFrom,
            ResolvedTo = resolvedTo,
            MinRefundAmount = minRefundAmount,
            MaxRefundAmount = maxRefundAmount
        };
    }
}
