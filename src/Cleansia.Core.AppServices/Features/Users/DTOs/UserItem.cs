#nullable enable
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.Common;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.AppServices.Shared.DTOs.Files;

namespace Cleansia.Core.AppServices.Features.Users.DTOs;

public record UserItem(
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    Code Profile,
    Code AuthenticationType,
    bool IsEmailConfirmed,
    DateOnly? BirthDate,
    IEnumerable<OrderListItem> Orders,
    BlobFileDto? ProfilePhoto,
    string? PreferredLanguageCode,
    string? PreferredLanguageName,
    string Id,
    bool IsActive)
    : BaseRecord<string>(Id, IsActive);