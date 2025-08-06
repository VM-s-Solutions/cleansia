using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth.Validators;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Google.Apis.Auth;

namespace Cleansia.Core.AppServices.Features.Auth;

public class GoogleAuth
{
    public class Validator : BaseAuthValidator<Command>
    {
        private readonly IGoogleConfig _googleConfig;
        private readonly IUserRepository _userRepository;

        public Validator(IGoogleConfig googleConfig, IUserRepository userRepository)
        {
            _googleConfig = googleConfig ?? throw new ArgumentNullException(nameof(googleConfig));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));

            AddEmailRules(command => command.Email);
            AddFirstNameRules(command => command.FirstName);
            AddLastNameRules(command => command.LastName);

            RuleFor(command => command.Token)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .WithErrorCode(nameof(Command.Token))
                .MustAsync(ValidateGoogleUserAsync)
                .WithMessage(BusinessErrorMessage.InvalidGoogleUserToken)
                .WithErrorCode(nameof(Command.Token));

            RuleFor(command => command.GoogleId)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .WithErrorCode(nameof(Command.GoogleId));

            RuleFor(user => user.Email)
                .MustAsync(UserAuthenticationTypeIsGoogle)
                .WithMessage(BusinessErrorMessage.InternalAuthTypeError)
                .WithErrorCode(nameof(Command.Email));
        }

        private async Task<bool> UserAuthenticationTypeIsGoogle(string email, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
            return user is null || user.AuthenticationType == AuthenticationType.Google;
        }

        private async Task<bool> ValidateGoogleUserAsync(string token, CancellationToken cancellationToken)
        {
            try
            {
                if (_googleConfig.IsDevelopment)
                {
                    return true;
                }

                await GoogleJsonWebSignature.ValidateAsync(token, new GoogleJsonWebSignature.ValidationSettings());
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public record Command(
        string Token,
        string GoogleId,
        string Email,
        string FirstName,
        string LastName)
        : ICommand<JwtTokenResponse>;

    internal class Handler(
        ITokenService tokenService,
        ICartRepository cartRepository,
        IUserRepository userRepository)
        : ICommandHandler<Command, JwtTokenResponse>
    {
        public async Task<BusinessResult<JwtTokenResponse>> Handle(Command command, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByEmailAsync(command.Email, cancellationToken);
            if (user is not null)
            {
                return BusinessResult.Success(tokenService.GenerateToken(user, rememberMe: true));
            }

            var userEntity = User.CreateWithGoogle(command.Email, command.FirstName, command.LastName, command.GoogleId);

            userRepository.Add(userEntity);
            cartRepository.Add(Cart.CreateWithUser(userEntity.Id));

            return BusinessResult.Success(tokenService.GenerateToken(userEntity, rememberMe: true));
        }
    }
}