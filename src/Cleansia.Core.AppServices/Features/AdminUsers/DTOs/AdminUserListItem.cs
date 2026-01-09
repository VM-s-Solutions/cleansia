#nullable enable
using Cleansia.Core.AppServices.Shared.DTOs.Enums;

namespace Cleansia.Core.AppServices.Features.AdminUsers.DTOs;

public record AdminUserListItem(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    Code Profile,
    bool IsEmailConfirmed,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);