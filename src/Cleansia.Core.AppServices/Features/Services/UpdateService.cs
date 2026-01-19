using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Services;

public class UpdateService
{
    public record Command(
        string ServiceId,
        string Name,
        string Description,
        decimal BasePrice,
        decimal PerRoomPrice,
        int EstimatedTime,
        Dictionary<string, CreateService.TranslationInput>? Translations) : ICommand<Response>;

    public record Response(string ServiceId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IServiceRepository serviceRepository, ILanguageRepository languageRepository)
        {
            RuleFor(x => x.ServiceId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(serviceRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.ServiceNotFound);

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
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .WithMessage(BusinessErrorMessage.TranslationsRequired)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.TranslationsRequired)
                .MustAsync(async (translations, cancellationToken) =>
                {
                    var allLanguages = await languageRepository.GetAll().ToListAsync(cancellationToken);
                    var allLanguageCodes = allLanguages.Select(l => l.Code).ToHashSet();
                    var providedCodes = translations!.Keys.ToHashSet();
                    return allLanguageCodes.SetEquals(providedCodes);
                })
                .WithMessage(BusinessErrorMessage.MissingTranslationForLanguage);

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

    internal class Handler(IServiceRepository serviceRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var service = await serviceRepository.GetByIdAsync(command.ServiceId, cancellationToken);

            service!.Update(
                command.Name,
                command.Description,
                command.BasePrice,
                command.PerRoomPrice,
                command.EstimatedTime);

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