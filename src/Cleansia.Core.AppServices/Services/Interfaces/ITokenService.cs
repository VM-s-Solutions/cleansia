using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.AppServices.Services.Interfaces;

public interface ITokenService
{
    JwtTokenResponse GenerateToken(User user, bool rememberMe);
}