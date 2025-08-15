using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Auth;

public class ResendConfirmationEmail
{
    public class Validator : AbstractValidator<Command>
    {
        private readonly IUserRepository _userRepository;

        public Validator(IUserRepository userRepository)
        {
            _userRepository = userRepository;

            RuleFor(command => command.Email)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .WithErrorCode(nameof(Command.Email))
                .MustAsync(userRepository.ExistsWithEmailAsync)
                .WithMessage(BusinessErrorMessage.NotExistingUserWithEmail)
                .WithErrorCode(nameof(Command.Email))
                .MustAsync(HasUnconfirmedEmailAsync)
                .WithMessage(BusinessErrorMessage.EmailConfirmed)
                .WithErrorCode(nameof(Command.Email));
        }

        private Task<bool> HasUnconfirmedEmailAsync(string email, CancellationToken cancellationToken) => _userRepository
            .GetByEmailAsync(email, cancellationToken)
            .ContinueWith(userTask => userTask.Result?.IsEmailConfirmed == false, cancellationToken);
    }

    public record Command(string Email) : ICommand<bool>;

    public class Handler(
        IEmailService emailService,
        IUserRepository userRepository) : ICommandHandler<Command, bool>
    {
        public async Task<BusinessResult<bool>> Handle(Command command, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByEmailAsync(command.Email, cancellationToken);
            var userName = $"{user!.FirstName} {user.LastName}";
            user.UpdateConfirmationCode();
            await emailService.SendEmailConfirmationAsync(command.Email, userName, user!.ConfirmationCode, cancellationToken);

            return BusinessResult.Success(true);
        }
    }
}