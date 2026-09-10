using Cleansia.Core.AppServices.Features.Catalog;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.PayConfig;

public class BulkCreateEmployeePayConfigs
{
    public record Command(
        string EmployeeId,
        string Grade,
        string CurrencyId,
        bool OverwriteExisting) : ICommand<Response>;

    public record Response(int CreatedCount, int SkippedCount);

    private static decimal GetGradeMultiplier(string grade) => grade.ToLowerInvariant() switch
    {
        "junior" => 0.5m,
        "medior" => 0.75m,
        "senior" => 1.0m,
        _ => 0m
    };

    public class Validator : UserEmailValidator<Command>
    {
        public Validator(
            IUserRepository userRepository,
            IUserSessionProvider userSessionProvider,
            IEmployeeRepository employeeRepository,
            ICurrencyRepository currencyRepository) : base(userRepository, userSessionProvider)
        {
            RuleFor(x => x.EmployeeId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(employeeRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotFound);

            RuleFor(x => x.Grade)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .Must(g => GetGradeMultiplier(g) > 0)
                .WithMessage(BusinessErrorMessage.PayConfigServiceOrPackageRequired);

            RuleFor(x => x.CurrencyId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(currencyRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.InvalidCurrency);
        }
    }

    public class Handler(
        IEmployeePayConfigRepository payConfigRepository,
        IServiceRepository serviceRepository,
        IServicePriceRepository servicePriceRepository,
        IPackagePriceRepository packagePriceRepository,
        IPackageRepository packageRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var multiplier = GetGradeMultiplier(command.Grade);

            var services = await serviceRepository.GetAll().ToListAsync(cancellationToken);
            var packages = await packageRepository.GetAll().ToListAsync(cancellationToken);

            // THE PRICES MUST BE IN THE CURRENCY THE PAY CONFIG IS BEING WRITTEN IN. This derived the
            // pay from the catalogue's own column while passing command.CurrencyId straight through --
            // so generating EUR configs produced EUR pay from CZK numbers, roughly a 24x error in what
            // a cleaner is paid, from one admin button. The join closes it by construction: a currency
            // the catalogue is not priced in has no rows, so nothing is generated from it.
            var servicePrices = await CataloguePriceLookup.ForServicesAsync(
                servicePriceRepository, services.Select(s => s.Id).ToList(), command.CurrencyId, cancellationToken);
            var packagePrices = await CataloguePriceLookup.ForPackagesAsync(
                packagePriceRepository, packages.Select(pk => pk.Id).ToList(), command.CurrencyId, cancellationToken);

            var existingConfigs = await payConfigRepository
                .GetByEmployeeIdAsync(command.EmployeeId, cancellationToken);

            // ONLY THE ROWS IN THE CURRENCY BEING GENERATED. The repository read is per employee and
            // carries every currency, while the unique index is
            // (EmployeeId, ServiceId, PackageId, CurrencyId) -- so a cleaner legitimately holds a CZK
            // rate and a EUR rate for the same service. Deciding "already exists" without the currency
            // term made a EUR run see the CZK row and skip; deciding what to REMOVE without it made an
            // overwrite delete the CZK row to make room for a EUR one.
            var existingInCurrency = existingConfigs
                .Where(c => c.CurrencyId == command.CurrencyId)
                .ToList();

            var existingServiceIds = existingInCurrency
                .Where(c => c.ServiceId != null)
                .Select(c => c.ServiceId!)
                .ToHashSet();

            var existingPackageIds = existingInCurrency
                .Where(c => c.PackageId != null)
                .Select(c => c.PackageId!)
                .ToHashSet();

            var toCreate = new List<EmployeePayConfig>();
            var toRemove = new List<EmployeePayConfig>();
            var skipped = 0;

            foreach (var service in services)
            {
                // THE PRICE GUARD RUNS FIRST, and that order is the point. Not sold in this currency
                // means there is nothing to derive a rate from -- skipped rather than defaulted,
                // because a zero here is a cleaner paid nothing. Deciding that BEFORE the overwrite
                // branch is what stops a bulk run against an unpriced currency from deleting the
                // cleaner's existing rates and creating nothing to replace them.
                if (!servicePrices.TryGetValue(service.Id, out var servicePrice))
                {
                    skipped++;
                    continue;
                }

                if (existingServiceIds.Contains(service.Id))
                {
                    if (command.OverwriteExisting)
                    {
                        toRemove.AddRange(existingInCurrency.Where(c => c.ServiceId == service.Id));
                    }
                    else
                    {
                        skipped++;
                        continue;
                    }
                }

                var basePay = Math.Round(servicePrice.BasePrice * multiplier, 2);
                var extraPerRoom = Math.Round(servicePrice.PerRoomPrice * multiplier, 2);

                var config = EmployeePayConfig.CreateForService(
                    service.Id,
                    basePay,
                    command.CurrencyId,
                    extraPerRoom,
                    extraPerBathroom: 0,
                    distanceRatePerKm: 0,
                    description: $"Auto-generated from {command.Grade} grade template",
                    employeeId: command.EmployeeId);

                toCreate.Add(config);
            }

            foreach (var package in packages)
            {
                // Guard before overwrite -- see the services loop above.
                if (!packagePrices.TryGetValue(package.Id, out var packagePrice))
                {
                    skipped++;
                    continue;
                }

                if (existingPackageIds.Contains(package.Id))
                {
                    if (command.OverwriteExisting)
                    {
                        toRemove.AddRange(existingInCurrency.Where(c => c.PackageId == package.Id));
                    }
                    else
                    {
                        skipped++;
                        continue;
                    }
                }

                var basePay = Math.Round(packagePrice * multiplier, 2);

                var config = EmployeePayConfig.CreateForPackage(
                    package.Id,
                    basePay,
                    command.CurrencyId,
                    extraPerRoom: 0,
                    extraPerBathroom: 0,
                    distanceRatePerKm: 0,
                    description: $"Auto-generated from {command.Grade} grade template",
                    employeeId: command.EmployeeId);

                toCreate.Add(config);
            }

            if (toRemove.Count > 0)
            {
                payConfigRepository.RemoveRange(toRemove);
            }

            payConfigRepository.AddRange(toCreate);

            return BusinessResult.Success(new Response(toCreate.Count, skipped));
        }
    }
}
