using System.Text.Json;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Cleansia.Tests.Infrastructure;
using Cleansia.Core.Domain.SeedWork;

namespace Cleansia.Tests.Functions;

/// <summary>
/// The admin-notification arm of the send-email consumer: the discriminator routes an enveloped body
/// to the one rendering method with the event, the args and the language the notifier chose; the
/// key it synthesises is the producer's, so a redelivery sends once; a transport fault throws so the
/// runtime retries; a malformed or hollow body acks; and the envelope's company becomes the override
/// before the send. The frozen shape beside it is untouched: a plain confirmation body still routes
/// where it did.
/// </summary>
public sealed class SendEmailHandlerAdminNotificationTests
{
    private const string Mailbox = "ops@example.com";
    private const string TenantId = "cleansia-cz";

    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly InMemoryIdempotencyGuard _guard = new();

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private SendEmailHandler CreateHandler() => new(
        _emailService.Object,
        _guard,
        _tenantProvider.Object,
        Mock.Of<IPromoCodeRepository>(),
        Mock.Of<ITenantRepository>(),
        Mock.Of<ICompanyInfoRepository>(),
        NullLogger<SendEmailHandler>.Instance,
        Mock.Of<IOrderRepository>(),
        TestGuestOrderAccessTokenIssuer.WithNoLiveTokens(),
        Mock.Of<IUnitOfWork>());

    private static SendAdminNotificationEmailMessage DisputeFiled(string subject = "dispute-1", string language = "cs") =>
        new(
            AdminNotificationEventCatalog.DisputeFiled,
            subject,
            new Dictionary<string, string>
            {
                ["orderNumber"] = "ORD-1A2B3C4D",
                ["reason"] = "QualityIssue",
                ["disputeId"] = subject,
                ["orderId"] = "order-1",
            },
            Mailbox,
            language,
            TenantId);

    private static string Envelope(SendAdminNotificationEmailMessage message, string? tenantId = TenantId) =>
        JsonSerializer.Serialize(
            new QueueEnvelope<SendAdminNotificationEmailMessage>(
                MessageKeys.AdminNotificationEmail(message.EventKey, message.Subject, message.Email), tenantId, message),
            JsonOptions);

    private void ArrangeSend() =>
        _emailService
            .Setup(s => s.SendAdminNotificationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("msg-id");

    [Fact]
    public async Task The_Discriminator_Routes_To_The_Admin_Rendering_With_The_Event_Args_And_Language()
    {
        ArrangeSend();

        await CreateHandler().HandleAsync(Envelope(DisputeFiled()), CancellationToken.None);

        _emailService.Verify(s => s.SendAdminNotificationEmailAsync(
            Mailbox,
            AdminNotificationEventCatalog.DisputeFiled,
            It.Is<IReadOnlyDictionary<string, string>>(a => a["orderNumber"] == "ORD-1A2B3C4D" && a["disputeId"] == "dispute-1" && a.Count == 4),
            "cs",
            It.IsAny<CancellationToken>()), Times.Once);
        _emailService.Verify(s => s.SendEmailConfirmationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Twice_With_The_Same_Body_Sends_Once_And_A_Different_Subject_Sends_Again()
    {
        ArrangeSend();
        var handler = CreateHandler();
        var body = Envelope(DisputeFiled());

        await handler.HandleAsync(body, CancellationToken.None);
        await handler.HandleAsync(body, CancellationToken.None);
        await handler.HandleAsync(Envelope(DisputeFiled(subject: "dispute-2")), CancellationToken.None);

        _emailService.Verify(s => s.SendAdminNotificationEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task The_Envelope_Company_Is_The_Override_Before_The_Send()
    {
        ArrangeSend();

        await CreateHandler().HandleAsync(Envelope(DisputeFiled(), tenantId: "cleansia-sk"), CancellationToken.None);

        _tenantProvider.Verify(t => t.SetTenantOverride("cleansia-sk"), Times.Once);
    }

    [Fact]
    public async Task A_Transport_Fault_Throws_So_The_Runtime_Retries_And_Leaves_The_Key_Unclaimed()
    {
        _emailService
            .SetupSequence(s => s.SendAdminNotificationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("SendGrid unreachable"))
            .ReturnsAsync("msg-id");
        var handler = CreateHandler();
        var body = Envelope(DisputeFiled());

        await Assert.ThrowsAsync<HttpRequestException>(() => handler.HandleAsync(body, CancellationToken.None));
        await handler.HandleAsync(body, CancellationToken.None);

        _emailService.Verify(s => s.SendAdminNotificationEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task A_Malformed_Body_Acks_Without_Throwing_Or_Sending()
    {
        var ex = await Record.ExceptionAsync(() => CreateHandler().HandleAsync("{\"messageType\":\"admin-notification\",", CancellationToken.None));

        Assert.Null(ex);
        _emailService.Verify(s => s.SendAdminNotificationEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("{\"messageKey\":\"k\",\"tenantId\":\"cleansia-cz\",\"payload\":{\"messageType\":\"admin-notification\"}}")]
    [InlineData("{\"messageKey\":\"k\",\"tenantId\":\"cleansia-cz\",\"payload\":{\"messageType\":\"admin-notification\",\"eventKey\":\"admin.dispute.filed\",\"subject\":\"d-1\",\"email\":\"\",\"args\":{}}}")]
    [InlineData("{\"messageKey\":\"k\",\"tenantId\":\"cleansia-cz\",\"payload\":{\"messageType\":\"admin-notification\",\"eventKey\":\"admin.dispute.filed\",\"subject\":\"\",\"email\":\"ops@example.com\",\"args\":{}}}")]
    [InlineData("{\"messageKey\":\"k\",\"tenantId\":\"cleansia-cz\",\"payload\":{\"messageType\":\"admin-notification\",\"eventKey\":\"admin.dispute.filed\",\"subject\":\"d-1\",\"email\":\"ops@example.com\"}}")]
    public async Task A_Hollow_Body_Acks_Without_Throwing_Or_Sending(string body)
    {
        var ex = await Record.ExceptionAsync(() => CreateHandler().HandleAsync(body, CancellationToken.None));

        Assert.Null(ex);
        _emailService.Verify(s => s.SendAdminNotificationEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task The_Wire_Body_Carries_The_Discriminator_And_The_Consumer_Reads_It_Back()
    {
        ArrangeSend();
        var body = Envelope(DisputeFiled());
        using var document = JsonDocument.Parse(body);
        Assert.Equal(SendAdminNotificationEmailMessage.Discriminator, document.RootElement.GetProperty("payload").GetProperty("messageType").GetString());

        await CreateHandler().HandleAsync(body, CancellationToken.None);

        _emailService.Verify(s => s.SendAdminNotificationEmailAsync(Mailbox, It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), "cs", It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class InMemoryIdempotencyGuard : IIdempotencyGuard
    {
        private readonly HashSet<string> _claimed = [];

        public Task<bool> AlreadyProcessedAsync(string messageKey, CancellationToken ct = default) =>
            Task.FromResult(!_claimed.Add(messageKey));

        public Task<bool> HasProcessedAsync(string messageKey, CancellationToken ct = default) =>
            Task.FromResult(_claimed.Contains(messageKey));

        public Task MarkProcessedAsync(string messageKey, CancellationToken ct = default)
        {
            _claimed.Add(messageKey);
            return Task.CompletedTask;
        }
    }
}
