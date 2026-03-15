using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth.Validators;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
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
        string Language)
        : ICommand<bool>;

    internal class Handler(
        IEmailService emailService,
        ICartRepository cartRepository,
        IUserRepository userRepository,
        IEmployeeRepository employeeRepository)
        : ICommandHandler<Command, bool>
    {
        public async Task<BusinessResult<bool>> Handle(Command command, CancellationToken cancellationToken)
        {
            var userEntity = await userRepository.GetByEmailAsync(command.Email, cancellationToken);
            if (userEntity is null)
            {
                userEntity = User.CreateWithPassword(command.Email, command.Password, command.FirstName, command.LastName, UserProfile.Employee, command.Language);
                userRepository.Add(userEntity);
                cartRepository.Add(Cart.CreateWithUser(userEntity));
                employeeRepository.Add(Employee.CreateWithUser(userEntity));
            }

            if (userEntity.Employee is null)
            {
                employeeRepository.Add(Employee.CreateWithUser(userEntity));
            }

            var userName = $"{userEntity.FirstName} {userEntity.LastName}";

            await emailService.SendEmailConfirmationAsync(userEntity.Email, userName, userEntity.ConfirmationCode!, command.Language, cancellationToken);

            return BusinessResult.Success(true);
        }
    }
}