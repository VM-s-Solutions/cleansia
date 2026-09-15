namespace Cleansia.Core.Domain.Common;

public class Auditable : BaseEntity
{
    public string CreatedBy { get; private set; } = default!;

    public DateTimeOffset CreatedOn { get; private set; } = DateTimeOffset.UtcNow;

    public string? UpdatedBy { get; private set; }

    public DateTimeOffset? UpdatedOn { get; private set; }

    public string? DeactivatedBy { get; private set; }

    public DateTimeOffset? DeactivatedOn { get; private set; }

    public Auditable Created(string createdBy, DateTimeOffset createdOn)
    {
        CreatedBy = createdBy;
        CreatedOn = createdOn;

        return this;
    }

    public Auditable Updated(string updatedBy, DateTimeOffset updatedOn)
    {
        UpdatedBy = updatedBy;
        UpdatedOn = updatedOn;

        return this;
    }

    public Auditable Deactivated(string deactivatedBy, DateTimeOffset deactivatedOn)
    {
        DeactivatedBy = deactivatedBy;
        DeactivatedOn = deactivatedOn;
        IsActive = false;

        return this;
    }
}