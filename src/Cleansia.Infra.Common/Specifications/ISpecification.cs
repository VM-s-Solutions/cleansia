using System.Linq.Expressions;

namespace Cleansia.Infra.Common.Specifications;

public interface ISpecification<TEntity>
    where TEntity : class
{
    Expression<Func<TEntity, bool>> SatisfiedBy();
}