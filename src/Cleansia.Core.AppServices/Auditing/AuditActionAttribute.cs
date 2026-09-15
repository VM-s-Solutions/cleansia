namespace Cleansia.Core.AppServices.Auditing;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AuditActionAttribute : Attribute
{
    public AuditActionAttribute(string? action = null)
    {
        Action = action;
    }

    public string? Action { get; }

    /// <summary>
    /// The label the ADMIN arm writes when an Administrator runs a customer-audience command: the same
    /// act by an administrator is an admin act and is read under an admin label (a sign-out on the admin
    /// host is <c>admin.session.logout</c>, not a customer's). Null keeps <see cref="Action"/> on both
    /// arms. An admin-audience marker never reads it — its one label is already the admin one.
    /// </summary>
    public string? AdminAction { get; init; }

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
    /// An anonymous caller is recorded only where the act genuinely has no session yet (registration,
    /// guest checkout, the session acts that open or recover one) — and only on the host that serves the
    /// marker's audience (<c>AuditGate</c>): a customer-audience marker on a customer host, an
    /// admin-audience marker (the admin sign-in) on the admin host. The command must be
    /// <c>IOperatorScopedRequest</c>, or its refusal rows have no tenant to be stamped with.
    /// </summary>
    public bool AllowsAnonymousActor { get; init; }
}
