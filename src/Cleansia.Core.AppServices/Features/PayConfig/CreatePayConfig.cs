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

    public class Validator : UserEmailValidator<Command>
    {
        private readonly IServiceRepository _serviceRepository;
        private readonly IPackageRepository _packageRepository;
        private readonly ICurrencyRepository _currencyRepository;
        private readonly IEmployeePayConfigRepository _payConfigRepository;

        public Validator(
            IUserRepository userRepository,
            IUserSessionProvider userSessionProvider,
            IServiceRepository serviceRepository,
            IPackageRepository packageRepository,
            ICurrencyRepository currencyRepository,
            IEmployeePayConfigRepository payConfigRepository) : base(userRepository, userSessionProvider)
        {
            _serviceRepository = serviceRepository;
            _packageRepository = packageRepository;
            _currencyRepository = currencyRepository;
            _payConfigRepository = payConfigRepository;

            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .Must(x => !string.IsNullOrEmpty(x.ServiceId) || !string.IsNullOrEmpty(x.PackageId))
                .WithMessage(BusinessErrorMessage.PayConfigServiceOrPackageRequired)
                .Must(x => string.IsNullOrEmpty(x.ServiceId) || string.IsNullOrEmpty(x.PackageId))
                .WithMessage(BusinessErrorMessage.PayConfigCannotHaveBoth);

            RuleFor(x => x.ServiceId)
                .MustAsync(async (serviceId, ct) =>
                {
                    if (string.IsNullOrEmpty(serviceId)) return true;
                    return await _serviceRepository.ExistsAsync(serviceId, ct);
                })
                .WithMessage(BusinessErrorMessage.NotFound)
                .MustAsync(BeNoExistingConfigForServiceAsync)
                .WithMessage(BusinessErrorMessage.PayConfigAlreadyExists);

            RuleFor(x => x.PackageId)
                .MustAsync(async (packageId, ct) =>
                {
                    if (string.IsNullOrEmpty(packageId)) return true;
                    return await _packageRepository.ExistsAsync(packageId, ct);
                })
                .WithMessage(BusinessErrorMessage.NotFound)
                .MustAsync(BeNoExistingConfigForPackageAsync)
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

        private Task<bool> BeNoExistingConfigForServiceAsync(string? serviceId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(serviceId)) return Task.FromResult(true);
            return _payConfigRepository.GetByServiceIdAsync(serviceId, cancellationToken)
                .ContinueWith(t => t.Result == null, cancellationToken);
        }

        private Task<bool> BeNoExistingConfigForPackageAsync(string? packageId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(packageId)) return Task.FromResult(true);
            return _payConfigRepository.GetByPackageIdAsync(packageId, cancellationToken)
                .ContinueWith(t => t.Result == null, cancellationToken);
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
                    command.Description);
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
                    command.Description);
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
