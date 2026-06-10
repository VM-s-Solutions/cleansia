using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Memberships.Admin;

/// <summary>
/// Admin edit of a membership plan's pricing + benefits. Code and
/// BillingInterval are create-only (immutable on edit). Mutations land via the
/// entity's UpdatePricing / UpdateBenefits methods; UpdatedBy/UpdatedOn are
/// stamped by the SaveChanges interceptor from the user session at commit.
/// </summary>
public class UpdateMembershipPlan
{
    public record Command(
        string MembershipPlanId,
        string Name,
        decimal MonthlyPriceCzk,
        string StripePriceId,
        decimal DiscountPercentage,
        int FreeCancellationWindowHours,
        int TrialPeriodDays,
        bool AllowsExpressUpgrade) : ICommand<Response>;

    public record Response(string MembershipPlanId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IMembershipPlanRepository membershipPlanRepository)
        {
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

            RuleFor(x => x.MonthlyPriceCzk)
                .GreaterThanOrEqualTo(0m)
                .WithMessage(BusinessErrorMessage.MustBePositive);

            RuleFor(x => x.StripePriceId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(64)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.DiscountPercentage)
                .InclusiveBetween(0m, 100m)
                .WithMessage(BusinessErrorMessage.MembershipPlanDiscountOutOfRange);

            RuleFor(x => x.FreeCancellationWindowHours)
                .GreaterThanOrEqualTo(0)
                .WithMessage(BusinessErrorMessage.MustBePositive);

            RuleFor(x => x.TrialPeriodDays)
                .GreaterThanOrEqualTo(0)
                .WithMessage(BusinessErrorMessage.MustBePositive);
        }
    }

    public class Handler(IMembershipPlanRepository membershipPlanRepository) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var plan = await membershipPlanRepository.GetByIdAsync(command.MembershipPlanId, cancellationToken);

            plan!
                .UpdateName(command.Name)
                .UpdatePricing(command.MonthlyPriceCzk, command.StripePriceId)
                .UpdateBenefits(
                    discountPercentage: command.DiscountPercentage,
                    freeCancellationWindowHours: command.FreeCancellationWindowHours,
                    allowsExpressUpgrade: command.AllowsExpressUpgrade)
                .UpdateTrial(command.TrialPeriodDays);

            return BusinessResult.Success(new Response(plan.Id));
        }
    }
}
