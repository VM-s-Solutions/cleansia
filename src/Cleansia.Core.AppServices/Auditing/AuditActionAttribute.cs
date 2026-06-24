namespace Cleansia.Core.AppServices.Auditing;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AuditActionAttribute : Attribute
{
    public AuditActionAttribute(string? action = null)
    {
        Action = action;
    }

    public string? Action { get; }

    public bool Sensitive { get; init; }

    public string? ResourceType { get; init; }

    public bool Audited { get; init; } = true;
}
