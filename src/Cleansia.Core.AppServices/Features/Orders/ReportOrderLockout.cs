using System.Globalization;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// The assigned cleaner reports that they cannot get in (owner ruling 2026-09-28, decision 11): no earlier
/// than <see cref="BookingPolicy.LockoutWaitMinutes"/> after the booked start, with an entrance photo
/// already uploaded as <see cref="PhotoType.Entrance"/> and a note of the calls they made. The report
/// alerts the company's administrators and charges nothing; an administrator confirms it with
/// <see cref="AdminCancelOrderAsLockout"/>. → /product/business-rules#cancellation
/// </summary>
public class ReportOrderLockout
{
    public record Command(string OrderId, string CallAttempts) : ICommand<Response>;

    public record Response(string OrderId, DateTime ReportedAt);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IOrderRepository orderRepository)
        {
            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound);

            RuleFor(x => x.CallAttempts)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(1000)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IOrderPhotoRepository photoRepository,
        IOrderAccessService orderAccessService,
        IAdminNotifier adminNotifier,
        TimeProvider timeProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var employeeId = await orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            var order = string.IsNullOrEmpty(employeeId)
                ? null
                : await orderRepository
                    .GetQueryable()
                    .Include(o => o.AssignedEmployees)
                    .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order is null || !order.AssignedEmployees.Any(ae => ae.EmployeeId == employeeId))
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
            if (RefusalFor(order, nowUtc) is { } refusal)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.OrderId), refusal));
            }

            if (await photoRepository.GetPhotoCountByOrderIdAndTypeAsync(
                    order.Id, PhotoType.Entrance, cancellationToken) == 0)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.OrderId), BusinessErrorMessage.LockoutPhotoRequired));
            }

            order.ReportLockout(employeeId!, command.CallAttempts, nowUtc);

            await adminNotifier.NotifyAsync(
                new AdminEvent(
                    AdminNotificationEventCatalog.OrderLockoutReported,
                    order.TenantId!,
                    Subject: order.Id,
                    Args: new Dictionary<string, string>
                    {
                        ["orderNumber"] = order.DisplayOrderNumber,
                        ["cleaningDateTime"] = DateTime.SpecifyKind(order.CleaningDateTime, DateTimeKind.Utc)
                            .ToString("O", CultureInfo.InvariantCulture),
                        ["orderId"] = order.Id,
                    }),
                cancellationToken);

            return BusinessResult.Success(new Response(order.Id, nowUtc));
        }

        private static string? RefusalFor(Order order, DateTime nowUtc) => order.CurrentStatus switch
        {
            OrderStatus.Cancelled or OrderStatus.Completed => BusinessErrorMessage.LockoutOrderClosed,
            _ when order.LockoutReportedAt is not null => BusinessErrorMessage.LockoutAlreadyReported,
            _ when nowUtc < order.CleaningDateTime.AddMinutes(BookingPolicy.LockoutWaitMinutes)
                => BusinessErrorMessage.LockoutTooEarly,
            _ => null,
        };
    }
}
