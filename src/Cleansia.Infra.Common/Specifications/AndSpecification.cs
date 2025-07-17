using System.Linq.Expressions;

namespace Cleansia.Infra.Common.Specifications;

public sealed class AndSpecification<T>(ISpecification<T> leftSide, ISpecification<T> rightSide)
    : CompositeSpecification<T>
    where T : class
{
    public override ISpecification<T> LeftSideSpecification { get; } = leftSide ?? throw new ArgumentNullException(nameof(leftSide));

    public override ISpecification<T> RightSideSpecification { get; } = rightSide ?? throw new ArgumentNullException(nameof(rightSide));

    public override Expression<Func<T, bool>> SatisfiedBy()
    {
        var left = LeftSideSpecification.SatisfiedBy();
        var right = RightSideSpecification.SatisfiedBy();
        return left.And(right);
    }
}