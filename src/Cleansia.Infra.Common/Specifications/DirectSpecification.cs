using System.Linq.Expressions;

namespace Cleansia.Infra.Common.Specifications;

public sealed class DirectSpecification<TEntity>(Expression<Func<TEntity, bool>> matchingCriteria)
    : Specification<TEntity>
    where TEntity : class
{
    private readonly Expression<Func<TEntity, bool>> matchingCriteria = matchingCriteria ?? throw new ArgumentNullException(nameof(matchingCriteria));

    public override Expression<Func<TEntity, bool>> SatisfiedBy()
    {
        return matchingCriteria;
    }
}