using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Functions.Core.Handlers;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

public class SendPreCleaningRemindersHandlerSmokeTests
{
    private readonly Mock<IMediator> _mediator = new();

    [Fact]
    public async Task HandleAsync_Is_Reachable_And_Drives_The_Sweep_Once()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<SendPreCleaningReminders.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new SendPreCleaningReminders.Response(RemindersSent: 0, Considered: 0)));

        var handler = new SendPreCleaningRemindersHandler(
            _mediator.Object,
            NullLogger<SendPreCleaningRemindersHandler>.Instance);

        await handler.HandleAsync(CancellationToken.None);

        _mediator.Verify(
            m => m.Send(It.IsAny<SendPreCleaningReminders.Command>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
