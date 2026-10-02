#nullable enable
namespace Cleansia.Core.AppServices.Features.Users.DTOs;

public record AdminCustomerListItem(
    string Id,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    bool IsActive,
    bool IsEmailConfirmed,
    DateTimeOffset CreatedOn);
