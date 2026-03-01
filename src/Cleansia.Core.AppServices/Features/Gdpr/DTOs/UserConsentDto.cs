using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.Gdpr.DTOs;

public record UserConsentDto(
    string Id,
    ConsentType ConsentType,
    bool IsGranted,
    DateTimeOffset? GrantedAt,
    DateTimeOffset? WithdrawnAt,
    DateTimeOffset CreatedOn
);
