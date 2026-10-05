using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Referrals.Admin;

/// <summary>
/// Admin force-qualify of a legitimate referral stuck in Accepted (e.g. the
/// qualifying order completed but the automatic path missed it). Credits both
/// sides the way the automatic path does and marks the referral Qualified with
/// the admin as actor.
/// <para>
/// Idempotency (ADR-0002, S7a): each side's grant carries the same per-(referral, side)
/// ledger key the automatic path uses, so the two paths can never both pay a side. The
/// status guard (must be Accepted) makes a second invocation on an already-Qualified row
/// a guarded no-op business error — never a double grant.
/// </para>
/// </summary>
public class ForceQualifyReferral
{
    public record Command(string ReferralId, string Reason) : ICommand<Response>;

    public record Response(
        string ReferralId,
        decimal CreditGrantedToReferrer,
        decimal CreditGrantedToReferred,
        string CurrencyCode);

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

            var actorId = userSessionProvider.GetUserId() ?? string.Empty;
            var currency = await ResolveMarketCurrencyAsync(referral.ReferredUserId, cancellationToken);

            var (toReferrer, toReferred) = await referralService.AwardCreditAsync(
                referral, currency.Id, currency.ReferralCredit, orderId: null, actorId, command.Reason, cancellationToken);

            referral.ForceQualify(
                creditCurrencyId: currency.Id,
                creditToReferrer: toReferrer,
                creditToReferred: toReferred,
                actorId: actorId);

            return BusinessResult.Success(new Response(
                referral.Id, toReferrer ?? 0m, toReferred ?? 0m, currency.Code));
        }

        /// <summary>
        /// The market the referred customer books in — their latest order's currency — or the platform
        /// default for one who has never booked.
        /// </summary>
        private async Task<Currency> ResolveMarketCurrencyAsync(string referredUserId, CancellationToken cancellationToken)
        {
            var latestCurrencyId = await orderRepository.GetQueryableForOwner(referredUserId)
                .OrderByDescending(o => o.CreatedOn)
                .Select(o => o.CurrencyId)
                .FirstOrDefaultAsync(cancellationToken);

            var latest = latestCurrencyId is null
                ? null
                : await currencyRepository.GetByIdAsync(latestCurrencyId, cancellationToken);
            return latest ?? await currencyRepository.GetDefaultAsync(cancellationToken);
        }
    }
}
