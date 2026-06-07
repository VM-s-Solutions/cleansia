using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Functions.Core.Handlers;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

public class MaterializeRecurringBookingsHandlerSmokeTests
{
    private readonly Mock<IMediator> _mediator = new();

    private MaterializeRecurringBookingsHandler CreateHandler() => new(
        _mediator.Object,
        NullLogger<MaterializeRecurringBookingsHandler>.Instance);

    [Fact]
    public async Task HandleAsync_Is_Reachable_And_Drives_The_Sweep_Once()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<MaterializeRecurringBookings.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new MaterializeRecurringBookings.Response(OrdersCreated: 0, TemplatesProcessed: 0)));

        var handler = CreateHandler();

        await handler.HandleAsync(CancellationToken.None);

        _mediator.Verify(
            m => m.Send(It.IsAny<MaterializeRecurringBookings.Command>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
