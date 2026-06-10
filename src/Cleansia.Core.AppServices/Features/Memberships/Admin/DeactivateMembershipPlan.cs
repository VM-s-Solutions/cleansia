using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Memberships.Admin;

/// <summary>
/// Soft-disable a membership plan (sets IsActive=false via Deactivate()). The
/// plan disappears from the customer switcher (GetActivePlansAsync) but
/// existing UserMembership rows referencing it keep working. Idempotent —
/// calling on an already-inactive plan returns success without an error.
/// </summary>
public class DeactivateMembershipPlan
{
    public record Command(string MembershipPlanId) : ICommand<Response>;

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
        }
    }

    public class Handler(IMembershipPlanRepository membershipPlanRepository) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var plan = await membershipPlanRepository.GetByIdAsync(command.MembershipPlanId, cancellationToken);

            if (!plan!.IsActive)
            {
                return BusinessResult.Success(new Response(plan.Id));
            }

            plan.Deactivate();

            return BusinessResult.Success(new Response(plan.Id));
        }
    }
}
