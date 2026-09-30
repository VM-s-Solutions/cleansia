using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// A guest's "the cleaner did not arrive", offered once the booked start has passed and self-cancel is
/// refused. A signed-in customer files a service-not-provided dispute instead; both raise the same
/// administrator alert, and only an administrator's confirmation moves money.
/// → /product/business-rules#when-the-cleaner-cancels-or-no-shows
/// </summary>
public class ReportGuestCleanerNoShow
{
    public record Command(string AccessToken) : ICommand<Response>, IGuestOrderScopedRequest
    {
        string? IOperatorScopedRequest.CountryId => null;
    }

    public record Response(string OrderId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.AccessToken).NotEmpty().WithMessage(BusinessErrorMessage.Required);
        }
    }

    public class Handler(
        GuestOrderAccess guestOrderAccess,
        IAdminNotifier adminNotifier,
        IUserNotificationRepository userNotificationRepository,
        TimeProvider timeProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var order = await guestOrderAccess.OrdersForKey(command)
                .Include(o => o.AssignedEmployees)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);
            if (order is null)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.AccessToken), BusinessErrorMessage.OrderNotFound));
            }

            if (CleanerNoShow.RefusalFor(order, timeProvider.GetUtcNow().UtcDateTime) is { } refusal)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.AccessToken), refusal));
            }

            // With nobody assigned there is no cleaner to be missing: the unfilled sweep cancels and refunds
            // that booking on its own.
            if (order.AssignedEmployees.Count > 0)
            {
                await CleanerNoShow.AlertAsync(order, adminNotifier, userNotificationRepository, cancellationToken);
            }

            return BusinessResult.Success(new Response(order.Id));
        }
    }
}
