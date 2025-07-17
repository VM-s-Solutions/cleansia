using System.Linq.Expressions;

namespace Cleansia.Infra.Common.Specifications;

public sealed class OrSpecification<T>(ISpecification<T> leftSide, ISpecification<T> rightSide)
    : CompositeSpecification<T>
    where T : class
{
    public override ISpecification<T> LeftSideSpecification { get; } = leftSide;

    public override ISpecification<T> RightSideSpecification { get; } = rightSide;

    public override Expression<Func<T, bool>> SatisfiedBy()
    {
        var left = this.LeftSideSpecification.SatisfiedBy();
        var right = this.RightSideSpecification.SatisfiedBy();
        return left.Or(right);
    }
}
