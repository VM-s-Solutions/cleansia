using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Functions.Core.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

public class SendNewJobsDigestTimerHandlerSmokeTests
{
    private readonly Mock<INewJobsDigestService> _digestService = new();

    private SendNewJobsDigestTimerHandler CreateHandler() => new(
        _digestService.Object,
        NullLogger<SendNewJobsDigestTimerHandler>.Instance);

    [Fact]
    public async Task HandleAsync_Is_Reachable_And_Sends_Digests_Once()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(CancellationToken.None);

        _digestService.Verify(
            s => s.SendDigestsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
