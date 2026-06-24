using System.Reflection;

namespace Cleansia.Core.AppServices.Auditing;

/// <summary>
/// ADR-0012 D5.1 — best-effort, convention-based read of the affected aggregate id off a command.
/// Pure and reflective: tries a <c>{ResourceType}Id</c> property (when the marker named a resource
/// type), then a conventional <c>Id</c>, then the single <c>*Id</c> string/Ulid-shaped property. Returns
/// null when nothing conventional resolves — the audit row is still written (the resource id is
/// nullable in the contract).
/// </summary>
public static class AuditResourceResolver
{
    public static string? ResolveResourceId(object request, string? resourceType)
    {
        var type = request.GetType();

        if (!string.IsNullOrWhiteSpace(resourceType))
        {
            var typed = ReadString(request, type.GetProperty($"{resourceType}Id", BindingFlags.Public | BindingFlags.Instance));
            if (typed is not null)
            {
                return typed;
            }
        }

        var idProperty = type.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        var direct = ReadString(request, idProperty);
        if (direct is not null)
        {
            return direct;
        }

        var idLike = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name.EndsWith("Id", StringComparison.Ordinal) && p.PropertyType == typeof(string))
            .ToArray();

        return idLike.Length == 1 ? ReadString(request, idLike[0]) : null;
    }

    private static string? ReadString(object request, PropertyInfo? property)
    {
        if (property is null)
        {
            return null;
        }

        var value = property.GetValue(request)?.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
