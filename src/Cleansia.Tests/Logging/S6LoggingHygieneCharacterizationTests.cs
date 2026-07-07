using System.Net;
using System.Text.Json;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions;
using Cleansia.Core.Clients.Abstractions.Fcm;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Fiscal.Abstractions;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Exceptions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cleansia.Tests.Logging;

/// <summary>
/// S6 logging-hygiene characterization (see <c>agents/knowledge/security-rules.md</c> §S6). These pin
/// the CURRENT log EVENT — the level and the safe scalar correlation keys (UserId / OrderId / EventKey /
/// CampaignId / StatusCode) — at the call sites that previously dumped a raw queue body, a SendGrid
/// response body, or a confirmation code/email. They assert the *event and its scalar properties*, NOT
/// the message-template string (string-coupled log assertions are a rejected anti-pattern), and that no
/// PII/secret VALUE rides along in the rendered message above Debug.
/// </summary>
// The EmailService test drives a real BadRequest send, which records ("SendGrid", Permanent) on the
// process-global IntegrationFailureMetrics meter — a real-provider emission must never overlap the
// boundary listeners serialized in this collection.
[Collection("IntegrationFailureMeter")]
public class S6LoggingHygieneCharacterizationTests
{
    // ── Functions: SendPushNotificationHandler raw messageText (AC2) ────────────────────────────────

    [Fact]
    public async Task PushHandler_TransientFailure_Logs_Error_With_UserId_EventKey_And_No_RawBody()
    {
        var logger = new CapturingLogger<SendPushNotificationHandler>();
        var devices = new Mock<IDeviceRepository>();
        var preferences = new Mock<IUserNotificationPreferencesRepository>();
        var dispatcher = new Mock<IPushDispatcher>();

        preferences
            .Setup(r => r.GetByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cleansia.Core.Domain.Notifications.UserNotificationPreferences?)null);
        // Transient infra fault on the device read drives the catch (Error) branch.
        devices
            .Setup(r => r.GetByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("transient DB connection failure"));

        var handler = new SendPushNotificationHandler(
            devices.Object, preferences.Object, dispatcher.Object,
            Mock.Of<IUnitOfWork>(), new NoopGuard(), Mock.Of<ITenantProvider>(), logger);

        var body = JsonSerializer.Serialize(
            new SendPushNotificationMessage("USER-1", "order.confirmed", new(), TenantId: null),
            CamelCase);

        await Assert.ThrowsAsync<TimeoutException>(() => handler.HandleAsync(body, CancellationToken.None));

