using System.Linq.Expressions;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class PromoCodeSpecification : BaseSpecification<string?>, ISpecification<PromoCode>
{
    public string? SearchCode { get; set; }
    public DateTimeOffset? ExpiredReference { get; set; }
    public bool? Expired { get; set; }

    public Expression<Func<PromoCode, bool>> SatisfiedBy()
    {
        Specification<PromoCode> specification = new TrueSpecification<PromoCode>();

        if (!string.IsNullOrWhiteSpace(Id))
        {
            specification &= new DirectSpecification<PromoCode>(x => x.Id == Id);
        }

        if (IsActive.HasValue)
        {
            specification &= new DirectSpecification<PromoCode>(x => x.IsActive == IsActive.Value);
        }

        if (Expired.HasValue && ExpiredReference.HasValue)
        {
            var now = ExpiredReference.Value;
            specification &= Expired.Value
                ? new DirectSpecification<PromoCode>(x => x.ValidUntil != null && x.ValidUntil < now)
                : new DirectSpecification<PromoCode>(x => x.ValidUntil == null || x.ValidUntil >= now);
        }

        if (!string.IsNullOrWhiteSpace(SearchCode))
        {
            var needle = SearchCode.Trim().ToUpperInvariant();
            specification &= new DirectSpecification<PromoCode>(x => x.Code.Contains(needle));
        }

        return specification.SatisfiedBy();
    }

    public static PromoCodeSpecification Create(
        bool? isActive = null,
        bool? expired = null,
        DateTimeOffset? expiredReference = null,
        string? searchCode = null) =>
        new()
        {
            IsActive = isActive,
            Expired = expired,
            ExpiredReference = expiredReference,
            SearchCode = searchCode
        };
}
