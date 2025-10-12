using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Users;

public class RequestPasswordChange
{
    public class Validator : AbstractValidator<Command>
    {
        public Validator(IUserRepository userRepository)
        {
            RuleFor(command => command.Email)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .WithErrorCode(nameof(Command.Email))
                .MustAsync(userRepository.ExistsWithEmailAsync)
                .WithErrorCode(nameof(Command.Email))
                .WithMessage(BusinessErrorMessage.NotExistingUserWithEmail);
        }
    }

    public record Command(
        string Email)
        : ICommand;

    internal class Handler(
        IEmailService emailService,
        IUserRepository userRepository)
        : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command command, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByEmailAsync(command.Email, cancellationToken);
            user!.UpdateResetPasswordToken();

            await userRepository.CommitAsync(cancellationToken);
            await emailService.SendResetPasswordEmailAsync(command.Email, $"{user.LastName} {user.FirstName}", user!.ResetPasswordCode!, cancellationToken);

            return BusinessResult.Success();
        }
    }
}