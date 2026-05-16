using Cleansia.Core.AppServices.Features.Users.DTOs;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.AppServices.Mappers;

public static class UserMappers
{
    public static UserListItem? MapToDto(this User? user)
    {
        return user is null
            ? null
            : new UserListItem(
                Id: user.Id,
                Email: user.Email,
                FirstName: user.FirstName,
                LastName: user.LastName,
                PhoneNumber: user.PhoneNumber,
                Profile: user.Profile.MapToCode(),
                AuthenticationType: user.AuthenticationType.MapToCode(),
                IsEmailConfirmed: user.IsEmailConfirmed,
                BirthDate: user.BirthDate,
                ProfilePhoto: user.ProfilePhotoName?.MapToDto(),
                PreferredLanguageCode: user.PreferredLanguageCode,
                PreferredLanguageName: user.PreferredLanguage?.Name);
    }

    public static MyProfileDto? MapToMyProfileDto(this User? user)
    {
        return user is null
            ? null
            : new MyProfileDto(
                Email: user.Email,
                FirstName: user.FirstName,
                LastName: user.LastName,
                PhoneNumber: user.PhoneNumber,
                Profile: user.Profile.MapToCode(),
                AuthenticationType: user.AuthenticationType.MapToCode(),
                IsEmailConfirmed: user.IsEmailConfirmed,
                BirthDate: user.BirthDate,
                ProfilePhoto: user.ProfilePhotoName?.MapToDto(),
                PreferredLanguageCode: user.PreferredLanguageCode,
                PreferredLanguageName: user.PreferredLanguage?.Name);
    }

    public static UserItem? MapToDetailDto(this User? user)
    {
        return user is null
            ? null
            : new UserItem(
                Email: user.Email,
                FirstName: user.FirstName,
                LastName: user.LastName,
                PhoneNumber: user.PhoneNumber,
                Profile: user.Profile.MapToCode(),
                AuthenticationType: user.AuthenticationType.MapToCode(),
                IsEmailConfirmed: user.IsEmailConfirmed,
                BirthDate: user.BirthDate,
                ProfilePhoto: user.ProfilePhotoName?.MapToDto(),
                PreferredLanguageCode: user.PreferredLanguageCode,
                PreferredLanguageName: user.PreferredLanguage?.Name,
                Id: user.Id,
                IsActive: user.IsActive);
    }
}