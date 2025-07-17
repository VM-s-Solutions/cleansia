namespace Cleansia.Core.Domain.Common;

public interface IEntity
{
    object Id { get; set; }

    bool IsActive { get; set; }
}

public interface IEntity<T> : IEntity
{
    new T Id { get; set; }
    new bool IsActive { get; set; }
}