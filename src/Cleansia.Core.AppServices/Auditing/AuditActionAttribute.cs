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

    /// <summary>
    /// Admin unless the marker says otherwise (ADR-0012 audits every admin mutation; a customer act is
    /// recorded only where a marker opts it in — ADR-0062 D1). A nullable enum is not a legal attribute
    /// argument type, so the default carries the meaning.
    /// </summary>
    public AuditAudience Audience { get; init; } = AuditAudience.Admin;

    /// <summary>
    /// Only a customer-audience marker reads this: an anonymous caller is recorded as a customer act
    /// only where the act genuinely has no session yet (registration, guest checkout).
    /// </summary>
    public bool AllowsAnonymousActor { get; init; }
}
