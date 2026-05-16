using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Referrals;

// Read-only validation of a referral code at sign-up time. Does not mutate
// state — the lazy-create commit for the *referrer's* own code happens
// separately in `EnsureCodeForUserAsync`. Modelled as IQuery so the CQRS
// surface accurately reflects "no writes".
public class ValidateReferral
{
    public class Validator : AbstractValidator<Query>
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

    public record Query(string Code) : IQuery<Response>;

    public record Response(
        bool IsValid,
        string? ReferrerFirstName,
        string? ErrorCode);

    public class Handler(
        IReferralService referralService,
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider) : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query command, CancellationToken cancellationToken)
        {
            var acceptingUserId = userSessionProvider.GetUserId() ?? string.Empty;
            var validation = await referralService.ValidateAsync(
                command.Code, acceptingUserId, cancellationToken);

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
