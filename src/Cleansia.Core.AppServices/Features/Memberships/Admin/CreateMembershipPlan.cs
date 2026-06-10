using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Memberships.Admin;

/// <summary>
/// Admin creation of a new membership plan. The Stripe Product/Price are
/// registered out of band — <see cref="Command.StripePriceId"/> is the
/// admin-entered Price id (we never call Stripe to create products/prices).
/// Code is normalised to uppercase by <see cref="MembershipPlan.Create"/>;
/// uniqueness within the tenant scope is enforced in the handler via
/// <see cref="IMembershipPlanRepository.GetByCodeAsync"/>.
/// </summary>
public class CreateMembershipPlan
{
    public record Command(
        string Code,
        string Name,
        BillingInterval BillingInterval,
        decimal MonthlyPriceCzk,
        string StripePriceId,
        decimal DiscountPercentage,
        int FreeCancellationWindowHours,
        int TrialPeriodDays,
        bool AllowsExpressUpgrade) : ICommand<Response>;

    public record Response(string MembershipPlanId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
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
            var existing = await membershipPlanRepository.GetByCodeAsync(command.Code, cancellationToken);
            if (existing != null)
            {
                return BusinessResult.Failure<Response>(
                    new Error(BusinessErrorMessage.MembershipPlanCodeAlreadyExists, BusinessErrorMessage.MembershipPlanCodeAlreadyExists));
            }

            var plan = MembershipPlan.Create(
                code: command.Code,
                name: command.Name,
                monthlyPriceCzk: command.MonthlyPriceCzk,
                stripePriceId: command.StripePriceId,
                discountPercentage: command.DiscountPercentage,
                freeCancellationWindowHours: command.FreeCancellationWindowHours,
                allowsExpressUpgrade: command.AllowsExpressUpgrade,
                billingInterval: command.BillingInterval,
                trialPeriodDays: command.TrialPeriodDays);

            membershipPlanRepository.Add(plan);

            return BusinessResult.Success(new Response(plan.Id));
        }
    }
}
