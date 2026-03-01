namespace Cleansia.Core.AppServices.Features.FeatureFlags.DTOs;

public record FeatureFlagDto(
    string Id,
    string Name,
    string? Description,
    bool IsEnabled,
    string Scope,
    string? ScopeValue,
    DateTimeOffset CreatedOn,
    DateTimeOffset? UpdatedOn);
