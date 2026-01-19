using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Services;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Packages;

public class UpdatePackage
{
    public record Command(
        string PackageId,
        string Name,
        string Description,
        decimal Price,
        List<string>? ServiceIds,
        Dictionary<string, CreateService.TranslationInput>? Translations) : ICommand<Response>;

    public record Response(string PackageId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IPackageRepository packageRepository, IServiceRepository serviceRepository)
        {
            RuleFor(x => x.PackageId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(packageRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.PackageNotFound);

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
        }
    }

    internal class Handler(
        IPackageRepository packageRepository,
        IServiceRepository serviceRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var package = await packageRepository.GetByIdAsync(command.PackageId, cancellationToken);

            package!.Update(
                command.Name,
                command.Description,
                command.Price);

            package.ClearTranslations();
            if (command.Translations != null)
            {
                foreach (var (languageCode, translation) in command.Translations)
                {
                    package.SetTranslation(languageCode, translation.Name, translation.Description);
                }
            }

            package.ClearServices();
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

            return BusinessResult.Success(new Response(package.Id));
        }
    }
}