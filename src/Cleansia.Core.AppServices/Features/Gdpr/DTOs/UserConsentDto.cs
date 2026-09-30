using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.Gdpr.DTOs;

/// <summary>
/// A consent row, read-only. <see cref="DocumentVersion"/> is the version of the text accepted, and
/// <see cref="CoversCurrentVersion"/> says whether that acceptance is of the very text in force in the
/// default market: false once another text is in force — a newer version, or a market's own copy — and
/// false when withdrawn. A consent type with no text covers while it is granted.
/// </summary>
public record UserConsentDto(
    string Id,
    ConsentType ConsentType,
    bool IsGranted,
    DateTimeOffset? GrantedAt,
    DateTimeOffset? WithdrawnAt,
    DateTimeOffset CreatedOn,
    string? DocumentVersion,
    bool CoversCurrentVersion
);
