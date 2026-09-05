using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.AppServices.Features.Packages.DTOs;

public record PackageListItem(
    string Id,
    string Name,
    string Description,
    string? Tagline,
    bool IsPopular,
    decimal Price,
    Dictionary<string, Translation> Translations,
    IEnumerable<PackageServiceSummary> IncludedServices);

/// <param name="ServiceId">
/// The service's own id, so a client can NAME this item back to the server.
///
/// <para>A dispute line and a review line-score both identify an item as
/// <c>(ServiceId, PackageId?)</c> — a service inside a bundle carries both. Without this the summary
/// could only be printed, never selected, and a customer could say a package went wrong but not which
/// part of it. → CreateDispute.DisputeLineSelection</para>
/// </param>
public record PackageServiceSummary(
    string ServiceId,
    string Name,
    Dictionary<string, Translation> Translations);