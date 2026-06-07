using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth.Validators;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Auth;

public class RegisterEmployee
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

        private async Task<bool> UserWithEmailNotExistsAsync(string email, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
            return user is null || !user.IsEmailConfirmed;
        }
    }

    public record Command(
        string Email,
        string Password,
        string FirstName,
        string LastName,
        string Language)
        : ICommand<bool>;

    public class Handler(
        ICartRepository cartRepository,
        IUserRepository userRepository,
        IEmployeeRepository employeeRepository,
        IPendingDispatch pending)
        : ICommandHandler<Command, bool>
    {
        public async Task<BusinessResult<bool>> Handle(Command command, CancellationToken cancellationToken)
        {
            var userEntity = await userRepository.GetByEmailAsync(command.Email, cancellationToken);
            // Email the RAW confirmation token; the entity persists only its hash.
            // New user -> raw from CreateWithPassword; existing unconfirmed user -> refresh to get a raw
            // token (the stored ConfirmationCode is a hash and cannot be emailed).
            string rawConfirmationToken;
            if (userEntity is null)
            {
                userEntity = User.CreateWithPassword(command.Email, command.Password, command.FirstName, command.LastName, UserProfile.Employee, command.Language);
                rawConfirmationToken = userEntity.RawConfirmationToken!;
                userRepository.Add(userEntity);
                cartRepository.Add(Cart.CreateWithUser(userEntity));
                employeeRepository.Add(Employee.CreateWithUser(userEntity));
            }
            else
            {
                rawConfirmationToken = userEntity.UpdateConfirmationCode();
            }

            if (userEntity.Employee is null)
            {
                employeeRepository.Add(Employee.CreateWithUser(userEntity));
            }

            var userName = $"{userEntity.FirstName} {userEntity.LastName}";

            EmailDispatch.EnqueueConfirmation(pending, userEntity, userName, rawConfirmationToken, command.Language);

            return BusinessResult.Success(true);
        }
    }
}