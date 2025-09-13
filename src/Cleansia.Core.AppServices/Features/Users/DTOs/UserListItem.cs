#nullable enable
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.AppServices.Shared.DTOs.Files;

namespace Cleansia.Core.AppServices.Features.Users.DTOs;

public record UserListItem(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    Code Profile,
    Code AuthenticationType,
    bool IsEmailConfirmed,
    DateOnly? BirthDate,
    BlobFile? ProfilePhoto);