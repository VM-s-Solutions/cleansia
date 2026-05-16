using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.PayPeriods;

public class DeletePayPeriod
{
    public record Command(string PayPeriodId) : ICommand<Response>;

    public record Response(string PayPeriodId);

    public class Validator : UserEmailValidator<Command>
    {
        private readonly IPayPeriodRepository _payPeriodRepository;
        private readonly IOrderEmployeePayRepository _orderEmployeePayRepository;

        public Validator(
            IUserRepository userRepository,
            IUserSessionProvider userSessionProvider,
            IPayPeriodRepository payPeriodRepository,
            IOrderEmployeePayRepository orderEmployeePayRepository) : base(userRepository, userSessionProvider)
        {
            _payPeriodRepository = payPeriodRepository;
            _orderEmployeePayRepository = orderEmployeePayRepository;

            RuleFor(x => x.PayPeriodId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(payPeriodRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.PayPeriodNotFound)
                .MustAsync(BeOpenStatusAsync)
                .WithMessage(BusinessErrorMessage.PayPeriodNotOpen)
                .MustAsync(BeNoOrderPaysAsync)
                .WithMessage(BusinessErrorMessage.HasOrderPays);
        }

        private async Task<bool> BeOpenStatusAsync(string payPeriodId, CancellationToken cancellationToken)
        {
            var period = await _payPeriodRepository.GetByIdAsync(payPeriodId, cancellationToken);
            return period!.Status == PayPeriodStatus.Open;
        }

        private async Task<bool> BeNoOrderPaysAsync(string payPeriodId, CancellationToken cancellationToken)
        {
            var count = await _orderEmployeePayRepository.GetAll()
                .Where(op => op.PayPeriodId == payPeriodId)
                .CountAsync(cancellationToken);
            return count == 0;
        }
    }

    public class Handler(
        IPayPeriodRepository payPeriodRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var payPeriod = await payPeriodRepository.GetByIdAsync(command.PayPeriodId, cancellationToken);

            payPeriodRepository.Remove(payPeriod!);

            return BusinessResult.Success(new Response(command.PayPeriodId));
        }
    }
}
