using Microsoft.EntityFrameworkCore;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.AppServices.Features.Packages.DTOs;
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
        string? Tagline,
        bool IsPopular,
        Dictionary<string, decimal>? Prices,
        List<string>? ServiceIds,
        Dictionary<string, PackageTranslationInput>? Translations) : ICommand<Response>;

    public record Response(string PackageId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(
            IServiceRepository serviceRepository,
            ILanguageRepository languageRepository,
            ICurrencyRepository currencyRepository)
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

            RuleFor(x => x.Tagline)
                .MaximumLength(60)
                .WithMessage(BusinessErrorMessage.MaxLength);

            // A price per currency, and never a negative one -- see CreateService.
            RuleFor(x => x.Prices)
                .MustCoverAllActiveCurrencies(currencyRepository)
                .Must(prices => prices!.Values.All(p => p >= 0))
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

                    translation.RuleFor(t => t.Value.Tagline)
                        .MaximumLength(60)
                        .WithMessage(BusinessErrorMessage.MaxLength);
                });
        }
    }

    internal class Handler(
        IPackageRepository packageRepository,
        IPackagePriceRepository packagePriceRepository,
        ICurrencyRepository currencyRepository,
        IServiceRepository serviceRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var package = Package.Create(
                command.Name,
                command.Description,
                command.Tagline,
                command.IsPopular);

            if (command.Translations != null)
            {
                foreach (var (languageCode, translation) in command.Translations)
                {
                    package.SetTranslation(languageCode, translation.Name, translation.Description, translation.Tagline);
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


            // ONE ROW PER CURRENCY THE FORM SENT, upserted -- see CreateService for why rows the payload
            // does not mention are left alone, and why this keys off every currency rather than the
            // active ones.
            var byCode = await currencyRepository.GetAll()
                .ToDictionaryAsync(c => c.Code, c => c.Id, cancellationToken);

            foreach (var (code, price) in command.Prices ?? [])
            {
                if (!byCode.TryGetValue(code, out var currencyId))
                {
                    continue;
                }

                var existingPrice = await packagePriceRepository.GetAll().FirstOrDefaultAsync(
                    p => p.PackageId == package.Id && p.CurrencyId == currencyId, cancellationToken);

                if (existingPrice is null)
                {
                    packagePriceRepository.Add(PackagePrice.Create(package.Id, currencyId, price));
                }
                else
                {
                    existingPrice.Update(price);
                }
            }

            return BusinessResult.Success(new Response(package.Id));
        }
    }
}