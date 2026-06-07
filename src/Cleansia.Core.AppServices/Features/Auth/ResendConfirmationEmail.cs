using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Auth;

public class ResendConfirmationEmail
{
    public class Validator : AbstractValidator<Command>
    {
        private readonly IUserRepository _userRepository;

        public Validator(
            IUserRepository userRepository,
            ILanguageRepository languageRepository)
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

            RuleFor(command => command.Language)
                .SetValidator(new LanguageValidator(languageRepository));
        }

        private async Task<bool> HasUnconfirmedEmailAsync(string email, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
            return user?.IsEmailConfirmed == false;
        }
    }

    public record Command(string Email, string Language) : ICommand<bool>;

    public class Handler(
        IUserRepository userRepository,
        IPendingDispatch pending) : ICommandHandler<Command, bool>
    {
        public async Task<BusinessResult<bool>> Handle(Command command, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByEmailAsync(command.Email, cancellationToken);
            var userName = $"{user!.FirstName} {user.LastName}";
            // Email the RAW token returned by the generator; the row keeps the hash.
            var rawConfirmationToken = user.UpdateConfirmationCode();

            EmailDispatch.EnqueueConfirmation(pending, user, userName, rawConfirmationToken, command.Language);

            return BusinessResult.Success(true);
        }
    }
}