using Microsoft.EntityFrameworkCore;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Extras;

/// <summary>
/// NO <c>Slug</c> ON THIS COMMAND, by construction. OrderExtras.Slug snapshots the slug at purchase and
/// the partner app keys a cleaner's per-order checklist by it, so a rename would re-point history. The
/// wire cannot carry one, and the reflection test in UpdateExtraSlugImmutabilityTests keeps it that way.
/// </summary>
public class UpdateExtra
{
    public record Command(
        string ExtraId,
        string Name,
        string? Description,
        int DisplayOrder,
        Dictionary<string, decimal>? Prices,
        Dictionary<string, CreateExtra.TranslationInput>? Translations) : ICommand<Response>;

    public record Response(string ExtraId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(
            IExtraRepository extraRepository,
            ILanguageRepository languageRepository,
            ICurrencyRepository currencyRepository)
        {
            RuleFor(x => x.ExtraId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(extraRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.ExtraNotFound);

            RuleFor(x => x.Name)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(100)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.Description)
                .MaximumLength(500)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.DisplayOrder)
                .GreaterThanOrEqualTo(0)
                .WithMessage(BusinessErrorMessage.MustBePositive);

            // A price per currency, and never a negative one -- see CreateService.
            RuleFor(x => x.Prices)
                .MustCoverAllActiveCurrencies(currencyRepository)
                .Must(prices => prices!.Values.All(p => p >= 0))
                .WithMessage(BusinessErrorMessage.MustBePositive);

            RuleFor(x => x.Translations)
                .MustCoverAllActiveLanguages(languageRepository);

            RuleForEach(x => x.Translations)
                .ChildRules(translation =>
                {
                    translation.RuleFor(t => t.Value.Name)
                        .Cascade(CascadeMode.Stop)
                        .NotEmpty()
                        .WithMessage(BusinessErrorMessage.Required)
                        .MaximumLength(100)
                        .WithMessage(BusinessErrorMessage.MaxLength);

                    translation.RuleFor(t => t.Value.Description)
                        .MaximumLength(500)
                        .WithMessage(BusinessErrorMessage.MaxLength);
                });
        }
    }

    internal class Handler(
        IExtraRepository extraRepository,
        IExtraPriceRepository extraPriceRepository,
        ICurrencyRepository currencyRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var extra = await extraRepository.GetByIdAsync(command.ExtraId, cancellationToken);
            if (extra is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.ExtraId), BusinessErrorMessage.ExtraNotFound));
            }

            extra.Update(command.Name, command.Description, command.DisplayOrder);

            // ONE ROW PER CURRENCY THE FORM SENT, upserted, keyed off every currency rather than the
            // active ones; rows for a currency the payload does NOT mention are left alone rather than
            // deleted -- see CreateService for both halves of the reasoning.
            var byCode = await currencyRepository.GetAll()
                .ToDictionaryAsync(c => c.Code, c => c.Id, cancellationToken);

            foreach (var (code, price) in command.Prices ?? [])
            {
                if (!byCode.TryGetValue(code, out var currencyId))
                {
                    continue;
                }

                var existing = await extraPriceRepository.GetAll().FirstOrDefaultAsync(
                    p => p.ExtraId == extra.Id && p.CurrencyId == currencyId, cancellationToken);

                if (existing is null)
                {
                    extraPriceRepository.Add(ExtraPrice.Create(extra.Id, currencyId, price));
                }
                else
                {
                    existing.Update(price);
                }
            }

            extra.ClearTranslations();
            foreach (var (languageCode, translation) in command.Translations ?? [])
            {
                extra.SetTranslation(languageCode, translation.Name, translation.Description);
            }

            return BusinessResult.Success(new Response(extra.Id));
        }
    }
}
