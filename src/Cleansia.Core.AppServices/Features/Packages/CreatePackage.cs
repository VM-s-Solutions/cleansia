using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.AppServices.Features.Services;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Packages;

public class CreatePackage
{
    public record Command(
        string Name,
        string Description,
        decimal Price,
        List<string>? ServiceIds,
        Dictionary<string, CreateService.TranslationInput>? Translations) : ICommand<Response>;

    public record Response(string PackageId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IServiceRepository serviceRepository, ILanguageRepository languageRepository)
        {
            RuleFor(x => x.Name)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(100)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.Description)
                .MaximumLength(500)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.Price)
                .GreaterThanOrEqualTo(0)
                .WithMessage(BusinessErrorMessage.MustBePositive);

            RuleFor(x => x.ServiceIds)
                .MustAsync(async (serviceIds, ct) =>
                {
                    if (serviceIds == null || serviceIds.Count == 0) return true;
                    return await serviceRepository.ExistWithIdsAsync(serviceIds, ct);
                })
                .WithMessage(BusinessErrorMessage.ServiceNotFound);

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
        IPackageRepository packageRepository,
        IServiceRepository serviceRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var package = Package.Create(
                command.Name,
                command.Description,
                command.Price);

            if (command.Translations != null)
            {
                foreach (var (languageCode, translation) in command.Translations)
                {
                    package.SetTranslation(languageCode, translation.Name, translation.Description);
                }
            }

            if (command.ServiceIds != null && command.ServiceIds.Count > 0)
            {
                foreach (var serviceId in command.ServiceIds)
                {
                    var service = await serviceRepository.GetByIdAsync(serviceId, cancellationToken);
                    if (service != null)
                    {
                        package.AddService(service);
                    }
                }
            }

            packageRepository.Add(package);

            return BusinessResult.Success(new Response(package.Id));
        }
    }
}