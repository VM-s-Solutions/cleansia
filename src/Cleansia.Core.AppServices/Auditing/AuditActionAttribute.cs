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

    /// <summary>
    /// The command property holding the resource id when its wire name is not <c>{ResourceType}Id</c>
    /// (a schedule command says <c>TemplateId</c>, and renaming that is a client contract change). Read
    /// by the customer arm's exact resolver only, so a refused edit still records WHICH resource was
    /// probed.
    /// </summary>
    public string? ResourceIdProperty { get; init; }

    public bool Audited { get; init; } = true;

    /// <summary>
    /// Admin unless the marker says otherwise (ADR-0012 audits every admin mutation; a customer act is
    /// recorded only where a marker opts it in — ADR-0062 D1). A nullable enum is not a legal attribute
    /// argument type, so the default carries the meaning.
    /// </summary>
    public AuditAudience Audience { get; init; } = AuditAudience.Admin;

    /// <summary>
    /// Only a customer-audience marker reads this: an anonymous caller is recorded as a customer act
    /// only where the act genuinely has no session yet (registration, guest checkout, the session acts
    /// that open or recover one) — and only on a customer host (<c>AuditGate</c>). The command must be
    /// <c>IOperatorScopedRequest</c>, or its refusal rows have no tenant to be stamped with.
    /// </summary>
    public bool AllowsAnonymousActor { get; init; }
}
