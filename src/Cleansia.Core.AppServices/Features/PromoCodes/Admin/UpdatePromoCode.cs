using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.PromoCodes.Admin;

/// <summary>
/// Admin edit of the mutable subset of a promo code. Code/Type/Discount
/// fields are immutable on the entity — admins must deactivate and re-issue
/// to change those (preserves audit history on already-redeemed codes).
/// </summary>
public class UpdatePromoCode
{
    public record Command(
        string PromoCodeId,
        bool IsActive,
        DateTimeOffset? ValidFrom,
        DateTimeOffset? ValidUntil,
        decimal? MinimumOrderAmount,
        int MaxRedemptionsPerUser,
        int? GlobalMaxRedemptions,
        string? Description) : ICommand<Response>;

    public record Response(string PromoCodeId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IPromoCodeRepository promoCodeRepository)
        {
            RuleFor(x => x.PromoCodeId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(promoCodeRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.PromoNotFound);

            RuleFor(x => x.MaxRedemptionsPerUser)
                .GreaterThanOrEqualTo(1)
                .WithMessage(BusinessErrorMessage.MustBePositive);

            RuleFor(x => x.GlobalMaxRedemptions)
                .GreaterThanOrEqualTo(1)
                .When(x => x.GlobalMaxRedemptions.HasValue)
                .WithMessage(BusinessErrorMessage.MustBePositive);

            RuleFor(x => x.MinimumOrderAmount)
                .GreaterThanOrEqualTo(0m)
                .When(x => x.MinimumOrderAmount.HasValue)
                .WithMessage(BusinessErrorMessage.MustBePositive);

            RuleFor(x => x.Description)
                .MaximumLength(500)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x)
                .Must(x => !x.ValidFrom.HasValue || !x.ValidUntil.HasValue || x.ValidFrom.Value <= x.ValidUntil.Value)
                .WithMessage(BusinessErrorMessage.PromoCodeValidityRangeInvalid);
        }
    }

    public class Handler(IPromoCodeRepository promoCodeRepository) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var entity = await promoCodeRepository.GetByIdAsync(command.PromoCodeId, cancellationToken);

            // ActorId is overridden in CommitAsync from the JWT — we pass an
            // empty string here because the SaveChanges interceptor stamps
            // UpdatedBy/UpdatedOn from the user session anyway.
            entity!.Update(
                isActive: command.IsActive,
                validFrom: command.ValidFrom,
                validUntil: command.ValidUntil,
                minimumOrderAmount: command.MinimumOrderAmount,
                maxRedemptionsPerUser: command.MaxRedemptionsPerUser,
                globalMaxRedemptions: command.GlobalMaxRedemptions,
                description: command.Description,
                actorId: string.Empty);

            return BusinessResult.Success(new Response(entity.Id));
        }
    }
}
