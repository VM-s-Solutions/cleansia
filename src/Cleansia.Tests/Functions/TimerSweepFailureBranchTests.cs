using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Referrals;
using Cleansia.Functions.Core.Handlers;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// AC8 (failure half) — the existing smoke tests pin each mediator-backed sweep's success path; these pin
/// the uncovered failure `else` branch. When the inner command returns a non-success BusinessResult, the
/// timer handler logs the error and RETURNS — it does NOT throw. A sweep is non-core background work, so a
/// failed tick must not surface as an unhandled exception (which the runtime would treat as a failed
/// invocation); the next scheduled tick retries. The command still ran exactly once.
/// </summary>
public class TimerSweepFailureBranchTests
{
    private readonly Mock<IMediator> _mediator = new();

    private static BusinessResult<T> Fail<T>() =>
        BusinessResult.Failure<T>(new Error("sweep", BusinessErrorMessage.Required));

    [Fact]
    public async Task AutoCancelStaleRecurringOrders_Failure_Result_Does_Not_Throw()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<AutoCancelStaleRecurringOrders.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Fail<AutoCancelStaleRecurringOrders.Response>());

        var handler = new AutoCancelStaleRecurringOrdersHandler(
            _mediator.Object, NullLogger<AutoCancelStaleRecurringOrdersHandler>.Instance);

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(CancellationToken.None));

        Assert.Null(ex);
        _mediator.Verify(
            m => m.Send(It.IsAny<AutoCancelStaleRecurringOrders.Command>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CleanupStalePendingOrders_Failure_Result_Does_Not_Throw()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<CleanupStalePendingOrders.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Fail<CleanupStalePendingOrders.Response>());

        var handler = new CleanupStalePendingOrdersHandler(
            _mediator.Object, NullLogger<CleanupStalePendingOrdersHandler>.Instance);

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(CancellationToken.None));

        Assert.Null(ex);
    }

    [Fact]
    public async Task MaterializeRecurringBookings_Failure_Result_Does_Not_Throw()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<MaterializeRecurringBookings.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Fail<MaterializeRecurringBookings.Response>());

        var handler = new MaterializeRecurringBookingsHandler(
            _mediator.Object, NullLogger<MaterializeRecurringBookingsHandler>.Instance);

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(CancellationToken.None));

        Assert.Null(ex);
    }

    [Fact]
    public async Task SendMembershipLifecycleNotifications_Failure_Result_Does_Not_Throw()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<SendMembershipLifecycleNotifications.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Fail<SendMembershipLifecycleNotifications.Response>());

        var handler = new SendMembershipLifecycleNotificationsHandler(
            _mediator.Object, NullLogger<SendMembershipLifecycleNotificationsHandler>.Instance);

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(CancellationToken.None));

        Assert.Null(ex);
    }

    [Fact]
    public async Task SendRecurringOrderReminders_Failure_Result_Does_Not_Throw()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<SendRecurringOrderReminders.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Fail<SendRecurringOrderReminders.Response>());

        var handler = new SendRecurringOrderRemindersHandler(
            _mediator.Object, NullLogger<SendRecurringOrderRemindersHandler>.Instance);

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(CancellationToken.None));

        Assert.Null(ex);
    }

    [Fact]
    public async Task ExpireStaleReferrals_Failure_Result_Does_Not_Throw()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<ExpireStaleReferrals.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Fail<ExpireStaleReferrals.Response>());

        var handler = new ExpireStaleReferralsHandler(
            _mediator.Object, NullLogger<ExpireStaleReferralsHandler>.Instance);

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(CancellationToken.None));

        Assert.Null(ex);
    }
}
