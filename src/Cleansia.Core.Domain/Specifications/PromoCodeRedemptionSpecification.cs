using System.Linq.Expressions;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class PromoCodeRedemptionSpecification : BaseSpecification<string?>, ISpecification<PromoCodeRedemption>
{
    public string? PromoCodeId { get; set; }

    public Expression<Func<PromoCodeRedemption, bool>> SatisfiedBy()
    {
        Specification<PromoCodeRedemption> specification = new TrueSpecification<PromoCodeRedemption>();

        if (!string.IsNullOrWhiteSpace(Id))
        {
            specification &= new DirectSpecification<PromoCodeRedemption>(x => x.Id == Id);
        }

        if (IsActive.HasValue)
        {
            specification &= new DirectSpecification<PromoCodeRedemption>(x => x.IsActive == IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(PromoCodeId))
        {
            specification &= new DirectSpecification<PromoCodeRedemption>(x => x.PromoCodeId == PromoCodeId);
        }

        return specification.SatisfiedBy();
    }

    public static PromoCodeRedemptionSpecification Create(
        string? promoCodeId = null) =>
        new()
        {
            PromoCodeId = promoCodeId
        };
}
