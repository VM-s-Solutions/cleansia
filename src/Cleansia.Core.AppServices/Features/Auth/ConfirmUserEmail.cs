using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Auth;

public class ConfirmUserEmail
{
    public class Validator : AbstractValidator<Command>
    {
        private readonly IUserRepository _userRepository;

        public Validator(IUserRepository userRepository)
        {
            _userRepository = userRepository;

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

            return user is not null &&
                   user.ConfirmationCode == command.Code &&
                   user.ConfirmationCodeExpiresAt.HasValue &&
                   DateTime.UtcNow < user.ConfirmationCodeExpiresAt.Value;
        }
    }

    public record Command(string Code) : ICommand<JwtTokenResponse>;

    public class Handler(
        ITokenService tokenService,
        IUserRepository userRepository) : ICommandHandler<Command, JwtTokenResponse>
    {
        public async Task<BusinessResult<JwtTokenResponse>> Handle(Command command, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByConfirmationCodeAsync(command.Code, cancellationToken);
            user!.ConfirmEmail();

            return BusinessResult.Success(tokenService.GenerateToken(user, rememberMe: true));
        }
    }
}