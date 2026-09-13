using System.Reflection;

namespace Cleansia.Core.AppServices.Auditing;

/// <summary>
/// ADR-0012 D5.1 — best-effort, convention-based read of the affected aggregate id off a command.
/// Pure and reflective: tries a <c>{ResourceType}Id</c> property (when the marker named a resource
/// type), then a conventional <c>Id</c>, then the single <c>*Id</c> string/Ulid-shaped property. Returns
/// null when nothing conventional resolves — the audit row is still written (the resource id is
/// nullable in the contract).
///
/// <para><see cref="ResolveExact"/> is the customer arm's read (ADR-0062 D1): the <c>{ResourceType}Id</c>
/// property only, or the one property the marker named in its place. The two fallbacks would label
/// <c>CreateMembershipCheckoutSession.Command</c>'s <c>CountryId</c> a membership and
/// <c>CreateDispute.Command</c>'s <c>OrderId</c> a dispute — a wrong id on an evidence row is worse
/// than none.</para>
/// </summary>
public static class AuditResourceResolver
{
    public static string? ResolveResourceId(object request, string? resourceType)
    {
        var type = request.GetType();

        var typed = ResolveExact(request, resourceType);
        if (typed is not null)
        {
            return typed;
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

    public static string? ResolveExact(object request, string? resourceType, string? resourceIdProperty = null)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
        {
            return null;
        }

        var propertyName = string.IsNullOrWhiteSpace(resourceIdProperty) ? $"{resourceType}Id" : resourceIdProperty;
        return ReadString(request, request.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance));
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
