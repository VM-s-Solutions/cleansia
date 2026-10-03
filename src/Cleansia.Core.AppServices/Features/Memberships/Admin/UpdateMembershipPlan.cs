using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Memberships.Admin.DTOs;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Memberships.Admin;

/// <summary>
/// Admin edit of a membership plan's benefits and per-currency prices. Code and BillingInterval are
/// create-only (immutable on edit). A currency the payload does not mention keeps the row it has;
/// removing a row is not offered — deactivate the plan instead. Once anyone has subscribed, the discount
/// and the express quota are the terms that subscriber was shown and stay as they are; a different offer
/// is a new plan.
/// </summary>
public class UpdateMembershipPlan
{
    public record Command(
        string MembershipPlanId,
        string Name,
        /// <summary>One entry per currency to write, keyed by currency code; keys not sent are left as they are.</summary>
        Dictionary<string, MembershipPlanPriceInput>? Prices,
        decimal DiscountPercentage,
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
        private readonly IMembershipPlanRepository _membershipPlanRepository;
        private readonly IUserMembershipRepository _userMembershipRepository;

        public Validator(
            IMembershipPlanRepository membershipPlanRepository,
            ICurrencyRepository currencyRepository,
            IMembershipPlanPriceRepository membershipPlanPriceRepository,
            IUserMembershipRepository userMembershipRepository)
        {
            _membershipPlanRepository = membershipPlanRepository;
            _userMembershipRepository = userMembershipRepository;

            RuleFor(x => x.MembershipPlanId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(membershipPlanRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.MembershipPlanNotFound);

            RuleFor(x => x.Name)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(100)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.Prices)
                .Cascade(CascadeMode.Stop)
                .MustBeKeyedByKnownCurrencyCodes(currencyRepository)
                .MustNotRepeatAStripePriceId();

            RuleForEach(x => x.Prices)
                .SetValidator(command => new MembershipPlanPriceEntryValidator(membershipPlanPriceRepository)
                {
                    ExceptPlanId = command.MembershipPlanId,
                });

            RuleFor(x => x.DiscountPercentage)
                .Cascade(CascadeMode.Stop)
                .InclusiveBetween(0m, 100m)
                .WithMessage(BusinessErrorMessage.MembershipPlanDiscountOutOfRange)
                .MustAsync((command, _, ct) => KeepsSubscribedTermsAsync(
                    command, plan => plan.DiscountPercentage == command.DiscountPercentage, ct))
                .WithMessage(BusinessErrorMessage.MembershipPlanBenefitsLocked);

            // A subscriber was shown a quota of zero while the waiver is off, so the toggle moves the
            // quota as surely as the number does.
            RuleFor(x => x.ExpressUpgradesPerMonth)
                .MustAsync((command, _, ct) => KeepsSubscribedTermsAsync(
                    command,
                    plan => plan.ExpressUpgradesPerMonth == command.ExpressUpgradesPerMonth
                        && StatedExpressQuota(plan.AllowsExpressUpgrade, plan.ExpressUpgradesPerMonth)
                            == StatedExpressQuota(command.AllowsExpressUpgrade, command.ExpressUpgradesPerMonth),
                    ct))
                .WithMessage(BusinessErrorMessage.MembershipPlanBenefitsLocked);

            RuleFor(x => x.TrialPeriodDays)
                .GreaterThanOrEqualTo(0)
                .WithMessage(BusinessErrorMessage.MustBePositive);
        }

        private async Task<bool> KeepsSubscribedTermsAsync(
            Command command,
            Func<MembershipPlan, bool> isUnchanged,
            CancellationToken cancellationToken)
        {
            var plan = await _membershipPlanRepository.GetByIdAsync(command.MembershipPlanId, cancellationToken);

            return plan is null
                || isUnchanged(plan)
                || !await _userMembershipRepository.HasAnyForPlanAsync(plan.Id, cancellationToken);
        }

        private static int StatedExpressQuota(bool allowsExpressUpgrade, int expressUpgradesPerMonth) =>
            allowsExpressUpgrade ? expressUpgradesPerMonth : 0;
    }

    public class Handler(
        IMembershipPlanRepository membershipPlanRepository,
        IMembershipPlanPriceRepository membershipPlanPriceRepository,
        ICurrencyRepository currencyRepository) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var plan = await membershipPlanRepository.GetByIdAsync(command.MembershipPlanId, cancellationToken);
            if (plan is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.MembershipPlanId), BusinessErrorMessage.MembershipPlanNotFound));
            }

            plan.UpdateName(command.Name)
                .UpdateBenefits(
                    discountPercentage: command.DiscountPercentage,
                    allowsExpressUpgrade: command.AllowsExpressUpgrade,
                    expressUpgradesPerMonth: command.ExpressUpgradesPerMonth)
                .UpdateTrial(command.TrialPeriodDays);

            await MembershipPlanPricing.UpsertAsync(
                plan.Id, command.Prices, membershipPlanPriceRepository, currencyRepository, cancellationToken);

            return BusinessResult.Success(new Response(plan.Id));
        }
    }
}
