using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Memberships.Admin.DTOs;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Memberships.Admin;

/// <summary>
/// Admin creation of a new membership plan with a price per currency. The Stripe Product/Prices are
/// registered out of band — each <see cref="MembershipPlanPriceInput.StripePriceId"/> is the
/// admin-entered Price id (we never call Stripe to create products/prices). Code is normalised to
/// uppercase by <see cref="MembershipPlan.Create"/>; uniqueness is enforced in the handler via
/// <see cref="IMembershipPlanRepository.GetByCodeAsync"/>.
/// </summary>
public class CreateMembershipPlan
{
    public record Command(
        string Code,
        string Name,
        BillingInterval BillingInterval,
        /// <summary>
        /// One entry per currency the plan is sold in, keyed by currency code. Empty or partial is
        /// legal — a plan unpriced in a market is "Plus is not on sale there" (ADR-0059 D4), which is
        /// why this is NOT MustCoverAllActiveCurrencies like the catalogue.
        /// </summary>
        Dictionary<string, MembershipPlanPriceInput>? Prices,
        decimal DiscountPercentage,
        int FreeCancellationWindowHours,
        int TrialPeriodDays,
        bool AllowsExpressUpgrade,
        /// <summary>
        /// Free express upgrades granted per calendar month. Ignored when
        /// <see cref="AllowsExpressUpgrade"/> is false; 0 means no waiver (fail-closed), never
        /// "unlimited" — a sentinel there is how a seeding mistake becomes an unbounded discount.
        /// </summary>
        int ExpressUpgradesPerMonth = 0) : ICommand<Response>;

    public record Response(string MembershipPlanId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(ICurrencyRepository currencyRepository, IMembershipPlanPriceRepository membershipPlanPriceRepository)
        {
            RuleFor(x => x.Code)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(50)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.Name)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(100)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.BillingInterval)
                .IsInEnum()
                .WithMessage(BusinessErrorMessage.InvalidEnumValue);

            RuleFor(x => x.Prices)
                .Cascade(CascadeMode.Stop)
                .MustBeKeyedByKnownCurrencyCodes(currencyRepository)
                .MustNotRepeatAStripePriceId();

            RuleForEach(x => x.Prices)
                .SetValidator(new MembershipPlanPriceEntryValidator(membershipPlanPriceRepository));

            RuleFor(x => x.DiscountPercentage)
                .InclusiveBetween(0m, 100m)
                .WithMessage(BusinessErrorMessage.MembershipPlanDiscountOutOfRange);

            RuleFor(x => x.FreeCancellationWindowHours)
                .GreaterThanOrEqualTo(0)
                .WithMessage(BusinessErrorMessage.MustBePositive);

            // A trial grants Cleansia Plus benefits to somebody who has not paid, which the owner ruling
            // of 2026-09-08 (T-0690) forbids. Refused here rather than merely defaulted to 0, because a
            // deployed database gets its plans from this admin surface and not from the dev seed — so the
            // seed value alone would enforce nothing where it matters.
            RuleFor(x => x.TrialPeriodDays)
                .Equal(0)
                .WithMessage(BusinessErrorMessage.MembershipPlanTrialNotPermitted);
        }
    }

    public class Handler(
        IMembershipPlanRepository membershipPlanRepository,
        IMembershipPlanPriceRepository membershipPlanPriceRepository,
        ICurrencyRepository currencyRepository) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var existing = await membershipPlanRepository.GetByCodeAsync(command.Code, cancellationToken);
            if (existing != null)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.Code), BusinessErrorMessage.MembershipPlanCodeAlreadyExists));
            }

            var plan = MembershipPlan.Create(
                code: command.Code,
                name: command.Name,
                discountPercentage: command.DiscountPercentage,
                freeCancellationWindowHours: command.FreeCancellationWindowHours,
                allowsExpressUpgrade: command.AllowsExpressUpgrade,
                billingInterval: command.BillingInterval,
                trialPeriodDays: command.TrialPeriodDays,
                expressUpgradesPerMonth: command.ExpressUpgradesPerMonth);

            membershipPlanRepository.Add(plan);

            await MembershipPlanPricing.UpsertAsync(
                plan.Id, command.Prices, membershipPlanPriceRepository, currencyRepository, cancellationToken);

            return BusinessResult.Success(new Response(plan.Id));
        }
    }
}
