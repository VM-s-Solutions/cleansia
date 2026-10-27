using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Closes one fully marked crew job under its own company through the shared completion.
/// Its own DI scope discards a failed completion. → /decisions/adr-0072
/// </summary>
public class AutoCompleteMarkedDoneOrder
{
    /// <param name="CutoffUtc">The sweep's cutoff: a job whose last mark is later is left alone.</param>
    public record Command(string OrderId, DateTime CutoffUtc) : ICommand<Response>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.OrderId).NotEmpty().WithMessage(BusinessErrorMessage.Required);

            // Bare, like the sweep's own bound: a scheduler sends this, so a translated key would be read by nobody.
            RuleFor(x => x.CutoffUtc).Must(cutoff =>
                cutoff <= DateTime.UtcNow.AddMinutes(-AutoCompleteMarkedDoneOrders.MinGraceMinutes));
        }
    }

    public record Response(bool Completed);

    /// <summary>
    /// A job under way whose whole crew has marked their parts done, the last of them at or before the cutoff,
    /// with its money as a cleaner's Complete needs it.
    /// </summary>
    internal static IQueryable<Order> Qualifying(IQueryable<Order> orders, DateTime cutoffUtc) =>
        orders
            .Where(o => o.CurrentStatus == OrderStatus.InProgress
                && o.AssignedEmployees.Any()
                && o.AssignedEmployees.All(oe => oe.CompletionConfirmedAt != null && oe.CompletionConfirmedAt <= cutoffUtc))
            .Where(CompleteOrder.CashIsCollected)
            .Where(CompleteOrder.CardIsCharged);

    public class Handler(
        IOrderRepository orderRepository,
        IOrderPhotoRepository orderPhotoRepository,
        ITenantProvider tenantProvider,
        IPendingDispatch pending,
        INotificationProducer notificationProducer,
        ILiveActivityProducer liveActivityProducer,
        ILoyaltyService loyaltyService,
        IReferralService referralService) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            // Before the job is read: a cleaner completing it at the same moment either commits first and the
            // job no longer qualifies here, or waits on the row and then finds it completed.
            await orderRepository.LockForCrewChangeAsync(command.OrderId, cancellationToken);

            var order = await CompleteOrder.IncludeForCompletion(
                    Qualifying(orderRepository.GetQueryableIgnoringTenant(), command.CutoffUtc))
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order is null)
            {
                return BusinessResult.Success(new Response(false));
            }

            // The rows the completion writes are stamped from the ambient tenant at commit, and a sweep has no
            // JWT; the photo count below reads under it too.
            tenantProvider.ClearTenantOverride();
            if (!string.IsNullOrEmpty(order.TenantId))
            {
                tenantProvider.SetTenantOverride(order.TenantId);
            }

            if (await orderPhotoRepository.GetPhotoCountByOrderIdAndTypeAsync(order.Id, PhotoType.After, cancellationToken) == 0)
            {
                return BusinessResult.Success(new Response(false));
            }

            // Timed to the last mark: the wait for somebody to press Complete was not work.
            var lastMark = order.AssignedEmployees.Max(oe => oe.CompletionConfirmedAt!.Value);
            var actualMinutes = CompleteOrder.Handler.ActualMinutes(
                order, statedMinutes: null, new DateTimeOffset(DateTime.SpecifyKind(lastMark, DateTimeKind.Utc)));

            await CompleteOrder.FinishAsync(
                order,
                actualMinutes,
                completionNotes: null,
                pending,
                notificationProducer,
                liveActivityProducer,
                loyaltyService,
                referralService,
                cancellationToken);

            return BusinessResult.Success(new Response(true));
        }
    }
}
