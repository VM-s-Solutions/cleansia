using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Referrals.Admin;

/// <summary>
/// Admin reversal of a fraudulent / refunded Qualified referral. Claws back the
/// symmetric point grants recorded on the row through the loyalty manual-revoke
/// path and flips the referral to the terminal Reversed status.
/// <para>
/// Idempotency (ADR-0002, S7a): each side's clawback uses a DETERMINISTIC
/// requestId derived from the referral id, so a retry of the SAME logical
/// reversal collapses onto exactly one revoke ledger row per side. The status
/// guard (must be Qualified) makes a second invocation on an already-reversed
/// row a guarded no-op business error — never a double clawback.
/// </para>
/// </summary>
public class ReverseReferral
{
    public record Command(string ReferralId, string Reason) : ICommand<Response>;

    public record Response(string ReferralId, int PointsRevokedFromReferrer, int PointsRevokedFromReferred);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IReferralRepository referralRepository)
        {
            RuleFor(x => x.ReferralId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(referralRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.ReferralNotFound);

            RuleFor(x => x.Reason)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.ReferralReasonRequired)
                .MaximumLength(500)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }
    }

    public class Handler(
        IReferralRepository referralRepository,
        ILoyaltyService loyaltyService,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var referral = await referralRepository.GetByIdAsync(command.ReferralId, cancellationToken);

            // Idempotency guard (ADR-0002): only a Qualified referral can be reversed. A retry on an
            // already-Reversed row lands here and returns a guarded error — no second clawback.
            if (referral!.Status != ReferralStatus.Qualified)
            {
                return BusinessResult.Failure<Response>(
                    new Error(BusinessErrorMessage.ReferralNotQualified, BusinessErrorMessage.ReferralNotQualified));
            }

            var actorId = userSessionProvider.GetUserId() ?? string.Empty;
            var referrerPoints = referral.PointsAwardedToReferrer ?? 0;
            var referredPoints = referral.PointsAwardedToReferred ?? 0;

            if (referrerPoints > 0)
            {
                await loyaltyService.RevokePointsManuallyAsync(
                    userId: referral.ReferrerUserId,
                    points: referrerPoints,
                    source: LoyaltyEarnSource.Referral,
                    orderId: null,
                    actorId: actorId,
                    reason: command.Reason,
                    requestId: ReverseRequestId(referral.Id, "referrer"),
                    cancellationToken: cancellationToken);
            }

            if (referredPoints > 0)
            {
                await loyaltyService.RevokePointsManuallyAsync(
                    userId: referral.ReferredUserId,
                    points: referredPoints,
                    source: LoyaltyEarnSource.Referral,
                    orderId: null,
                    actorId: actorId,
                    reason: command.Reason,
                    requestId: ReverseRequestId(referral.Id, "referred"),
                    cancellationToken: cancellationToken);
            }

            referral.Reverse(actorId);

            return BusinessResult.Success(new Response(referral.Id, referrerPoints, referredPoints));
        }

        // Deterministic per-(referral, side) idempotency key so a retry collapses onto one revoke.
        private static string ReverseRequestId(string referralId, string side) =>
            $"referral-reverse:{referralId}:{side}";
    }
}
