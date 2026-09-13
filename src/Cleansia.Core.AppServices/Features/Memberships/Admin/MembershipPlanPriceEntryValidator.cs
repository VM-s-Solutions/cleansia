using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Memberships.Admin.DTOs;
using Cleansia.Core.Domain.Repositories;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Memberships.Admin;

/// <summary>
/// One dictionary entry of the admin form: a non-negative price and a Stripe Price id that is
/// present, fits the column, and is not already charging another row — another plan's, or this
/// plan's row in another currency, since a Stripe Price is single-currency. The unique index on
/// <c>StripePriceId</c> is the backstop behind that last rule.
/// </summary>
public class MembershipPlanPriceEntryValidator : AbstractValidator<KeyValuePair<string, MembershipPlanPriceInput>>
{
    public MembershipPlanPriceEntryValidator(IMembershipPlanPriceRepository membershipPlanPriceRepository, string? exceptPlanId)
    {
        RuleFor(x => x.Value.Price)
            .GreaterThanOrEqualTo(0m)
            .WithMessage(BusinessErrorMessage.MustBePositive);

        RuleFor(x => x.Value.StripePriceId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(BusinessErrorMessage.Required)
            .MaximumLength(64)
            .WithMessage(BusinessErrorMessage.MaxLength)
            .MustAsync(async (entry, stripePriceId, cancellationToken) =>
                !await membershipPlanPriceRepository.IsStripePriceIdUsedAsync(stripePriceId, exceptPlanId, entry.Key, cancellationToken))
            .WithMessage(BusinessErrorMessage.MembershipPlanStripePriceAlreadyUsed);
    }
}
