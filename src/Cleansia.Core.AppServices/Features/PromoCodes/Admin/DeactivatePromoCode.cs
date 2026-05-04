using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.PromoCodes.Admin;

/// <summary>
/// Soft-disable a promo code (preserves audit history). Idempotent — calling
/// on an already-inactive code returns success without raising an error.
/// </summary>
public class DeactivatePromoCode
{
    public record Command(string PromoCodeId) : ICommand<Response>;

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
        }
    }

    public class Handler(IPromoCodeRepository promoCodeRepository) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var entity = await promoCodeRepository.GetByIdAsync(command.PromoCodeId, cancellationToken);

            if (!entity!.IsActive)
            {
                // Idempotent — nothing to do.
                return BusinessResult.Success(new Response(entity.Id));
            }

            entity.Deactivate(string.Empty);

            return BusinessResult.Success(new Response(entity.Id));
        }
    }
}
