using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Referrals.Admin;

/// <summary>
/// Admin reversal of a fraudulent / refunded Qualified referral, and the rejection of one held for review.
/// Takes back, per side and in the currency that side was paid in, the referral credit the ledger shows was
/// granted under that side's key, and flips the referral to the terminal Reversed status. A held referral
/// paid nothing, so nothing is taken. A balance that no longer holds the whole grant gives up only what is
/// left (owner default 2026-10-04): credit never goes negative, and the debit row records what was taken.
/// <para>
/// Idempotency (ADR-0002, S7a): each side's debit uses a DETERMINISTIC key derived from the referral id,
/// so a retry of the SAME logical reversal collapses onto exactly one debit row per side. The status
/// guard (must be Qualified or held) makes a second invocation on an already-reversed row a guarded no-op
/// business error — never a double clawback.
/// </para>
/// </summary>
public class ReverseReferral
{
    public record Command(string ReferralId, string Reason) : ICommand<Response>;

    public record Response(
        string ReferralId,
        decimal CreditTakenFromReferrer,
        string? ReferrerCurrencyCode,
        decimal CreditTakenFromReferred,
        string? ReferredCurrencyCode);

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
        ICreditAccountRepository creditAccountRepository,
        ICurrencyRepository currencyRepository,
        IUserSessionProvider userSessionProvider,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var referral = await referralRepository.GetByIdAsync(command.ReferralId, cancellationToken);
            if (referral is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.ReferralId), BusinessErrorMessage.ReferralNotFound));
            }

            // Idempotency guard (ADR-0002): only a Qualified or a held referral can be reversed. A retry on an
            // already-Reversed row lands here and returns a guarded error — no second clawback.
            var held = referral.Status == ReferralStatus.Accepted && referral.HoldReasons is not null;
            if (referral.Status != ReferralStatus.Qualified && !held)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.ReferralId), BusinessErrorMessage.ReferralNotQualified));
            }

            var actorId = userSessionProvider.GetUserId() ?? string.Empty;
            decimal fromReferrer = 0m;
            decimal fromReferred = 0m;
            string? referrerCurrencyCode = null;
            string? referredCurrencyCode = null;

            // Same owner-lock order as the grant, so a reversal and a grant to the same pair queue.
            var sides = new[]
                {
                    (Side: ReferralService.ReferrerSide, UserId: referral.ReferrerUserId, CurrencyId: referral.ReferrerCreditCurrencyId),
                    (Side: ReferralService.ReferredSide, UserId: referral.ReferredUserId, CurrencyId: referral.ReferredCreditCurrencyId),
                }
                .OrderBy(s => s.UserId, StringComparer.Ordinal);
            foreach (var (side, userId, currencyId) in sides)
            {
                if (currencyId is null)
                {
                    continue;
                }

                var taken = await TakeBackAsync(
                    referral, currencyId, side, userId, actorId, command.Reason, cancellationToken);
                var currencyCode = (await currencyRepository.GetByIdAsync(currencyId, cancellationToken))?.Code;
                if (side == ReferralService.ReferrerSide)
                {
                    fromReferrer = taken;
                    referrerCurrencyCode = currencyCode;
                }
                else
                {
                    fromReferred = taken;
                    referredCurrencyCode = currencyCode;
                }
            }

            referral.Reverse(actorId);

            return BusinessResult.Success(new Response(
                referral.Id, fromReferrer, referrerCurrencyCode, fromReferred, referredCurrencyCode));
        }

        private async Task<decimal> TakeBackAsync(
            Referral referral,
            string currencyId,
            string side,
            string userId,
            string actorId,
            string reason,
            CancellationToken cancellationToken)
        {
            var granted = await creditAccountRepository.GetAmountAsync(
                ReferralService.CreditKey(referral.Id, side), CreditTransactionReason.Referral, cancellationToken);
            if (granted <= 0m)
            {
                return 0m;
            }

            // The balance is read under the owner lock, so the debit below cannot find less than it saw.
            await creditAccountRepository.LockForUserAsync(userId, cancellationToken);
            var account = await creditAccountRepository.GetSpendableAsync(userId, currencyId, cancellationToken);
            var taken = Math.Min(granted, account?.Balance ?? 0m);
            if (taken <= 0m
                || !await creditAccountRepository.TryDebitAsync(
                    account!.AccountId,
                    taken,
                    CreditTransactionReason.ReferralReversed,
                    $"referral-reverse:{referral.Id}:{side}",
                    actorId,
                    cancellationToken,
                    note: reason))
            {
                taken = 0m;
            }

            if (taken < granted)
            {
                logger.LogWarning(
                    "Referral {ReferralId} reversed: took {Taken} of the {Granted} credit granted to the {Side}; the balance held no more.",
                    referral.Id, taken, granted, side);
            }

            return taken;
        }
    }
}
