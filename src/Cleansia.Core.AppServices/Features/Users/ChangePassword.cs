using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Users;

public class ChangePassword
{
    public class Validator : AbstractValidator<Command>
    {
        private readonly IUserRepository _userRepository;

        public Validator(IUserRepository userRepository)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));

            const string passwordPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$";

            RuleFor(command => command.Email)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .WithErrorCode(nameof(Command.Email))
                .MustAsync(userRepository.ExistsWithEmailAsync)
                .WithErrorCode(nameof(Command.Email))
                .WithMessage(BusinessErrorMessage.NotExistingUserWithEmail);

            RuleFor(command => command.NewPassword)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .Matches(passwordPattern)
                .WithMessage(BusinessErrorMessage.InvalidPasswordFormat);

            RuleFor(command => command.Code)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);

            RuleFor(command => command)
                .Cascade(CascadeMode.Stop)
                .MustAsync(ValidateUserTokenAsync)
                    .WithErrorCode(nameof(Command.Code))
                    .WithMessage(BusinessErrorMessage.NotValidResetPasswordToken)
                .MustAsync(CheckIfPasswordDifferentAsync)
                    .WithErrorCode(nameof(Command.NewPassword))
                    .WithMessage(BusinessErrorMessage.SameResetPassword)
                .When(c => !string.IsNullOrWhiteSpace(c.Code) && !string.IsNullOrWhiteSpace(c.NewPassword))
                .WhenAsync((c, cc) => userRepository.ExistsWithEmailAsync(c.Email, cc));
        }

        private async Task<bool> ValidateUserTokenAsync(Command command, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByEmailAsync(command.Email, cancellationToken);

            return user is not null &&
                   user.ResetPasswordCode == command.Code &&
                   user.ResetPasswordCodeExpiresAt.HasValue &&
                   DateTime.UtcNow < user.ResetPasswordCodeExpiresAt.Value;
        }

        private async Task<bool> CheckIfPasswordDifferentAsync(Command command, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByEmailAsync(command.Email, cancellationToken);
            return user is not null && !command.NewPassword.CheckIfPasswordSame(user.Password!);
        }
    }

    public record Command(
        string Email,
        string NewPassword,
        string Code)
        : ICommand<Response>;

    public record Response(string Id);

    internal class Handler(
        IUserRepository userRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByEmailAsync(command.Email, cancellationToken);
            user!.UpdatePassword(command.NewPassword);
            user.ClearResetPasswordToken();

            return BusinessResult.Success(new Response(Id: user.Id));
        }
    }
}