        var entry = logger.Single(LogLevel.Error);
        Assert.Equal("USER-1", entry.Scalar("UserId"));
        Assert.Equal("order.confirmed", entry.Scalar("EventKey"));
        Assert.DoesNotContain(body, entry.Message);
    }

    [Fact]
    public async Task PushHandler_MalformedBody_Logs_Warning_Without_RawBody()
    {
        var logger = new CapturingLogger<SendPushNotificationHandler>();
        var handler = new SendPushNotificationHandler(
            Mock.Of<IDeviceRepository>(), Mock.Of<IUserNotificationPreferencesRepository>(),
            Mock.Of<IPushDispatcher>(), Mock.Of<IUnitOfWork>(), new NoopGuard(),
            Mock.Of<ITenantProvider>(), logger);

        const string malformed = "}{ not json with secret-token-abc";

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(malformed, CancellationToken.None));

        Assert.Null(ex);
        var entry = logger.Single(LogLevel.Warning);
        Assert.DoesNotContain(malformed, entry.Message);
        Assert.DoesNotContain("secret-token-abc", entry.Message);
    }

    [Fact]
    public async Task PushHandler_MissingFields_Logs_Warning_Without_RawBody()
    {
        var logger = new CapturingLogger<SendPushNotificationHandler>();
        var handler = new SendPushNotificationHandler(
            Mock.Of<IDeviceRepository>(), Mock.Of<IUserNotificationPreferencesRepository>(),
            Mock.Of<IPushDispatcher>(), Mock.Of<IUnitOfWork>(), new NoopGuard(),
            Mock.Of<ITenantProvider>(), logger);

        // Valid JSON, missing UserId/EventKey — drives the missing-field discard guard.
        const string body = """{"args":{"address":"42 Secret St"}}""";

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(body, CancellationToken.None));

        Assert.Null(ex);
        var entry = logger.Single(LogLevel.Warning);
        Assert.DoesNotContain("42 Secret St", entry.Message);
    }

    // ── Functions: SendSitewidePromoFanoutHandler raw messageText (AC2) ─────────────────────────────

    [Fact]
    public async Task PromoFanout_DiscardGuard_Logs_Warning_With_CampaignId_And_No_RawBody()
    {
        var logger = new CapturingLogger<SendSitewidePromoFanoutHandler>();
        var handler = new SendSitewidePromoFanoutHandler(
            Mock.Of<IUserNotificationPreferencesRepository>(), Mock.Of<IUserRepository>(),
            Mock.Of<IQueueClient>(), Mock.Of<ICampaignProgressStore>(), Mock.Of<ITenantProvider>(),
            logger);

        // No "en" fallback → the discard guard fires after a successful deserialize.
        var body = JsonSerializer.Serialize(
            new SendSitewidePromoMessage(
                TitleByLocale: new Dictionary<string, string> { ["cs"] = "Tajná akce" },
                BodyByLocale: new Dictionary<string, string> { ["cs"] = "Tajné tělo" },
                TenantId: null,
                CampaignId: "promo::camp-1"),
            CamelCase);

        await handler.HandleAsync(body, CancellationToken.None);

        var entry = logger.Single(LogLevel.Warning);
        Assert.Equal("promo::camp-1", entry.Scalar("CampaignId"));
        Assert.DoesNotContain(body, entry.Message);
        Assert.DoesNotContain("Tajná akce", entry.Message);
    }

    [Fact]
    public async Task PromoFanout_TransientFailure_Logs_Error_Without_RawBody()
    {
        var logger = new CapturingLogger<SendSitewidePromoFanoutHandler>();
        var preferences = new Mock<IUserNotificationPreferencesRepository>();
        // Throw when paging starts → drives the outer Error catch.
        preferences
            .Setup(r => r.GetQueryableIgnoringTenant())
            .Throws(new InvalidOperationException("transient store fault"));
        var progress = new Mock<ICampaignProgressStore>();
        progress
            .Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CampaignProgress(LastProcessedUserId: null, IsComplete: false));

        var handler = new SendSitewidePromoFanoutHandler(
            preferences.Object, Mock.Of<IUserRepository>(), Mock.Of<IQueueClient>(),
            progress.Object, Mock.Of<ITenantProvider>(), logger);

        var body = JsonSerializer.Serialize(
            new SendSitewidePromoMessage(
                TitleByLocale: new Dictionary<string, string> { ["en"] = "Hi" },
                BodyByLocale: new Dictionary<string, string> { ["en"] = "Body secret-xyz" },
                TenantId: null,
                CampaignId: "promo::camp-2"),
            CamelCase);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(body, CancellationToken.None));

        var entry = logger.Single(LogLevel.Error);
        Assert.DoesNotContain(body, entry.Message);
        Assert.DoesNotContain("secret-xyz", entry.Message);
    }

    [Fact]
    public async Task PromoFanout_NullDeserialize_Throws_Without_RawBody_In_ExceptionMessage()
    {
        var logger = new CapturingLogger<SendSitewidePromoFanoutHandler>();
        var handler = new SendSitewidePromoFanoutHandler(
            Mock.Of<IUserNotificationPreferencesRepository>(), Mock.Of<IUserRepository>(),
            Mock.Of<IQueueClient>(), Mock.Of<ICampaignProgressStore>(), Mock.Of<ITenantProvider>(),
            logger);

        // JSON null literal → Deserialize returns null → the `?? throw` fires. Its message is logged at
        // Error in the outer catch, so it must not embed the raw body.
        const string body = "null";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(body, CancellationToken.None));

        Assert.DoesNotContain(body, ex.Message);
    }

    // ── Functions: GenerateReceiptHandler raw messageText (AC2) ─────────────────────────────────────

    [Fact]
    public async Task ReceiptHandler_OrderNotFound_Logs_Error_With_OrderId_And_No_RawBody()
    {
        const string orderId = "01HZX9N6M7Q8R9S0T1V2W3X4Y5";
        var logger = new CapturingLogger<GenerateReceiptHandler>();
        var orders = new Mock<IOrderRepository>();
        // Order missing → throws inside the try → outer Error catch logs.
        orders
            .Setup(r => r.GetByIdIgnoringTenantAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IDbContextTransaction>());

        var handler = new GenerateReceiptHandler(
            orders.Object, Mock.Of<IReceiptService>(), Mock.Of<IEmailService>(),
            Mock.Of<ICountryConfigurationRepository>(), uow.Object, Mock.Of<ITenantProvider>(), logger);

        var body = JsonSerializer.Serialize(
            new QueueEnvelope<GenerateReceiptMessage>(
                MessageKeys.Receipt(orderId), null, new GenerateReceiptMessage(orderId, "en")),
            CamelCase);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(body, CancellationToken.None));

        var entry = logger.Single(LogLevel.Error);
        Assert.Equal(orderId, entry.Scalar("OrderId"));
        Assert.DoesNotContain(body, entry.Message);
    }

    [Fact]
    public async Task ReceiptHandler_MalformedBody_Throws_Without_RawBody_In_ExceptionMessage()
    {
        var logger = new CapturingLogger<GenerateReceiptHandler>();
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IDbContextTransaction>());

        var handler = new GenerateReceiptHandler(
            Mock.Of<IOrderRepository>(), Mock.Of<IReceiptService>(), Mock.Of<IEmailService>(),
            Mock.Of<ICountryConfigurationRepository>(), uow.Object, Mock.Of<ITenantProvider>(), logger);

        // Malformed body → both envelope and bare reads throw → ReadPayload returns null → the
        // deserialize-failure throw fires. The exception message (logged at Error in the outer catch)
        // must not embed the raw body.
        const string body = "}{ not json secret-receipt-xyz";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(body, CancellationToken.None));

        Assert.DoesNotContain(body, ex.Message);
        Assert.DoesNotContain("secret-receipt-xyz", ex.Message);
    }

    // ── EmailService: SendGrid response body (AC4) ──────────────────────────────────────────────────

    [Fact]
    public async Task EmailService_NonSuccessResponse_Logs_StatusCode_Not_Body_Above_Debug()
    {
        const string responseBody = """{"errors":[{"message":"recipient leaked@example.com rejected"}]}""";
        var logger = new CapturingLogger<EmailService>();
        var service = BuildEmailService(HttpStatusCode.BadRequest, responseBody, logger);

        await Assert.ThrowsAsync<EmailDeliveryException>(
            () => service.SendEmailConfirmationAsync(
                "user@example.com", "John", "code-1", "en", CancellationToken.None));

        var error = logger.Single(LogLevel.Error);
        Assert.Equal(400, error.Scalar("StatusCode"));
        // The response body (which can echo recipient addresses) must not appear above Debug.
        foreach (var entry in logger.Entries.Where(e => e.Level >= LogLevel.Information))
        {
            Assert.DoesNotContain(responseBody, entry.Message);
            Assert.DoesNotContain("leaked@example.com", entry.Message);
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions CamelCase =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static EmailService BuildEmailService(
        HttpStatusCode status, string responseBody, ILogger<EmailService> logger)
    {
        var config = new Mock<ISendGridConfig>();
        config.SetupGet(c => c.ApiKey).Returns("SG.test");
        config.SetupGet(c => c.AddressFrom).Returns("noreply@example.test");
        config.SetupGet(c => c.EmailConfirmationTemplateId).Returns("d-template-1");
        config.SetupGet(c => c.ClientDomainUrl).Returns("https://app.test");

        var translations = new Mock<IEmailTemplateTranslationRepository>();
        translations
            .Setup(r => r.GetTranslationsByTypeAndLanguageAsync(
                It.IsAny<EmailType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["Subject"] = "Confirm Your Email" });

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(new StubHandler(status, responseBody), disposeHandler: false));

        return new EmailService(config.Object, logger, httpClientFactory.Object, translations.Object);
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }

    private sealed class NoopGuard : IIdempotencyGuard
    {
        public Task<bool> AlreadyProcessedAsync(string messageKey, CancellationToken ct = default) =>
            Task.FromResult(false);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var scalars = new Dictionary<string, object?>();
            if (state is IEnumerable<KeyValuePair<string, object?>> kvps)
            {
                foreach (var kvp in kvps)
                {
                    scalars[kvp.Key] = kvp.Value;
                }
            }

            Entries.Add(new LogEntry(logLevel, formatter(state, exception), scalars));
        }

        public LogEntry Single(LogLevel level)
        {
            var matches = Entries.Where(e => e.Level == level).ToList();
            Assert.Single(matches);
            return matches[0];
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message, IReadOnlyDictionary<string, object?> Scalars)
    {
        public object? Scalar(string key) => Scalars.TryGetValue(key, out var value) ? value : null;
    }
}
