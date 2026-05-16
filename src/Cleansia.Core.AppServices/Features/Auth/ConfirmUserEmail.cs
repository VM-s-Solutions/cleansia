using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Auth;

public class ConfirmUserEmail
{
    public class Validator : AbstractValidator<Command>
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<Validator> _logger;

        public Validator(IUserRepository userRepository, ILogger<Validator> logger)
        {
            _userRepository = userRepository;
            _logger = logger;

            RuleFor(command => command.Code)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .WithErrorCode(nameof(Command.Code))
                .MustAsync(ValidateUserTokenAsync)
                .WithMessage(BusinessErrorMessage.InvalidConfirmationCode)
                .WithErrorCode(nameof(Command.Code));
        }

        private async Task<bool> ValidateUserTokenAsync(Command command, string code, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByConfirmationCodeAsync(command.Code, cancellationToken);

            if (user is null)
            {
                _logger.LogWarning("Email confirmation failed: no user found with code {Code}", command.Code);
                return false;
            }

            if (!user.ConfirmationCodeExpiresAt.HasValue || DateTime.UtcNow >= user.ConfirmationCodeExpiresAt.Value)
            {
                _logger.LogWarning("Email confirmation failed for user {Email}: code expired at {ExpiresAt}, current time {Now}",
                    user.Email, user.ConfirmationCodeExpiresAt, DateTime.UtcNow);
                return false;
            }

            return true;
        }
    }

    public record Command(string Code) : ICommand<JwtTokenResponse>;

    public class Handler(
        ITokenService tokenService,
        IUserRepository userRepository,
        IHostAudienceProvider hostAudience) : ICommandHandler<Command, JwtTokenResponse>
    {
        public async Task<BusinessResult<JwtTokenResponse>> Handle(Command command, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByConfirmationCodeAsync(command.Code, cancellationToken);
            user!.ConfirmEmail();

            return BusinessResult.Success(await tokenService.GenerateTokenAsync(user, rememberMe: true, hostAudience.Audience, cancellationToken));
        }
    }
}