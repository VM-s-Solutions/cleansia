using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Referrals;

/// <summary>
/// Pre-submit validation called from the signup form (anonymous, with empty
/// AcceptingUserId) and from the booking-time late-acceptance link (with
/// the JWT-derived AcceptingUserId). Returns the inviter's first name so
/// the UI can render "Code from Jana — you'll get 150 bonus points".
/// </summary>
public class ValidateReferral
{
    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Code)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(10)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }
    }

    public record Command(
        string Code,
        // Enriched server-side from the JWT in the controller — empty when
        // called anonymously from the signup form.
        string AcceptingUserId = "") : ICommand<Response>;

    public record Response(
        bool IsValid,
        string? ReferrerFirstName,
        string? ErrorCode);

    public class Handler(
        IReferralService referralService,
        IUserRepository userRepository) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var validation = await referralService.ValidateAsync(
                command.Code, command.AcceptingUserId, cancellationToken);

            string? referrerFirstName = null;
            if (validation.IsValid && !string.IsNullOrEmpty(validation.ReferrerUserId))
            {
                var referrer = await userRepository.GetByIdAsync(
                    validation.ReferrerUserId, cancellationToken);
                referrerFirstName = referrer?.FirstName;
            }

            return BusinessResult.Success(new Response(
                IsValid: validation.IsValid,
                ReferrerFirstName: referrerFirstName,
                ErrorCode: validation.Error?.ToString()));
        }
    }
}
