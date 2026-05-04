using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
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
    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Token)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .WithErrorCode(nameof(Command.Token));
        }
    }

    public record Command(string Token) : ICommand<bool>;

    internal class Handler(IRefreshTokenService refreshTokenService)
        : ICommandHandler<Command, bool>
    {
        public async Task<BusinessResult<bool>> Handle(Command command, CancellationToken cancellationToken)
        {
            await refreshTokenService.RevokeAsync(command.Token, reason: "logout", cancellationToken);
            return BusinessResult.Success(true);
        }
    }
}
