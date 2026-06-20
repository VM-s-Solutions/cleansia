using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.PayConfig;

public class CreatePayConfig
{
    public record Command(
        string? EmployeeId,
        string? ServiceId,
        string? PackageId,
        decimal BasePay,
        decimal ExtraPerRoom,
        decimal ExtraPerBathroom,
        decimal DistanceRatePerKm,
        decimal MinimumPay,
        decimal MaximumPay,
        string CurrencyId,
        string? Description) : ICommand<Response>;

    public record Response(string PayConfigId);

    public class Validator : AbstractValidator<Command>
    {
        private readonly IServiceRepository _serviceRepository;
        private readonly IPackageRepository _packageRepository;
        private readonly ICurrencyRepository _currencyRepository;
        private readonly IEmployeePayConfigRepository _payConfigRepository;
        private readonly IEmployeeRepository _employeeRepository;

        public Validator(
            IUserRepository userRepository,
            IUserSessionProvider userSessionProvider,
            IServiceRepository serviceRepository,
            IPackageRepository packageRepository,
            ICurrencyRepository currencyRepository,
            IEmployeePayConfigRepository payConfigRepository,
            IEmployeeRepository employeeRepository)
        {
            _serviceRepository = serviceRepository;
            _packageRepository = packageRepository;
            _currencyRepository = currencyRepository;
            _payConfigRepository = payConfigRepository;
            _employeeRepository = employeeRepository;

            RuleFor(x => x).SetValidator(new UserEmailValidator<Command>(userRepository, userSessionProvider));

            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .Must(x => !string.IsNullOrEmpty(x.ServiceId) || !string.IsNullOrEmpty(x.PackageId))
                .WithMessage(BusinessErrorMessage.PayConfigServiceOrPackageRequired)
                .Must(x => string.IsNullOrEmpty(x.ServiceId) || string.IsNullOrEmpty(x.PackageId))
                .WithMessage(BusinessErrorMessage.PayConfigCannotHaveBoth);

            RuleFor(x => x.EmployeeId)
                .MustAsync(async (employeeId, ct) =>
                {
                    if (string.IsNullOrEmpty(employeeId)) return true;
                    return await _employeeRepository.ExistsAsync(employeeId, ct);
                })
                .WithMessage(BusinessErrorMessage.EmployeeNotFound);

            RuleFor(x => x.ServiceId)
                .MustAsync(async (serviceId, ct) =>
                {
                    if (string.IsNullOrEmpty(serviceId)) return true;
                    return await _serviceRepository.ExistsAsync(serviceId, ct);
                })
                .WithMessage(BusinessErrorMessage.NotFound)
                .MustAsync(async (cmd, serviceId, ct) =>
                {
                    if (string.IsNullOrEmpty(serviceId)) return true;
                    if (!string.IsNullOrEmpty(cmd.EmployeeId))
                    {
                        return await _payConfigRepository.GetByEmployeeServiceIdAsync(cmd.EmployeeId, serviceId, ct) == null;
                    }
                    return await _payConfigRepository.GetByServiceIdAsync(serviceId, ct) == null;
                })
                .WithMessage(BusinessErrorMessage.PayConfigAlreadyExists);

            RuleFor(x => x.PackageId)
                .MustAsync(async (packageId, ct) =>
                {
                    if (string.IsNullOrEmpty(packageId)) return true;
                    return await _packageRepository.ExistsAsync(packageId, ct);
                })
                .WithMessage(BusinessErrorMessage.NotFound)
                .MustAsync(async (cmd, packageId, ct) =>
                {
                    if (string.IsNullOrEmpty(packageId)) return true;
                    if (!string.IsNullOrEmpty(cmd.EmployeeId))
                    {
                        return await _payConfigRepository.GetByEmployeePackageIdAsync(cmd.EmployeeId, packageId, ct) == null;
                    }
                    return await _payConfigRepository.GetByPackageIdAsync(packageId, ct) == null;
                })
                .WithMessage(BusinessErrorMessage.PayConfigAlreadyExists);

            RuleFor(x => x.BasePay)
                .GreaterThanOrEqualTo(0)
                .WithMessage(BusinessErrorMessage.PayConfigBasePayNegative);

            RuleFor(x => x.ExtraPerRoom)
                .GreaterThanOrEqualTo(0)
                .WithMessage(BusinessErrorMessage.PayConfigExtraPerRoomNegative);

            RuleFor(x => x.ExtraPerBathroom)
                .GreaterThanOrEqualTo(0)
                .WithMessage(BusinessErrorMessage.PayConfigExtraPerBathroomNegative);

            RuleFor(x => x.DistanceRatePerKm)
                .GreaterThanOrEqualTo(0)
                .WithMessage(BusinessErrorMessage.PayConfigDistanceRateNegative);

            RuleFor(x => x.MinimumPay)
                .GreaterThanOrEqualTo(0)
                .WithMessage(BusinessErrorMessage.PayConfigMinimumPayNegative);

            RuleFor(x => x.MaximumPay)
                .GreaterThanOrEqualTo(0)
                .WithMessage(BusinessErrorMessage.PayConfigMaximumPayNegative)
                .Must((cmd, maxPay) => maxPay == 0 || maxPay >= cmd.MinimumPay)
                .WithMessage(BusinessErrorMessage.PayConfigMaximumLessThanMinimum);

            RuleFor(x => x.CurrencyId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(_currencyRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.InvalidCurrency);

            RuleFor(x => x.Description)
                .MaximumLength(500)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }

    }

    public class Handler(
        IEmployeePayConfigRepository payConfigRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            EmployeePayConfig payConfig;

            if (!string.IsNullOrEmpty(command.ServiceId))
            {
                payConfig = EmployeePayConfig.CreateForService(
                    command.ServiceId,
                    command.BasePay,
                    command.CurrencyId,
                    command.ExtraPerRoom,
                    command.ExtraPerBathroom,
                    command.DistanceRatePerKm,
                    command.Description,
                    command.EmployeeId);
            }
            else
            {
                payConfig = EmployeePayConfig.CreateForPackage(
                    command.PackageId!,
                    command.BasePay,
                    command.CurrencyId,
                    command.ExtraPerRoom,
                    command.ExtraPerBathroom,
                    command.DistanceRatePerKm,
                    command.Description,
                    command.EmployeeId);
            }

            if (command.MinimumPay > 0 || command.MaximumPay > 0)
            {
                payConfig.SetPayLimits(command.MinimumPay, command.MaximumPay);
            }

            payConfigRepository.Add(payConfig);

            return BusinessResult.Success(new Response(payConfig.Id));
        }
    }
}
