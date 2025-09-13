#nullable enable
namespace Cleansia.Core.AppServices.Features.Users.Filters;

public record UserFilter(
    string? Id,
    bool? IsActive,
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    string? Email,
    int[]? UserProfiles,
    int[]? AuthenticationTypes);