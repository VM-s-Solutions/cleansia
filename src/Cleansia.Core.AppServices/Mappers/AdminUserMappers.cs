using Cleansia.Core.AppServices.Features.AdminUsers.DTOs;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.AppServices.Mappers;

public static class AdminUserMappers
{
    public static AdminUserListItem? MapToAdminListItem(this User? user)
    {
        return user is null
            ? null
            : new AdminUserListItem(
                Id: user.Id,
                Email: user.Email,
                FirstName: user.FirstName,
                LastName: user.LastName,
                PhoneNumber: user.PhoneNumber,
                Profile: user.Profile.MapToCode(),
                IsEmailConfirmed: user.IsEmailConfirmed,
                IsActive: user.IsActive,
                CreatedAt: user.CreatedOn,
                LastLoginAt: user.LastLoginAt);
    }

    public static AdminUserDetailDto? MapToAdminDetailDto(this User? user)
    {
        return user is null
            ? null
            : new AdminUserDetailDto(
                Id: user.Id,
                Email: user.Email,
                FirstName: user.FirstName,
                LastName: user.LastName,
                PhoneNumber: user.PhoneNumber,
                Profile: user.Profile.MapToCode(),
                IsEmailConfirmed: user.IsEmailConfirmed,
                IsActive: user.IsActive,
                BirthDate: user.BirthDate,
                PreferredLanguageCode: user.PreferredLanguageCode,
                CreatedAt: user.CreatedOn,
                LastLoginAt: user.LastLoginAt);
    }
}