namespace Cleansia.Core.Domain.Specifications;

public class BaseSpecification
{
    public int? Id { get; set; }

    public bool? IsActive { get; set; }
}

public class BaseSpecification<T>
{
    public T? Id { get; set; }

    public bool? IsActive { get; set; }
}
