using System.Reflection;

namespace Cleansia.Core.AppServices.Auditing;

/// <summary>
/// ADR-0012 D5/D5.1 — the rename-proof label + resource-type + audited/sensitive flags for a command
/// type, resolved once from its <c>[AuditAction]</c> marker (frozen) or, when unmarked, from the
/// normalized type name. Pure: no session, no domain state. The normalized name strips a trailing
/// <c>Command</c> and unwraps a nested <c>Command</c> record to its declaring type
/// (<c>AdminRefundOrder.Command</c> -&gt; <c>AdminRefundOrder</c>).
/// </summary>
public sealed record AuditActionDescriptor(string Action, string? ResourceType, bool Sensitive, bool Audited)
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
            marker?.Audited ?? true);
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
