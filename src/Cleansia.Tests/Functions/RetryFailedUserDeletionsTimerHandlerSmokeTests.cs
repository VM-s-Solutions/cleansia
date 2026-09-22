using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Functions.Core.Handlers;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

public class RetryFailedUserDeletionsTimerHandlerSmokeTests
{
    private readonly Mock<IMediator> _mediator = new();

    private RetryFailedUserDeletionsTimerHandler CreateHandler() => new(
        _mediator.Object,
        NullLogger<RetryFailedUserDeletionsTimerHandler>.Instance);

    [Fact]
    public async Task HandleAsync_Is_Reachable_And_Sends_The_Sweep_Once()
    {
        _mediator.Setup(m => m.Send(It.IsAny<RetryFailedUserDeletions.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new RetryFailedUserDeletions.Response(2, 1, 1)));

        await CreateHandler().HandleAsync(CancellationToken.None);

        _mediator.Verify(
            m => m.Send(It.IsAny<RetryFailedUserDeletions.Command>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
