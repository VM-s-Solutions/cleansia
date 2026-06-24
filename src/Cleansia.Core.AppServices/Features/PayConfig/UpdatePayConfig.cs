using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.PayConfig;

[AuditAction("payconfig.update", Sensitive = true, ResourceType = "EmployeePayConfig")]
public class UpdatePayConfig
{
    public record Command(
        string PayConfigId,
        decimal BasePay,
        decimal ExtraPerRoom,
        decimal ExtraPerBathroom,
        decimal DistanceRatePerKm,
        decimal MinimumPay,
        decimal MaximumPay,
        string? Description) : ICommand<Response>;

    public record Response(string PayConfigId);

    public record PayRatesSnapshot(
        string PayConfigId,
        string? EmployeeId,
        string? ServiceId,
        string? PackageId,
        decimal BasePay,
        decimal ExtraPerRoom,
        decimal ExtraPerBathroom,
        decimal DistanceRatePerKm,
        decimal MinimumPay,
        decimal MaximumPay);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(
            IUserRepository userRepository,
            IUserSessionProvider userSessionProvider,
            IEmployeePayConfigRepository payConfigRepository)
        {
            RuleFor(x => x).SetValidator(new UserEmailValidator<Command>(userRepository, userSessionProvider));

            RuleFor(x => x.PayConfigId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(payConfigRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.PayConfigNotFound);

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

            RuleFor(x => x.Description)
                .MaximumLength(500)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }
    }

    public class Handler(
        IEmployeePayConfigRepository payConfigRepository,
        IAuditContext auditContext)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var payConfig = await payConfigRepository.GetByIdAsync(command.PayConfigId, cancellationToken);

            var before = Snapshot(payConfig!);

            payConfig!.UpdatePayRates(
                command.BasePay,
                command.ExtraPerRoom,
                command.ExtraPerBathroom,
                command.DistanceRatePerKm);

            payConfig.SetPayLimits(command.MinimumPay, command.MaximumPay);

            auditContext.RecordChange(
                "EmployeePayConfig",
                payConfig.Id,
                before,
                Snapshot(payConfig));

            return BusinessResult.Success(new Response(payConfig.Id));
        }

        private static PayRatesSnapshot Snapshot(Domain.EmployeePayroll.EmployeePayConfig config) =>
            new(
                config.Id,
                config.EmployeeId,
                config.ServiceId,
                config.PackageId,
                config.BasePay,
                config.ExtraPerRoom,
                config.ExtraPerBathroom,
                config.DistanceRatePerKm,
                config.MinimumPay,
                config.MaximumPay);
    }
}
