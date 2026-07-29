using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Users;

public class RequestPasswordChange
{
    public class Validator : AbstractValidator<Command>
    {
        // The auth-type rule cannot pick its message up front: the provider is only known after the async
        // user read inside the predicate. So the predicate hands the resolved message key to the rule
        // through the MessageFormatter, and the rule's template is nothing but this placeholder.
        private const string AuthTypeErrorPlaceholder = "AuthTypeError";
        private const string AuthTypeErrorTemplate = "{" + AuthTypeErrorPlaceholder + "}";

        private readonly IUserRepository _userRepository;

        public Validator(IUserRepository userRepository)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));

            RuleFor(command => command.Email)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .WithErrorCode(nameof(Command.Email))
                // Anonymous forgot-password request — the IgnoringTenant read reaches
                // tenant-stamped accounts the ambient-null tenant filter would hide.
                .MustAsync(userRepository.ExistsWithEmailIgnoringTenantAsync)
                .WithErrorCode(nameof(Command.Email))
                .WithMessage(BusinessErrorMessage.NotExistingUserWithEmail)
                .MustAsync((_, email, context, cancellationToken) => UserAuthenticationTypeIsInternal(email, context, cancellationToken))
                .WithErrorCode(nameof(Command.Email))
                .WithMessage(AuthTypeErrorTemplate);
        }

        // Only an Internal account has a password to recover: LoginValidator refuses a password login for
        // every other type, so mailing a reset code to a Google/Apple row can only end in a credential
        // that never works. Refuse at the source, naming the provider the account ACTUALLY uses so the
        // caller is told how to sign in. No new disclosure — the preceding existence rule already stops
        // the cascade for an unknown email, so the provider is only named for a known address.
        private async Task<bool> UserAuthenticationTypeIsInternal(
            string email, ValidationContext<Command> context, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByEmailIgnoringTenantAsync(email, cancellationToken);
            if (user is not null && user.AuthenticationType == AuthenticationType.Internal)
            {
                return true;
            }

            // user is null only if the account disappeared between the existence rule and this one; reuse
            // that rule's message rather than inventing a provider for an account that is not there.
            context.MessageFormatter.AppendArgument(
                AuthTypeErrorPlaceholder,
                user is null ? BusinessErrorMessage.NotExistingUserWithEmail : AuthTypeErrorMessages.For(user.AuthenticationType));

            return false;
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
            var user = await userRepository.GetByEmailIgnoringTenantAsync(command.Email, cancellationToken);
            // email the RAW reset token returned by the generator; the row keeps
            // only the hash (never read the persisted hashed column back into the email).
            var rawResetToken = user!.UpdateResetPasswordToken();

            var languageCode = user.PreferredLanguageCode ?? command.Language;
            EmailDispatch.EnqueuePasswordReset(pending, user, $"{user.LastName} {user.FirstName}", rawResetToken, languageCode);

            return BusinessResult.Success();
        }
    }
}