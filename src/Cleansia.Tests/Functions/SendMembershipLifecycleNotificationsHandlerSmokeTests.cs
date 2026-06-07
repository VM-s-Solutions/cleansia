using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Functions.Core.Handlers;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

public class SendMembershipLifecycleNotificationsHandlerSmokeTests
{
    private readonly Mock<IMediator> _mediator = new();

    private SendMembershipLifecycleNotificationsHandler CreateHandler() => new(
        _mediator.Object,
        NullLogger<SendMembershipLifecycleNotificationsHandler>.Instance);

    [Fact]
    public async Task HandleAsync_Is_Reachable_And_Drives_The_Sweep_Once()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<SendMembershipLifecycleNotifications.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(
                new SendMembershipLifecycleNotifications.Response(RenewalRemindersSent: 0, CancellationRemindersSent: 0)));

        var handler = CreateHandler();

        await handler.HandleAsync(CancellationToken.None);

        _mediator.Verify(
            m => m.Send(It.IsAny<SendMembershipLifecycleNotifications.Command>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
