using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Referrals.Admin;

/// <summary>
/// Admin force-qualify of a legitimate referral stuck in Accepted (e.g. the
/// qualifying order completed but the automatic path missed it). Applies the
/// symmetric grants through the loyalty manual-grant path and marks the referral
/// Qualified with the admin as actor.
/// <para>
/// Idempotency (ADR-0002, S7a): each side's grant uses a DETERMINISTIC requestId
/// derived from the referral id, so a retry collapses onto one grant ledger row
/// per side. The status guard (must be Accepted) makes a second invocation on an
/// already-Qualified row a guarded no-op business error — never a double grant.
/// </para>
/// </summary>
public class ForceQualifyReferral
{
    public record Command(string ReferralId, string Reason) : ICommand<Response>;

    public record Response(string ReferralId, int PointsGrantedToReferrer, int PointsGrantedToReferred);

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

            // Idempotency guard (ADR-0002): only an Accepted referral can be force-qualified. A retry on
            // an already-Qualified row lands here and returns a guarded error — no second grant.
            if (referral!.Status != ReferralStatus.Accepted)
            {
                return BusinessResult.Failure<Response>(
                    new Error(BusinessErrorMessage.ReferralNotAccepted, BusinessErrorMessage.ReferralNotAccepted));
            }

            var actorId = userSessionProvider.GetUserId() ?? string.Empty;
            var points = ReferralPolicy.PointsPerSide;

            await loyaltyService.GrantPointsManuallyAsync(
                userId: referral.ReferrerUserId,
                points: points,
                source: LoyaltyEarnSource.Referral,
                orderId: null,
                actorId: actorId,
                reason: command.Reason,
                requestId: QualifyRequestId(referral.Id, "referrer"),
                cancellationToken: cancellationToken);

            await loyaltyService.GrantPointsManuallyAsync(
                userId: referral.ReferredUserId,
                points: points,
                source: LoyaltyEarnSource.Referral,
                orderId: null,
                actorId: actorId,
                reason: command.Reason,
                requestId: QualifyRequestId(referral.Id, "referred"),
                cancellationToken: cancellationToken);

            referral.ForceQualify(
                pointsToReferrer: points,
                pointsToReferred: points,
                actorId: actorId);

            return BusinessResult.Success(new Response(referral.Id, points, points));
        }

        // Deterministic per-(referral, side) idempotency key so a retry collapses onto one grant.
        private static string QualifyRequestId(string referralId, string side) =>
            $"referral-qualify:{referralId}:{side}";
    }
}
