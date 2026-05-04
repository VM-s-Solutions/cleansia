using System.Text.Json;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Loyalty.Admin;

/// <summary>
/// Edit a single tier config. The Tier discriminator is immutable; admin
/// updates the threshold / discount / perks for the row identified by
/// <see cref="Command.TierConfigId"/>. Threshold edits retroactively
/// reclassify users — the UI should call PreviewTierThresholdImpact first.
/// </summary>
public class UpdateTierConfig
{
    public record Command(
        string TierConfigId,
        int LifetimePointsThreshold,
        decimal DiscountPercent,
        decimal? MinimumOrderAmountForDiscount,
        string PerksJson) : ICommand<Response>;

    public record Response(string TierConfigId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(ILoyaltyTierConfigRepository tierConfigRepository)
        {
            RuleFor(x => x.TierConfigId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(tierConfigRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.LoyaltyTierConfigNotFound);

            RuleFor(x => x.LifetimePointsThreshold)
                .GreaterThanOrEqualTo(0)
                .WithMessage(BusinessErrorMessage.MustBePositive);

            RuleFor(x => x.DiscountPercent)
                .Must(p => p >= 0m && p <= 1m)
                .WithMessage(BusinessErrorMessage.PromoCodePercentOutOfRange);

            RuleFor(x => x.MinimumOrderAmountForDiscount)
                .GreaterThanOrEqualTo(0m)
                .When(x => x.MinimumOrderAmountForDiscount.HasValue)
                .WithMessage(BusinessErrorMessage.MustBePositive);

            RuleFor(x => x.PerksJson)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(2000)
                .WithMessage(BusinessErrorMessage.MaxLength)
                .Must(IsValidJson)
                .WithMessage(BusinessErrorMessage.LoyaltyTierPerksJsonInvalid);
        }

        private static bool IsValidJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }
            try
            {
                using var _ = JsonDocument.Parse(json);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }

    public class Handler(ILoyaltyTierConfigRepository tierConfigRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var entity = await tierConfigRepository.GetByIdAsync(command.TierConfigId, cancellationToken);

            entity!.Update(
                lifetimePointsThreshold: command.LifetimePointsThreshold,
                discountPercent: command.DiscountPercent,
                minimumOrderAmountForDiscount: command.MinimumOrderAmountForDiscount,
                perksJson: command.PerksJson,
                actorId: string.Empty);

            return BusinessResult.Success(new Response(entity.Id));
        }
    }
}
