using System.Linq.Expressions;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class LoyaltyTransactionSpecification : BaseSpecification<string?>, ISpecification<LoyaltyTransaction>
{
    public string? LoyaltyAccountId { get; set; }

    public Expression<Func<LoyaltyTransaction, bool>> SatisfiedBy()
    {
        Specification<LoyaltyTransaction> specification = new TrueSpecification<LoyaltyTransaction>();

        if (!string.IsNullOrWhiteSpace(Id))
        {
            specification &= new DirectSpecification<LoyaltyTransaction>(x => x.Id == Id);
        }

        if (IsActive.HasValue)
        {
            specification &= new DirectSpecification<LoyaltyTransaction>(x => x.IsActive == IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(LoyaltyAccountId))
        {
            specification &= new DirectSpecification<LoyaltyTransaction>(x => x.LoyaltyAccountId == LoyaltyAccountId);
        }

        return specification.SatisfiedBy();
    }

    public static LoyaltyTransactionSpecification Create(
        string? loyaltyAccountId = null) =>
        new()
        {
            LoyaltyAccountId = loyaltyAccountId
        };
}
