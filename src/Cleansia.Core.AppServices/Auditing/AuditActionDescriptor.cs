using System.Reflection;

namespace Cleansia.Core.AppServices.Auditing;

/// <summary>
/// ADR-0012 D5/D5.1 — the rename-proof label + resource-type + audited/sensitive flags for a command
/// type, resolved once from its <c>[AuditAction]</c> marker (frozen) or, when unmarked, from the
/// normalized type name. Pure: no session, no domain state. The normalized name strips a trailing
/// <c>Command</c> and unwraps a nested <c>Command</c> record to its declaring type
/// (<c>AdminRefundOrder.Command</c> -&gt; <c>AdminRefundOrder</c>). <see cref="Audience"/> and
/// <see cref="AllowsAnonymousActor"/> are copied from the marker (ADR-0062 D1), as is
/// <see cref="ResourceIdProperty"/>; an unmarked command is an admin-audience one.
/// </summary>
public sealed record AuditActionDescriptor(
    string Action,
    string? ResourceType,
    bool Sensitive,
    bool Audited,
    AuditAudience Audience = AuditAudience.Admin,
    bool AllowsAnonymousActor = false,
    string? ResourceIdProperty = null)
{
    public static AuditActionDescriptor For(Type requestType)
    {
        var marker = requestType.GetCustomAttribute<AuditActionAttribute>(inherit: false)
            ?? requestType.DeclaringType?.GetCustomAttribute<AuditActionAttribute>(inherit: false);

        var label = string.IsNullOrWhiteSpace(marker?.Action)
            ? NormalizeTypeName(requestType)
            : marker!.Action!;

        return new AuditActionDescriptor(
            label,
            marker?.ResourceType,
            marker?.Sensitive ?? false,
            marker?.Audited ?? true,
            marker?.Audience ?? AuditAudience.Admin,
            marker?.AllowsAnonymousActor ?? false,
            marker?.ResourceIdProperty);
    }

    private static string NormalizeTypeName(Type requestType)
    {
        var name = requestType.Name;
        if (name == "Command" && requestType.DeclaringType is not null)
        {
            name = requestType.DeclaringType.Name;
        }

        return name.EndsWith("Command", StringComparison.Ordinal)
            ? name[..^"Command".Length]
            : name;
    }
}
