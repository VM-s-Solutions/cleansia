namespace Cleansia.Core.Domain.Common;

public class BaseEntity : IEntity<string>
{
    public virtual string Id { get; set; } = Ulid.NewUlid().ToString();

    public bool IsActive { get; set; } = true;


    object IEntity.Id
    {
        get => Id!;
        set => Id = (string)value;
    }
}
