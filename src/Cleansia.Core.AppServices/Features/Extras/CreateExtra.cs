using Microsoft.EntityFrameworkCore;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Extras;

public class CreateExtra
{
    public record Command(
        string Slug,
        string Name,
        string? Description,
        int DisplayOrder,
        Dictionary<string, decimal>? Prices,
        Dictionary<string, TranslationInput>? Translations) : ICommand<Response>;

    public record Response(string ExtraId);

    public record TranslationInput(string Name, string? Description);

    public class Validator : AbstractValidator<Command>
    {
        // Lower-case words joined by single hyphens ("inside-oven"). The slug is the only identifier of
        // an extra that crosses the wire: five client-side maps key by the literal and OrderExtra
        // snapshots it, so it is fixed at creation and spelled in one alphabet.
        private const string SlugPattern = "^[a-z0-9]+(-[a-z0-9]+)*$";

        public Validator(
            IExtraRepository extraRepository,
            ILanguageRepository languageRepository,
            ICurrencyRepository currencyRepository)
        {
            RuleFor(x => x.Slug)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(50)
                .WithMessage(BusinessErrorMessage.MaxLength)
                .Matches(SlugPattern)
                .WithMessage(BusinessErrorMessage.ExtraSlugInvalid)
                .MustAsync(async (slug, ct) =>
                    !await extraRepository.GetAll().AnyAsync(e => e.Slug == slug, ct))
                .WithMessage(BusinessErrorMessage.ExtraSlugAlreadyExists);

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
            var extra = Extra.Create(command.Slug, command.Name, command.Description, command.DisplayOrder);

            foreach (var (languageCode, translation) in command.Translations ?? [])
            {
                extra.SetTranslation(languageCode, translation.Name, translation.Description);
            }

            extraRepository.Add(extra);

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

            // The validator's slug check and this insert cross a snapshot boundary with no lock, so
            // IX_Extras_Slug arbitrates two simultaneous creations. Flush here and own the loser's
            // 23505 -- at the pipeline commit it can only surface as a plain-text 500. Same shape as
            // CreateCurrency.
            try
            {
                await extraRepository.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (DbConstraintViolation.IsUniqueViolation(ex))
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(Command.Slug), BusinessErrorMessage.ExtraSlugAlreadyExists));
            }

            return BusinessResult.Success(new Response(extra.Id));
        }
    }
}
