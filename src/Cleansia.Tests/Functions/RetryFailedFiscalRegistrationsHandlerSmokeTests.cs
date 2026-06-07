using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Functions.Core.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

public class RetryFailedFiscalRegistrationsHandlerSmokeTests
{
    private readonly Mock<IFiscalRetryService> _fiscalRetryService = new();

    private RetryFailedFiscalRegistrationsHandler CreateHandler() => new(
        _fiscalRetryService.Object,
        NullLogger<RetryFailedFiscalRegistrationsHandler>.Instance);

    [Fact]
    public async Task HandleAsync_Is_Reachable_And_Processes_Due_Retries_Once()
    {
        _fiscalRetryService
            .Setup(s => s.ProcessDueRetriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var handler = CreateHandler();

        await handler.HandleAsync(CancellationToken.None);

        _fiscalRetryService.Verify(
            s => s.ProcessDueRetriesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
