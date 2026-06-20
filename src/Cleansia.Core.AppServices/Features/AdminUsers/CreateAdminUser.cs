using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using static Cleansia.Core.AppServices.Common.Validators.ValidationExtensions;

namespace Cleansia.Core.AppServices.Features.AdminUsers;

public class CreateAdminUser
{
    public record Command(
        string Email,
        string Password,
        string FirstName,
        string LastName,
        string? PhoneNumber,
        DateOnly? BirthDate,
        string? PreferredLanguageCode) : ICommand<Response>;

    public record Response(string Id);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IUserRepository userRepository, ILanguageRepository languageRepository)
        {
            RuleFor(x => x.Email)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .EmailAddress()
                .WithMessage(BusinessErrorMessage.InvalidEmailFormat)
                .MaximumLength(150)
                .WithMessage(BusinessErrorMessage.MaxLength)
                .MustAsync(async (email, ct) =>
                    !await userRepository.GetAll()
                        .AnyAsync(u => u.Email.ToLower() == email.ToLower(), ct))
                .WithMessage(BusinessErrorMessage.AdminUserEmailExists);

            RuleFor(x => x.Password).ValidatePassword();

            RuleFor(x => x.FirstName)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(50)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.LastName)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(50)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.PhoneNumber)
                .MaximumLength(50)
                .WithMessage(BusinessErrorMessage.MaxLength)
                .When(x => !string.IsNullOrEmpty(x.PhoneNumber));

            RuleFor(x => x.BirthDate)
                .Cascade(CascadeMode.Stop)
                .Must(date => BeInPast(date!.Value))
                .WithMessage(BusinessErrorMessage.DateMustBeInPast)
                .Must(date => BeReasonableAge(date!.Value))
                .WithMessage(BusinessErrorMessage.InvalidAge)
                .When(x => x.BirthDate.HasValue);

            RuleFor(x => x.PreferredLanguageCode)
                .MustAsync(async (code, ct) => await languageRepository.ExistsWithCodeAsync(code!, ct))
                .WithMessage(BusinessErrorMessage.LanguageNotSupported)
                .When(x => !string.IsNullOrWhiteSpace(x.PreferredLanguageCode));
        }
    }

    internal class Handler(IUserRepository userRepository)
        : ICommandHandler<Command, Response>
    {
        public Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            // Pass the RAW password — the EF PasswordConverter hashes exactly once on
            // persist (matching Register.cs / RegisterEmployee.cs). Pre-hashing here caused
            // hash(hash(password)), silently locking new admins out of login.
            var user = User.CreateWithPassword(
                email: command.Email,
                password: command.Password,
                firstName: command.FirstName,
                lastName: command.LastName,
                profile: UserProfile.Administrator,
                languageCode: command.PreferredLanguageCode);

            user.ConfirmEmail();
            user.UpdateBirthDate(command.BirthDate);

            if (!string.IsNullOrWhiteSpace(command.PhoneNumber))
            {
                user.UpdatePhoneNumber(command.PhoneNumber);
            }

            userRepository.Add(user);

            return Task.FromResult(BusinessResult.Success(new Response(user.Id)));
        }
    }
}