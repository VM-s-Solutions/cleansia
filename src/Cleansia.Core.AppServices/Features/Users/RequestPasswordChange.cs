using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
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
        string Email,
        string Language = Constants.Language.English)
        : ICommand;

    public class Handler(
        IUserRepository userRepository,
        IPendingDispatch pending)
        : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command command, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByEmailAsync(command.Email, cancellationToken);
            // email the RAW reset token returned by the generator; the row keeps
            // only the hash (never read the persisted hashed column back into the email).
            var rawResetToken = user!.UpdateResetPasswordToken();

            var languageCode = user.PreferredLanguageCode ?? command.Language;
            EmailDispatch.EnqueuePasswordReset(pending, user, $"{user.LastName} {user.FirstName}", rawResetToken, languageCode);

            return BusinessResult.Success();
        }
    }
}