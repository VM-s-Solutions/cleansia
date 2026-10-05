using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Referrals.Admin;

/// <summary>
/// Admin force-qualify of a legitimate referral stuck in Accepted (e.g. the
/// qualifying order completed but the automatic path missed it), and the release
/// of one held for review. Credits each side in the currency it books in, the way
/// the automatic path does — a held referral's friend in the currency of the held
/// order, against that order — and marks the referral Qualified with the admin as actor.
/// <para>
/// Idempotency (ADR-0002, S7a): each side's grant carries the same per-(referral, side)
/// ledger key the automatic path uses, so the two paths can never both pay a side. The
/// status guard (must be Accepted) makes a second invocation on an already-Qualified row
/// a guarded no-op business error — never a double grant.
/// </para>
/// <para>
/// The command carries the hold state the administrator saw. A force-qualify sent from a row that was not
/// held, reaching a referral held since, is refused rather than paying past a hold nobody reviewed.
/// </para>
/// </summary>
public class ForceQualifyReferral
{
    public record Command(string ReferralId, string Reason, bool ExpectHeld) : ICommand<Response>;

    public record Response(
        string ReferralId,
        decimal CreditGrantedToReferrer,
        string ReferrerCurrencyCode,
        decimal CreditGrantedToReferred,
        string ReferredCurrencyCode);

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
        IReferralService referralService,
        IOrderRepository orderRepository,
        ICurrencyRepository currencyRepository,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var referral = await referralRepository.GetByIdAsync(command.ReferralId, cancellationToken);
            if (referral is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.ReferralId), BusinessErrorMessage.ReferralNotFound));
            }

            // Idempotency guard (ADR-0002): only an Accepted referral can be force-qualified. A retry on
            // an already-Qualified row lands here and returns a guarded error — no second grant.
            if (referral.Status != ReferralStatus.Accepted)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.ReferralId), BusinessErrorMessage.ReferralNotAccepted));
            }

            var held = referral.HoldReasons is not null;
            if (held != command.ExpectHeld)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.ExpectHeld), BusinessErrorMessage.ReferralHoldChanged));
            }

            var actorId = userSessionProvider.GetUserId() ?? string.Empty;
            var referredCurrency = await HeldOrderCurrencyAsync(referral, cancellationToken)
                ?? await referralService.GetBookingCurrencyAsync(referral.ReferredUserId, cancellationToken)
                ?? await currencyRepository.GetDefaultAsync(cancellationToken);
            var referrerCurrency = await referralService.GetBookingCurrencyAsync(referral.ReferrerUserId, cancellationToken)
                ?? referredCurrency;

            var (toReferrer, toReferred) = await referralService.AwardCreditAsync(
                referral, referrerCurrency, referredCurrency, referral.FirstQualifyingOrderId, actorId, command.Reason,
                cancellationToken);

            referral.ForceQualify(
                referrerCurrencyId: referrerCurrency.Id,
                creditToReferrer: toReferrer,
                referredCurrencyId: referredCurrency.Id,
                creditToReferred: toReferred,
                actorId: actorId);

            return BusinessResult.Success(new Response(
                referral.Id, toReferrer ?? 0m, referrerCurrency.Code, toReferred ?? 0m, referredCurrency.Code));
        }

        /// <summary>
        /// The currency of the order a held referral was held on, while that order still names the friend —
        /// erasure takes their name off it. Null for a referral that was never held.
        /// </summary>
        private async Task<Currency?> HeldOrderCurrencyAsync(Referral referral, CancellationToken cancellationToken)
        {
            if (referral.FirstQualifyingOrderId is null)
            {
                return null;
            }

            var held = await orderRepository.GetOwnerAndCurrencyAsync(
                referral.FirstQualifyingOrderId, referral.ReferredUserId, cancellationToken);
            return held is null ? null : await currencyRepository.GetByIdAsync(held.CurrencyId, cancellationToken);
        }
    }
}
