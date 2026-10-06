using System.Linq.Expressions;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// The assigned cleaner of a signed-in customer's cash booking in progress reports that the customer did not
/// pay at the door (owner ruling 2026-10-06). The job completes, but the payment stays pending: the price less
/// any credit applied is owed as an unpaid-cash receivable, which refuses the customer every new booking until
/// it is paid through its pay link or written off. The cleaner is paid the job's full reward. No receipt is
/// issued, because no money arrived, and the booking earns no loyalty points and qualifies no referral, then
/// or once the debt is paid. The customer is told by push and e-mail, and the company's administrators are
/// alerted. → /product/business-rules#card-guarantee
/// </summary>
public class ReportCashNotPaid
{
    public record Command(string OrderId) : ICommand<Response>;

    public record Response(string OrderId, OrderStatus NewStatus, decimal AmountOwed);

    /// <summary>One chain, so one reason is given, and the crew first, so a cleaner not on the job learns nothing of it.</summary>
    public class Validator : AbstractValidator<Command>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IOrderPhotoRepository _orderPhotoRepository;
        private readonly IOrderAccessService _orderAccessService;
        private readonly IWorkContractAcceptanceRepository _workContractAcceptanceRepository;

        public Validator(
            IOrderRepository orderRepository,
            IEmployeeRepository employeeRepository,
            IOrderPhotoRepository orderPhotoRepository,
            IOrderAccessService orderAccessService,
            IWorkContractAcceptanceRepository workContractAcceptanceRepository)
        {
            _orderRepository = orderRepository;
            _employeeRepository = employeeRepository;
            _orderPhotoRepository = orderPhotoRepository;
            _orderAccessService = orderAccessService;
            _workContractAcceptanceRepository = workContractAcceptanceRepository;

            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (orderId, ct) => await CallerSeatIdAsync(orderId, ct) is not null)
                .WithMessage(BusinessErrorMessage.OrderNotFound)
                .MustAsync(CallerIsApprovedAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotApproved)
                .MustAsync(CallerHasCompletedProfileAsync)
                .WithMessage(BusinessErrorMessage.EmployeeProfileIncomplete)
                .MustAsync(CallerAcceptedTheContractForTheSeatAsync)
                .WithMessage(BusinessErrorMessage.WorkContractAcceptanceRequired)
                .MustAsync((orderId, ct) => OrderIsAsync(orderId, o => o.CurrentStatus == OrderStatus.InProgress, ct))
                .WithMessage(BusinessErrorMessage.OrderNotInProgress)
                .MustAsync((orderId, ct) => OrderIsAsync(orderId, o => o.PaymentType == PaymentType.Cash, ct))
                .WithMessage(BusinessErrorMessage.OrderCashNotAllowedOnCardOrder)
                .MustAsync((orderId, ct) => OrderIsAsync(orderId, o => o.PaymentStatus != PaymentStatus.Paid, ct))
                .WithMessage(BusinessErrorMessage.OrderCashAlreadyCollected)
                // A guest cash booking predates the rule that cash is for an account: there is nobody to owe it.
                .MustAsync((orderId, ct) => OrderIsAsync(
                    orderId, o => o.PaymentStatus == PaymentStatus.Pending && o.UserId != null, ct))
                .WithMessage(BusinessErrorMessage.OrderPaymentNotOutstanding)
                .MustAsync(async (orderId, ct) =>
                    await _orderPhotoRepository.GetPhotoCountByOrderIdAndTypeAsync(orderId, PhotoType.After, ct) > 0)
                .WithMessage(BusinessErrorMessage.AfterPhotosRequired);
        }

        private Task<bool> OrderIsAsync(
            string orderId, Expression<Func<Order, bool>> predicate, CancellationToken cancellationToken) =>
            _orderRepository.GetQueryable().Where(o => o.Id == orderId).AnyAsync(predicate, cancellationToken);

        private async Task<string?> CallerSeatIdAsync(string orderId, CancellationToken cancellationToken)
        {
            var employeeId = await _orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            if (string.IsNullOrEmpty(employeeId))
            {
                return null;
            }

            return await _orderRepository
                .GetQueryable()
                .Where(o => o.Id == orderId)
                .SelectMany(o => o.AssignedEmployees)
                .Where(oe => oe.EmployeeId == employeeId)
                .Select(oe => oe.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private async Task<bool> CallerIsApprovedAsync(string orderId, CancellationToken cancellationToken)
        {
            var employeeId = await _orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            var employee = await _employeeRepository.GetByIdAsync(employeeId!, cancellationToken);
            return employee?.ContractStatus == ContractStatus.Approved;
        }

        private async Task<bool> CallerHasCompletedProfileAsync(string orderId, CancellationToken cancellationToken)
        {
            var employeeId = await _orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            var employee = await _employeeRepository
                .GetQueryable()
                .Include(e => e.Address)
                .FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken);
            return employee?.Address is not null;
        }

        private async Task<bool> CallerAcceptedTheContractForTheSeatAsync(string orderId, CancellationToken cancellationToken) =>
            await CallerSeatIdAsync(orderId, cancellationToken) is { } seatId
            && await _workContractAcceptanceRepository.AnyForSeatAsync(seatId, cancellationToken);
    }

    public class Handler(
        IOrderRepository orderRepository,
        IReceivableRepository receivableRepository,
        IPendingDispatch pending,
        INotificationProducer notificationProducer,
        ILiveActivityProducer liveActivityProducer,
        IAdminNotifier adminNotifier) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.OrderStatusHistory)
                .Include(o => o.AssignedEmployees)
                .Include(o => o.Currency)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order == null)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            order.CompleteOrder(CompleteOrder.Handler.ActualMinutes(order, statedMinutes: null));
            var completed = OrderStatusTrack.Create(OrderStatus.Completed, order);
            order.AddOrderStatus(completed);
            await liveActivityProducer.NotifyOrderTransitionAsync(order, LiveActivityEventKeys.End, completed, cancellationToken);

            var receivable = Receivable.ForUnpaidCash(order);
            receivableRepository.Add(receivable);

            CalculateOrderPay.EnqueueForCrew(order, pending);

            var amount = MoneyText.Format(receivable.Amount, order.Currency!);
            await notificationProducer.NotifyAsync(
                order.UserId!,
                NotificationEventCatalog.OrderCashNotPaid,
                new Dictionary<string, string>
                {
                    ["orderId"] = order.Id,
                    ["orderNumber"] = order.DisplayOrderNumber,
                    ["amount"] = amount,
                },
                order.TenantId,
                receivable.Id,
                cancellationToken);

            var emailKey = MessageKeys.OrderCashNotPaidEmail(receivable.Id);
            pending.Enqueue(
                QueueNames.SendEmail,
                new QueueEnvelope<SendOrderCashNotPaidEmailMessage>(
                    emailKey,
                    order.TenantId,
                    new SendOrderCashNotPaidEmailMessage(receivable.Id, EmailLocale.Resolve(order.LanguageCode), order.TenantId)),
                emailKey);

            await adminNotifier.NotifyAsync(
                new AdminEvent(
                    AdminNotificationEventCatalog.OrderCashNotPaid,
                    order.TenantId!,
                    Subject: order.Id,
                    Args: new Dictionary<string, string>
                    {
                        ["orderNumber"] = order.DisplayOrderNumber,
                        ["amount"] = amount,
                        ["orderId"] = order.Id,
                    }),
                cancellationToken);

            return BusinessResult.Success(new Response(order.Id, OrderStatus.Completed, receivable.Amount));
        }
    }
}
