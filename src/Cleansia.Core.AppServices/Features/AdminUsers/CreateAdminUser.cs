using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Extensions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.AdminUsers;

public class CreateAdminUser
{
    public record Command(
        string Email,
        string Password,
        string FirstName,
        string LastName,
        string? PhoneNumber) : ICommand<Response>;

    public record Response(string Id);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IUserRepository userRepository)
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

            RuleFor(x => x.Password)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MinimumLength(8)
                .WithMessage(BusinessErrorMessage.InvalidPasswordFormat);

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
        }
    }

    internal class Handler(IUserRepository userRepository)
        : ICommandHandler<Command, Response>
    {
        public Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var hashedPassword = command.Password.HashAndSaltPassword();

            var user = User.CreateWithPassword(
                email: command.Email,
                password: hashedPassword,
                firstName: command.FirstName,
                lastName: command.LastName,
                profile: UserProfile.Administrator);

            user.ConfirmEmail();

            if (!string.IsNullOrWhiteSpace(command.PhoneNumber))
            {
                user.UpdatePhoneNumber(command.PhoneNumber);
            }

            userRepository.Add(user);

            return Task.FromResult(BusinessResult.Success(new Response(user.Id)));
        }
    }
}