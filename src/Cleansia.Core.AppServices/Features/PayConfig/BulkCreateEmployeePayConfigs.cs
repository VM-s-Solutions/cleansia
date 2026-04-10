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
        IPackageRepository packageRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var multiplier = GetGradeMultiplier(command.Grade);

            var services = await serviceRepository.GetAll().ToListAsync(cancellationToken);
            var packages = await packageRepository.GetAll().ToListAsync(cancellationToken);

            var existingConfigs = await payConfigRepository
                .GetByEmployeeId(command.EmployeeId)
                .ToListAsync(cancellationToken);

            var existingServiceIds = existingConfigs
                .Where(c => c.ServiceId != null)
                .Select(c => c.ServiceId!)
                .ToHashSet();

            var existingPackageIds = existingConfigs
                .Where(c => c.PackageId != null)
                .Select(c => c.PackageId!)
                .ToHashSet();

            var toCreate = new List<EmployeePayConfig>();
            var toRemove = new List<EmployeePayConfig>();
            var skipped = 0;

            foreach (var service in services)
            {
                if (existingServiceIds.Contains(service.Id))
                {
                    if (command.OverwriteExisting)
                    {
                        toRemove.AddRange(existingConfigs.Where(c => c.ServiceId == service.Id));
                    }
                    else
                    {
                        skipped++;
                        continue;
                    }
                }

                var basePay = Math.Round(service.BasePrice * multiplier, 2);
                var extraPerRoom = Math.Round(service.PerRoomPrice * multiplier, 2);

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
                if (existingPackageIds.Contains(package.Id))
                {
                    if (command.OverwriteExisting)
                    {
                        toRemove.AddRange(existingConfigs.Where(c => c.PackageId == package.Id));
                    }
                    else
                    {
                        skipped++;
                        continue;
                    }
                }

                var basePay = Math.Round(package.Price * multiplier, 2);

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
