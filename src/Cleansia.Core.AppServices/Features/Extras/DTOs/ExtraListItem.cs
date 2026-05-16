using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.AppServices.Features.Extras.DTOs;

/// <summary>
/// Customer-facing add-on. Returned by the anonymous
/// <c>ExtraController.GetOverview</c> endpoint and consumed by the booking
/// wizard (web + mobile) to render the extras toggle list.
///
/// <see cref="Slug"/> is the stable per-tenant identifier the client uses
/// for icon/copy lookups; <see cref="Translations"/> mirrors the same
/// fallback-then-localize pattern the services/packages overview uses.
/// </summary>
public record ExtraListItem(
    string Id,
    string Slug,
    string Name,
    string? Description,
    decimal Price,
    int DisplayOrder,
    Dictionary<string, Translation> Translations);
