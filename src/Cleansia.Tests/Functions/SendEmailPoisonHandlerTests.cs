using Cleansia.Core.Queue.Abstractions;
using Cleansia.Functions.Core.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// AC1/AC8 — the send-email poison consumer is the one PoisonHandlerBase subclass with no dedicated
/// construction test (PoisonHandlerTests covers the other five). It records the dead-letter row tagged
/// with the send-email source queue and acks without throwing, matching the shared base contract.
/// </summary>
public class SendEmailPoisonHandlerTests
{
    private readonly Mock<IDeadLetterStore> _store = new();

    [Fact]
    public async Task Records_DeadLetter_With_SendEmail_Source_Queue_And_Does_Not_Throw()
    {
        var handler = new SendEmailPoisonHandler(_store.Object, NullLogger<SendEmailPoisonHandler>.Instance);
        const string body = "{\"messageKey\":\"email:ConfirmationEmail:USER-1\",\"payload\":{\"userId\":\"USER-1\"}}";

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(body, CancellationToken.None));

        Assert.Null(ex);
        _store.Verify(
            s => s.RecordAsync(QueueNames.SendEmail, body, It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
