using System.Text.Json;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

public class SendSitewidePromoFanoutHandlerSmokeTests
{
    private readonly Mock<IUserNotificationPreferencesRepository> _preferencesRepository = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IQueueClient> _queueClient = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();

    private SendSitewidePromoFanoutHandler CreateHandler() => new(
        _preferencesRepository.Object,
        _userRepository.Object,
        _queueClient.Object,
        _tenantProvider.Object,
        NullLogger<SendSitewidePromoFanoutHandler>.Instance);

    [Fact]
    public async Task HandleAsync_Is_Reachable_And_Injectable_From_Tests()
    {
        var handler = CreateHandler();

        // A campaign with no en title/body fallback is discarded on the guard before any
        // paging or queue send, so this drives the body once without a DB or queue dependency.
        var messageText = JsonSerializer.Serialize(
            new SendSitewidePromoMessage(
                TitleByLocale: new Dictionary<string, string>(),
                BodyByLocale: new Dictionary<string, string>(),
                TenantId: null),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        await handler.HandleAsync(messageText, CancellationToken.None);

        _queueClient.Verify(
            q => q.SendAsync(It.IsAny<string>(), It.IsAny<SendPushNotificationMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
