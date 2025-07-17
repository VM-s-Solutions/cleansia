using System.Linq.Expressions;

namespace Cleansia.Infra.Common.Specifications;

public sealed class NotSpecification<TEntity>
    : Specification<TEntity>
    where TEntity : class
{
    private readonly Expression<Func<TEntity, bool>> originalCriteria;

    public NotSpecification(ISpecification<TEntity> originalSpecification)
    {
        ArgumentNullException.ThrowIfNull(originalSpecification);

        this.originalCriteria = originalSpecification.SatisfiedBy();
    }

    public NotSpecification(Expression<Func<TEntity, bool>> originalSpecification)
    {
        this.originalCriteria = originalSpecification ?? throw new ArgumentNullException(nameof(originalSpecification));
    }

    public override Expression<Func<TEntity, bool>> SatisfiedBy()
    {
        return Expression.Lambda<Func<TEntity, bool>>(
            Expression.Not(this.originalCriteria.Body),
            this.originalCriteria.Parameters.Single());
    }
}