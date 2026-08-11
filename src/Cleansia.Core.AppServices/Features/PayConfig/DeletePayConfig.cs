using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.PayConfig;

public class DeletePayConfig
{
    public record Command(string PayConfigId) : ICommand<Response>;

    public record Response(string PayConfigId);

    public class Validator : UserEmailValidator<Command>
    {
        private readonly IEmployeePayConfigRepository _payConfigRepository;
        private readonly IOrderEmployeePayRepository _orderEmployeePayRepository;
        private readonly IServiceRepository _serviceRepository;
        private readonly IPackageRepository _packageRepository;
        private readonly IOrderRepository _orderRepository;

        public Validator(
            IUserRepository userRepository,
            IUserSessionProvider userSessionProvider,
            IEmployeePayConfigRepository payConfigRepository,
            IOrderEmployeePayRepository orderEmployeePayRepository,
            IServiceRepository serviceRepository,
            IPackageRepository packageRepository,
            IOrderRepository orderRepository) : base(userRepository, userSessionProvider)
        {
            _payConfigRepository = payConfigRepository;
            _orderEmployeePayRepository = orderEmployeePayRepository;
            _serviceRepository = serviceRepository;
            _packageRepository = packageRepository;
            _orderRepository = orderRepository;

            RuleFor(x => x.PayConfigId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(payConfigRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.PayConfigNotFound)
                .MustAsync(BeNoOrderPaysUsingConfigAsync)
                .WithMessage(BusinessErrorMessage.PayConfigHasOrderPays);

            // The other direction into the same end state: removing the LAST platform-wide row for an
            // entry that is still quoted blanks the pay on every cleaner's board at once. Named per
            // entry, so the admin is told which one to deactivate or reconfigure first.
            RuleFor(x => x)
                .CustomAsync(async (command, context, cancellationToken) =>
                {
                    var blanked = await FindEntryThisDeleteWouldBlankAsync(command.PayConfigId, cancellationToken);
                    if (blanked is null)
                    {
                        return;
                    }

                    context.AddFailure(new ValidationFailure(
                        nameof(Command.PayConfigId),
                        BusinessErrorMessage.PayConfigLastForLiveCatalogueEntry)
                    {
                        ErrorCode = blanked.Name
                    });
                });
        }

        /// <summary>
        /// The entry this delete would leave unquotable, or null when it would not. Both conjuncts are
        /// required: another platform-wide row keeps the estimator resolving (and a per-employee row was
        /// never load-bearing, since the estimator falls back past it), and an entry nothing consults —
        /// deactivated AND on no order — cannot blank a board that never asks about it.
        /// </summary>
        private async Task<PayCoverageTarget?> FindEntryThisDeleteWouldBlankAsync(
            string payConfigId, CancellationToken cancellationToken)
        {
            var config = await _payConfigRepository.GetByIdAsync(payConfigId, cancellationToken);
            if (config is null || config.EmployeeId is not null)
            {
                return null;
            }

            var siblingRemains = await _payConfigRepository.GetAll()
                .AnyAsync(other =>
                    other.Id != payConfigId
                    && other.EmployeeId == null
                    && (config.ServiceId != null
                        ? other.ServiceId == config.ServiceId
                        : other.PackageId == config.PackageId),
                    cancellationToken);

            if (siblingRemains)
            {
                return null;
            }

            if (config.ServiceId is not null)
            {
                var service = await _serviceRepository.GetAll()
                    .Where(s => s.Id == config.ServiceId)
                    .Select(s => new { s.Id, s.Name, s.IsActive })
                    .FirstOrDefaultAsync(cancellationToken);

                if (service is null)
                {
                    return null;
                }

                var carried = await _orderRepository.GetAll()
                    .AnyAsync(o => o.SelectedServices.Any(s => s.ServiceId == config.ServiceId), cancellationToken);

                return service.IsActive || carried
                    ? new PayCoverageTarget(PayCoverageTargetKind.Service, service.Id, service.Name)
                    : null;
            }

            var package = await _packageRepository.GetAll()
                .Where(p => p.Id == config.PackageId)
                .Select(p => new { p.Id, p.Name, p.IsActive })
                .FirstOrDefaultAsync(cancellationToken);

            if (package is null)
            {
                return null;
            }

            var packageCarried = await _orderRepository.GetAll()
                .AnyAsync(o => o.SelectedPackages.Any(p => p.PackageId == config.PackageId), cancellationToken);

            return package.IsActive || packageCarried
                ? new PayCoverageTarget(PayCoverageTargetKind.Package, package.Id, package.Name)
                : null;
        }

        private async Task<bool> BeNoOrderPaysUsingConfigAsync(string payConfigId, CancellationToken cancellationToken)
        {
            var config = await _payConfigRepository.GetByIdAsync(payConfigId, cancellationToken);
            if (config == null) return false;

            // Pay rows don't record the config they were computed under, so dependency is
            // reconstructed the way CalculateOrderPay selects configs: rows whose order carries this
            // config's service/package, narrowed to the override's employee for a per-employee
            // config. A global config also blocks on rows a per-employee override may have shadowed
            // at calc time — deliberately conservative, since shadowing can't be reconstructed from
            // the recorded data.
            var candidateRows = _orderEmployeePayRepository.GetAll();
            if (config.EmployeeId != null)
            {
                candidateRows = candidateRows.Where(pay => pay.EmployeeId == config.EmployeeId);
            }

            var hasOrderPays = config.ServiceId != null
                ? await candidateRows.AnyAsync(
                    pay => pay.Order!.SelectedServices.Any(s => s.ServiceId == config.ServiceId),
                    cancellationToken)
                : await candidateRows.AnyAsync(
                    pay => pay.Order!.SelectedPackages.Any(p => p.PackageId == config.PackageId),
                    cancellationToken);

            return !hasOrderPays;
        }
    }

    public class Handler(
        IEmployeePayConfigRepository payConfigRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var payConfig = await payConfigRepository.GetByIdAsync(command.PayConfigId, cancellationToken);

            payConfigRepository.Remove(payConfig!);

            return BusinessResult.Success(new Response(command.PayConfigId));
        }
    }
}
