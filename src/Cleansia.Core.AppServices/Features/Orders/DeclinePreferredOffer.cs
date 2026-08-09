using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// ADR-0045 D3b — the cleaner says no, in one tap. <b>One write:</b>
/// <c>PreferredHoldUntilUtc = now</c>, which is ADR-0036 D2 consequence #4 executed verbatim — no new
/// column, no new state, no status transition. The beneficiary stays on the row so the customer's
/// re-offer can refuse the same person without anybody being told who it was.
///
/// <para>Nothing is recorded about the refusal (D13, owner ruling <c>Q-ASSIGN-03</c>): after the write
/// the platform cannot tell a decline from a silence, and cannot tell how many either has produced.
/// That is deliberate — a policy nobody has ruled must not accrete from data collected just in
/// case — and it means any future "declines N times loses X" rule needs a schema change and a
/// backfill-less start date first.</para>
/// </summary>
public class DeclinePreferredOffer
{
    public record Command(string OrderId) : ICommand<Response>;

    public record Response(string OrderId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IOrderAccessService orderAccessService,
        INotificationProducer notificationProducer) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var nowUtc = DateTime.UtcNow;
            var employeeId = await orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);

            // The SAME existence gate TakeOrder uses, answering with the SAME error: a held order has
            // to be indistinguishable from a missing one, or the fact that some other cleaner was named
            // is inferable from the refusal (ADR-0036 D5.2). A caller with no employee id is nobody's
            // beneficiary and is held out by the rule itself.
            var order = await orderRepository
                .GetQueryable()
                .Where(OrderVisibility.NotHeldFrom(employeeId, nowUtc))
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order is null)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            // Idempotent, and silent for anyone who is not the beneficiary: a second decline finds the
            // reservation already over and a bystander's call writes nothing. Reporting either as an
            // error would let a caller probe whether an order carries a live reservation.
            if (!PreferredOffer.HasLiveReservation(
                    order.PreferredEmployeeId, order.PreferredHoldUntilUtc, nowUtc)
                || order.PreferredEmployeeId != employeeId)
            {
                return BusinessResult.Success(new Response(order.Id));
            }

            order.EndPreferredHold(nowUtc);
            await PreferredOfferClosedNotifier.NotifyPreferredOfferClosedAsync(
                order, notificationProducer, nowUtc, cancellationToken);

            return BusinessResult.Success(new Response(order.Id));
        }
    }
}
