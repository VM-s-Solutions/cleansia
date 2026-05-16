using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.AppServices.Services.Interfaces;

public interface ITokenService
{
    Task<JwtTokenResponse> GenerateTokenAsync(User user, bool rememberMe, string audience, CancellationToken cancellationToken = default);
}
