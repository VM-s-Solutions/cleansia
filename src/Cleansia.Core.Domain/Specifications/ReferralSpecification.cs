using System.Linq.Expressions;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class ReferralSpecification : BaseSpecification<string?>, ISpecification<Referral>
{
    public string? ReferrerUserId { get; set; }
    public ReferralStatus? Status { get; set; }
    public DateTimeOffset? AcceptedFrom { get; set; }
    public DateTimeOffset? AcceptedTo { get; set; }

    public Expression<Func<Referral, bool>> SatisfiedBy()
    {
        Specification<Referral> specification = new TrueSpecification<Referral>();

        if (!string.IsNullOrWhiteSpace(Id))
        {
            specification &= new DirectSpecification<Referral>(x => x.Id == Id);
        }

        if (IsActive.HasValue)
        {
            specification &= new DirectSpecification<Referral>(x => x.IsActive == IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(ReferrerUserId))
        {
            specification &= new DirectSpecification<Referral>(x => x.ReferrerUserId == ReferrerUserId);
        }

        if (Status.HasValue)
        {
            specification &= new DirectSpecification<Referral>(x => x.Status == Status.Value);
        }

        if (AcceptedFrom.HasValue)
        {
            specification &= new DirectSpecification<Referral>(x => x.AcceptedOn >= AcceptedFrom.Value);
        }

        if (AcceptedTo.HasValue)
        {
            specification &= new DirectSpecification<Referral>(x => x.AcceptedOn <= AcceptedTo.Value);
        }

        return specification.SatisfiedBy();
    }

    public static ReferralSpecification Create(
        string? referrerUserId = null,
        ReferralStatus? status = null,
        DateTimeOffset? acceptedFrom = null,
        DateTimeOffset? acceptedTo = null) =>
        new()
        {
            ReferrerUserId = referrerUserId,
            Status = status,
            AcceptedFrom = acceptedFrom,
            AcceptedTo = acceptedTo
        };
}
