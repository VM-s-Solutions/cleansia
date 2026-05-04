using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.AppServices.Features.Auth.Validators;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Auth;

public class Register
{
    public class Validator : BaseAuthValidator<Command>
    {
        private readonly IUserRepository _userRepository;

        public Validator(
            IUserRepository userRepository,
            ILanguageRepository languageRepository)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));

            AddEmailRules(command => command.Email);
            AddFirstNameRules(command => command.FirstName);
            AddLastNameRules(command => command.LastName);
            AddPasswordRules(command => command.Password);

            RuleFor(user => user.Email)
                .MustAsync(UserWithEmailNotExistsAsync)
                .WithMessage(BusinessErrorMessage.ExistingUserWithEmail)
                .WithErrorCode(nameof(Command.Email));

            RuleFor(user => user.Language)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .WithMessage(BusinessErrorMessage.Required)
                .WithErrorCode(nameof(Command.Language))
                .SetValidator(new LanguageValidator(languageRepository));
        }

        private Task<bool> UserWithEmailNotExistsAsync(string email, CancellationToken cancellationToken) =>
            _userRepository.GetByEmailAsync(email, cancellationToken)
                .ContinueWith(t => t.Result is null || (t.Result is not null && !t.Result.IsEmailConfirmed),
                    cancellationToken);
    }

    public record Command(
        string Email,
        string Password,
        string FirstName,
        string LastName,
        string Language,
        // Optional referral code entered by the customer at signup. Empty
        // when the user signed up directly. Validated + accepted server-side
        // after the user is created — bad codes do NOT block registration.
        string? ReferralCode = null)
        : ICommand<bool>;

    internal class Handler(
        IEmailService emailService,
        ICartRepository cartRepository,
        IUserRepository userRepository,
        IReferralService referralService,
        ILogger<Handler> logger)
        : ICommandHandler<Command, bool>
    {
        public async Task<BusinessResult<bool>> Handle(Command command, CancellationToken cancellationToken)
        {
            var userEntity = await userRepository.GetByEmailAsync(command.Email, cancellationToken);
            if (userEntity is null)
            {
                userEntity = User.CreateWithPassword(command.Email, command.Password, command.FirstName, command.LastName, UserProfile.Customer, command.Language);
                userRepository.Add(userEntity);
                cartRepository.Add(Cart.CreateWithUser(userEntity));
            }
            else
            {
                // Re-registration: user exists but hasn't confirmed — refresh the code
                userEntity.UpdateConfirmationCode();
            }

            var userName = $"{userEntity.FirstName} {userEntity.LastName}";

            await emailService.SendEmailConfirmationAsync(userEntity.Email, userName, userEntity.ConfirmationCode!, command.Language, cancellationToken);

            // Referral acceptance is fail-soft: a bad code (typo, expired,
            // self-referral) must NOT block account creation. The user can
            // re-enter at first booking via CreateOrder's late-acceptance.
            if (!string.IsNullOrWhiteSpace(command.ReferralCode))
            {
                try
                {
                    var acceptResult = await referralService.AcceptAsync(
                        command.ReferralCode, userEntity.Id, cancellationToken);
                    if (!acceptResult.IsAccepted)
                    {
                        logger.LogInformation(
                            "Referral code {Code} not accepted for new user {UserId}: {Error}",
                            command.ReferralCode, userEntity.Id, acceptResult.Error);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Failed to accept referral code {Code} for user {UserId}",
                        command.ReferralCode, userEntity.Id);
                }
            }

            return BusinessResult.Success(true);
        }
    }
}