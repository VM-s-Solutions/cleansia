using Microsoft.EntityFrameworkCore;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Services;

public class CreateService
{
    public record Command(
        string CategoryId,
        string Name,
        string Description,
        int EstimatedTime,
        Dictionary<string, ServicePriceInput>? Prices,
        Dictionary<string, TranslationInput>? Translations) : ICommand<Response>;

    public record Response(string ServiceId);

    /// <summary>A service's two money components in ONE currency. They travel together.</summary>
    public record ServicePriceInput(decimal BasePrice, decimal PerRoomPrice);

    public record TranslationInput(string Name, string Description);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(
            ILanguageRepository languageRepository,
            IServiceCategoryRepository categoryRepository,
            ICurrencyRepository currencyRepository)
        {
            RuleFor(x => x.CategoryId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(categoryRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.ServiceCategoryNotFound);

            RuleFor(x => x.Name)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(100)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.Description)
                .MaximumLength(500)
                .WithMessage(BusinessErrorMessage.MaxLength);

            // A price per currency, and never a negative one. The coverage half is the business rule --
            // see MustCoverAllActiveCurrencies -- and the sign check is what the two scalar
            // MustBePositive rules that used to live here became.
            RuleFor(x => x.Prices)
                .MustCoverAllActiveCurrencies(currencyRepository)
                .Must(prices => prices!.Values.All(p => p.BasePrice >= 0 && p.PerRoomPrice >= 0))
                .WithMessage(BusinessErrorMessage.MustBePositive);

            RuleFor(x => x.EstimatedTime)
                .GreaterThanOrEqualTo(0)
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
        IServiceRepository serviceRepository,
        IServicePriceRepository servicePriceRepository,
        ICurrencyRepository currencyRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var service = Service.Create(
                command.CategoryId,
                command.Name,
                command.Description,
                command.EstimatedTime);

            if (command.Translations != null)
            {
                foreach (var (languageCode, translation) in command.Translations)
                {
                    service.SetTranslation(languageCode, translation.Name, translation.Description);
                }
            }

            serviceRepository.Add(service);

            // The command still carries a price and the wire has not moved -- what changed is where it
            // LANDS. A catalogue entry has no price of its own any more; it has a price per currency,
            // and this authors the platform default one. That is also what keeps the entry offerable:
            // the customer catalogue withholds anything with no row in the currency being quoted.

            // ONE ROW PER CURRENCY THE FORM SENT, upserted, and rows for a currency the payload does
            // NOT mention are left alone rather than deleted. The form renders a block per active
            // currency, so an absent code is a currency this entry is not sold in -- which the row's
            // absence already says, and is not a reason to destroy a row that exists. Deleting a price
            // stays its own act, not a side effect of an unrelated save.
            //
            // Keyed off EVERY currency rather than the active ones: the validator requires the active
            // set and PERMITS the rest, because pricing a market before opening it is how a currency
            // gets activated at all -- EUR is seeded inactive and gains price rows in the same change
            // that flips it. Those rows have to actually land.
            var byCode = await currencyRepository.GetAll()
                .ToDictionaryAsync(c => c.Code, c => c.Id, cancellationToken);

            foreach (var (code, price) in command.Prices ?? [])
            {
                if (!byCode.TryGetValue(code, out var currencyId))
                {
                    continue;
                }

                var existing = await servicePriceRepository.GetAll().FirstOrDefaultAsync(
                    p => p.ServiceId == service.Id && p.CurrencyId == currencyId, cancellationToken);

                if (existing is null)
                {
                    servicePriceRepository.Add(ServicePrice.Create(
                        service.Id, currencyId, price.BasePrice, price.PerRoomPrice));
                }
                else
                {
                    existing.Update(price.BasePrice, price.PerRoomPrice);
                }
            }

            return BusinessResult.Success(new Response(service.Id));
        }
    }
}