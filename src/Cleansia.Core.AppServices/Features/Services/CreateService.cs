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
        decimal BasePrice,
        decimal PerRoomPrice,
        int EstimatedTime,
        Dictionary<string, TranslationInput>? Translations) : ICommand<Response>;

    public record Response(string ServiceId);

    public record TranslationInput(string Name, string Description);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(ILanguageRepository languageRepository, IServiceCategoryRepository categoryRepository)
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

    internal class Handler(IServiceRepository serviceRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var service = Service.Create(
                command.CategoryId,
                command.Name,
                command.Description,
                command.BasePrice,
                command.PerRoomPrice,
                command.EstimatedTime);

            if (command.Translations != null)
            {
                foreach (var (languageCode, translation) in command.Translations)
                {
                    service.SetTranslation(languageCode, translation.Name, translation.Description);
                }
            }

            serviceRepository.Add(service);

            return BusinessResult.Success(new Response(service.Id));
        }
    }
}