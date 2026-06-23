using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using static Cleansia.Core.AppServices.Common.Validators.ValidationExtensions;

namespace Cleansia.Core.AppServices.Features.AdminUsers;

public class UpdateAdminUser
{
    [AuditAction("admin.user.update", ResourceType = "AdminUser")]
    public record Command(
        string UserId,
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
            RuleFor(x => x.UserId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (userId, ct) =>
                    await userRepository.GetAll()
                        .AnyAsync(u => u.Id == userId && u.Profile == UserProfile.Administrator, ct))
                .WithMessage(BusinessErrorMessage.AdminUserNotFound);

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
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var user = await userRepository
                .GetAll()
                .FirstOrDefaultAsync(
                    u => u.Id == command.UserId && u.Profile == UserProfile.Administrator,
                    cancellationToken);

            // Omitted optional fields preserve the stored values — User.Update defaults
            // birthDate to null, which silently wiped it on every name-only edit.
            user!.Update(
                firstName: command.FirstName,
                lastName: command.LastName,
                phoneNumber: command.PhoneNumber ?? string.Empty,
                birthDate: command.BirthDate ?? user.BirthDate);

            if (!string.IsNullOrWhiteSpace(command.PreferredLanguageCode))
            {
                user.UpdateLanguagePreference(command.PreferredLanguageCode);
            }

            return BusinessResult.Success(new Response(user.Id));
        }
    }
}