#nullable enable
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.AppServices.Shared.DTOs.Files;

namespace Cleansia.Core.AppServices.Features.Users.DTOs;

// `Id` was previously exposed on this self-profile DTO but the client doesn't
// need its own backend UserId (per the project rule). The session JWT carries
// it server-side wherever it's needed; surfacing it to the client added zero
// UX value and a small enumeration risk.
public record MyProfileDto(
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    Code Profile,
    Code AuthenticationType,
    bool IsEmailConfirmed,
    DateOnly? BirthDate,
    BlobFileDto? ProfilePhoto,
    string? PreferredLanguageCode,
    string? PreferredLanguageName);
