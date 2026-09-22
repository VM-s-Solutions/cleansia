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
    /// <summary>
    /// The plan being updated, whose own row in the entry's currency may keep its Stripe id; null on
    /// create. A property rather than a constructor argument because the assembly scan registers every
    /// validator with the container, and a <c>string</c> parameter cannot be resolved there.
    /// </summary>
    public string? ExceptPlanId { get; init; }

    public MembershipPlanPriceEntryValidator(IMembershipPlanPriceRepository membershipPlanPriceRepository)
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
                !await membershipPlanPriceRepository.IsStripePriceIdUsedAsync(stripePriceId, ExceptPlanId, entry.Key, cancellationToken))
            .WithMessage(BusinessErrorMessage.MembershipPlanStripePriceAlreadyUsed);
    }
}
