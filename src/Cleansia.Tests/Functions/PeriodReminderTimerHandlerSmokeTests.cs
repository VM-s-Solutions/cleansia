using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Functions.Core.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

public class PeriodReminderTimerHandlerSmokeTests
{
    private readonly Mock<IPeriodReminderBackgroundService> _reminderService = new();

    private PeriodReminderTimerHandler CreateHandler() => new(
        _reminderService.Object,
        NullLogger<PeriodReminderTimerHandler>.Instance);

    [Fact]
    public async Task HandleAsync_Is_Reachable_And_Sends_Reminders_Once()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(CancellationToken.None);

        _reminderService.Verify(
            s => s.SendPeriodEndRemindersAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
