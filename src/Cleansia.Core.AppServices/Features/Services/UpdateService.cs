using Microsoft.EntityFrameworkCore;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Services;

public class UpdateService
{
    public record Command(
        string ServiceId,
        string CategoryId,
        string Name,
        string Description,
        decimal BasePrice,
        decimal PerRoomPrice,
        int EstimatedTime,
        Dictionary<string, CreateService.TranslationInput>? Translations) : ICommand<Response>;

    public record Response(string ServiceId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IServiceRepository serviceRepository, ILanguageRepository languageRepository, IServiceCategoryRepository categoryRepository)
        {
            RuleFor(x => x.ServiceId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(serviceRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.ServiceNotFound);

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

            RuleFor(x => x.BasePrice)
                .GreaterThanOrEqualTo(0)
                .WithMessage(BusinessErrorMessage.MustBePositive);

            RuleFor(x => x.PerRoomPrice)
                .GreaterThanOrEqualTo(0)
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
            var service = await serviceRepository.GetByIdAsync(command.ServiceId, cancellationToken);
            if (service is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.ServiceId), BusinessErrorMessage.ServiceNotFound));
            }

            service.Update(
                command.CategoryId,
                command.Name,
                command.Description,
                command.EstimatedTime);

            // The price is a row now, not a column -- see CreateService. Upsert rather than update,
            // because a service can reach here without a row for the default currency: one seeded
            // before that currency existed, or one whose row was removed.
            var currency = await currencyRepository.GetDefaultAsync(cancellationToken);
            var existing = await servicePriceRepository.GetAll()
                .FirstOrDefaultAsync(
                    p => p.ServiceId == service.Id && p.CurrencyId == currency.Id, cancellationToken);
            if (existing is null)
            {
                servicePriceRepository.Add(ServicePrice.Create(
                    service.Id, currency.Id, command.BasePrice, command.PerRoomPrice));
            }
            else
            {
                existing.Update(command.BasePrice, command.PerRoomPrice);
            }

            service.ClearTranslations();
            if (command.Translations != null)
            {
                foreach (var (languageCode, translation) in command.Translations)
                {
                    service.SetTranslation(languageCode, translation.Name, translation.Description);
                }
            }

            return BusinessResult.Success(new Response(service.Id));
        }
    }
}