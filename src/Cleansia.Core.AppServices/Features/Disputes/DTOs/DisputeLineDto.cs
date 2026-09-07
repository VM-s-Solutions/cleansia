namespace Cleansia.Core.AppServices.Features.Disputes.DTOs;

/// <summary>
/// One order item the customer named as unsatisfactory.
///
/// <para>Carries the display name as well as the ids, because the admin resolving the dispute needs
/// to read "Oven cleaning" rather than a ULID — and because the catalogue name can move after the
/// order, the id is what identifies the line and the name is what explains it.</para>
/// </summary>
public record DisputeLineDto(
    string ServiceId,
    string ServiceName,
    /// <summary>Null when the service was bought on its own rather than inside a package.</summary>
    string? PackageId,
    string? PackageName);
