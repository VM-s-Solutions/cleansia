#nullable enable
using Cleansia.Core.AppServices.Shared.DTOs.Enums;

namespace Cleansia.Core.AppServices.Features.AdminUsers.DTOs;

public record AdminUserDetailDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    Code Profile,
    bool IsEmailConfirmed,
    bool IsActive,
    DateOnly? BirthDate,
    string? PreferredLanguageCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);