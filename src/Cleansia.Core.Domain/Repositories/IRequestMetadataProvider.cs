namespace Cleansia.Core.Domain.Repositories;

/// <summary>
/// Surfaces best-effort request metadata (client IP, device label) to non-controller
/// code (handlers, services) without forcing them to depend on HttpContext directly.
/// Both properties return null when called outside a request scope (e.g. background services).
/// </summary>
public interface IRequestMetadataProvider
{
    /// <summary>Client IP from the current request, or null if unavailable.</summary>
    string? IpAddress { get; }

    /// <summary>
    /// Best-effort human-readable device label. For web clients this is derived from
    /// the User-Agent header. For mobile clients, the app sends a custom
    /// <c>X-Device-Label</c> header which takes priority.
    /// </summary>
    string? DeviceLabel { get; }
}
