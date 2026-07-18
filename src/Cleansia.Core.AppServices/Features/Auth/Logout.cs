using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Auth;

/// <summary>
/// Revokes a refresh token. Idempotent: unknown or already-revoked tokens
/// are silently treated as success (prevents token-probing attacks and makes
/// logout safe to retry).
/// </summary>
public class Logout
{
    // Validator intentionally has no rules: web clients now send an empty
    // body and the controller fills `Token` from the HttpOnly cookie. If
    // the cookie is also missing (already-expired session) the handler
    // treats it as a no-op success — logout is idempotent.
    public class Validator : AbstractValidator<Command> { }

    public record Command(string Token) : ICommand<bool>;

    internal class Handler(
        IRefreshTokenService refreshTokenService,
        IUserSessionProvider userSessionProvider)
        : ICommandHandler<Command, bool>
    {
        public async Task<BusinessResult<bool>> Handle(Command command, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(command.Token))
            {
                // The caller id gates the rotated-token successor walk to the token's own
                // account (host-agnostic ownership check; the endpoint is [Authorize]).
                await refreshTokenService.RevokeAsync(
                    command.Token, reason: "logout", cancellationToken, userSessionProvider.GetUserId());
            }
            return BusinessResult.Success(true);
        }
    }
}
