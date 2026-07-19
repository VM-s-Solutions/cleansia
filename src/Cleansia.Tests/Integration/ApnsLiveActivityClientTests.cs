using System.Net;
using System.Text;
using Cleansia.Core.Clients.Abstractions.Apns;
using Cleansia.Infra.Clients.Apns;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cleansia.Tests.Integration;

/// <summary>
/// ADR-0029 D1 — the direct-APNs failure taxonomy + INERT behavior of <see cref="ApnsLiveActivityClient"/>.
/// (TC-LA-3, client half.) Disabled/keyless → Skipped without a socket; 410/BadDeviceToken → prune;
/// 403 → re-mint once then transient; 429/5xx → throw; topic/host derivation; JWT never in an error.
/// </summary>
public class ApnsLiveActivityClientTests
{
    private static LiveActivityPush SamplePush() => new(
        Event: "update",
        ContentState: new LiveActivityContentState(1, "inProgress", "ORD-1",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(2)),
        Timestamp: DateTimeOffset.UtcNow,
        StaleDate: DateTimeOffset.UtcNow.AddHours(4),
        DismissalDate: null,
        AttributesType: null,
        Attributes: null);

    private static FakeApnsConfig EnabledConfig() => new()
    {
        Enabled = true,
        KeyId = "ABC1234567",
        TeamId = "TEAM123456",
        PrivateKeyPem = "-----BEGIN PRIVATE KEY-----\nX\n-----END PRIVATE KEY-----",
        CustomerBundleId = "cz.cleansia.customer",
        UseSandbox = true,
    };

    private static ApnsLiveActivityClient CreateClient(FakeApnsConfig config, StubHandler handler, FakeJwtProvider? jwt = null)
    {
        var factory = new StubHttpClientFactory(handler);
        return new ApnsLiveActivityClient(config, jwt ?? new FakeJwtProvider(), factory,
            NullLogger<ApnsLiveActivityClient>.Instance);
    }

    [Fact]
    public async Task Disabled_Reports_Skipped_And_Never_Opens_A_Socket()
    {
        var config = EnabledConfig();
        config.Enabled = false;
        var handler = new StubHandler();
        var client = CreateClient(config, handler);

        var result = await client.SendAsync("TOKEN", SamplePush(), CancellationToken.None);

        Assert.True(result.Skipped);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Enabled_But_Keyless_Reports_Skipped_Without_A_Socket()
    {
        var config = EnabledConfig();
        config.PrivateKeyPem = string.Empty;
        var handler = new StubHandler();
        var client = CreateClient(config, handler);

        var result = await client.SendAsync("TOKEN", SamplePush(), CancellationToken.None);

        Assert.True(result.Skipped);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Enabled_But_Key_Is_An_Unresolved_KeyVault_Reference_Reports_Skipped_And_Warns_Without_A_Socket()
    {
        // App Service hands the app the LITERAL "@Microsoft.KeyVault(SecretUri=…)" string (non-empty, so it
        // clears the keyless guard) when apnsSecretProvisioned flips true before the KV secret is seeded.
        // That value would fail Convert.FromBase64String inside GetToken() → FormatException → the consumer
        // classifies it transient → retries → poison-storm. It must degrade to Skipped instead.
        var config = EnabledConfig();
        config.PrivateKeyPem = "@Microsoft.KeyVault(SecretUri=https://cleansia-kv.vault.azure.net/secrets/Apns--PrivateKeyPem/)";
        var handler = new StubHandler();
        var logger = new CapturingLogger();
        // A REAL ApnsJwtProvider so the unparseable-key path runs against the actual Convert.FromBase64String.
        var client = new ApnsLiveActivityClient(
            config, new ApnsJwtProvider(config, TimeProvider.System),
            new StubHttpClientFactory(handler), logger);

        var result = await client.SendAsync("TOKEN", SamplePush(), CancellationToken.None);

        Assert.True(result.Skipped);
        Assert.Equal(0, handler.CallCount);
        Assert.Single(logger.Warnings);
        Assert.DoesNotContain("KeyVault", logger.Warnings[0]); // S6: the reference / key material is never echoed
    }

    [Fact]
    public async Task Success_Reports_Delivered_With_The_Right_Topic_Host_And_PushType()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(EnabledConfig(), handler);

        var result = await client.SendAsync("TOKEN-9", SamplePush(), CancellationToken.None);

        Assert.True(result.Delivered);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.sandbox.push.apple.com/3/device/TOKEN-9", request.Uri);
        Assert.Equal("liveactivity", request.PushType);
        Assert.Equal("cz.cleansia.customer.push-type.liveactivity", request.Topic);
        Assert.Equal("bearer JWT-STUB", request.Authorization);
    }

    [Fact]
    public async Task Production_Host_When_Not_Sandbox()
    {
        var config = EnabledConfig();
        config.UseSandbox = false;
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(config, handler);

        await client.SendAsync("T", SamplePush(), CancellationToken.None);

        Assert.StartsWith("https://api.push.apple.com/3/device/", handler.Requests[0].Uri);
    }

    [Fact]
    public async Task Status_410_Prunes_The_Token()
    {
        var handler = new StubHandler(_ => Reason(HttpStatusCode.Gone, "Unregistered"));
        var client = CreateClient(EnabledConfig(), handler);

        var result = await client.SendAsync("DEAD", SamplePush(), CancellationToken.None);

        Assert.True(result.TokenInvalid);
    }

    [Fact]
    public async Task Status_400_BadDeviceToken_Prunes_The_Token()
    {
        var handler = new StubHandler(_ => Reason(HttpStatusCode.BadRequest, "BadDeviceToken"));
        var client = CreateClient(EnabledConfig(), handler);

        var result = await client.SendAsync("DEAD", SamplePush(), CancellationToken.None);

        Assert.True(result.TokenInvalid);
    }

    [Fact]
    public async Task Status_400_BadTopic_Is_Permanent_But_Not_A_Prune()
    {
        var handler = new StubHandler(_ => Reason(HttpStatusCode.BadRequest, "BadTopic"));
        var client = CreateClient(EnabledConfig(), handler);

        var result = await client.SendAsync("T", SamplePush(), CancellationToken.None);

        Assert.False(result.TokenInvalid);
        Assert.False(result.Delivered);
        Assert.False(result.Skipped);
    }

    [Fact]
    public async Task Status_403_Remints_Once_Then_Succeeds()
    {
        var responses = new Queue<HttpResponseMessage>([
            Reason(HttpStatusCode.Forbidden, "ExpiredProviderToken"),
            new HttpResponseMessage(HttpStatusCode.OK),
        ]);
        var handler = new StubHandler(_ => responses.Dequeue());
        var jwt = new FakeJwtProvider();
        var client = CreateClient(EnabledConfig(), handler, jwt);

        var result = await client.SendAsync("T", SamplePush(), CancellationToken.None);

        Assert.True(result.Delivered);
        Assert.Equal(1, jwt.InvalidateCount);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task Status_403_Twice_Remints_Once_Then_Throws()
    {
        var handler = new StubHandler(_ => Reason(HttpStatusCode.Forbidden, "InvalidProviderToken"));
        var jwt = new FakeJwtProvider();
        var client = CreateClient(EnabledConfig(), handler, jwt);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.SendAsync("T", SamplePush(), CancellationToken.None));

        Assert.Equal(1, jwt.InvalidateCount);
        Assert.Equal(2, handler.CallCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Transient_Status_Throws_So_The_Queue_Retries(HttpStatusCode status)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(status));
        var client = CreateClient(EnabledConfig(), handler);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.SendAsync("T", SamplePush(), CancellationToken.None));
    }

    [Fact]
    public async Task Transient_Throw_Message_Never_Leaks_The_Jwt()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = CreateClient(EnabledConfig(), handler);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.SendAsync("T", SamplePush(), CancellationToken.None));

        Assert.DoesNotContain("JWT-STUB", ex.Message);
    }

    private static HttpResponseMessage Reason(HttpStatusCode status, string reason) =>
        new(status) { Content = new StringContent($"{{\"reason\":\"{reason}\"}}", Encoding.UTF8, "application/json") };

    private sealed record CapturedRequest(string Uri, string? PushType, string? Topic, string? Authorization);

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage>? responder = null) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder =
            responder ?? (_ => new HttpResponseMessage(HttpStatusCode.OK));

        public int CallCount { get; private set; }
        public List<CapturedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            Requests.Add(new CapturedRequest(
                request.RequestUri!.ToString(),
                Header(request, "apns-push-type"),
                Header(request, "apns-topic"),
                Header(request, "authorization")));
            return Task.FromResult(_responder(request));
        }

        private static string? Header(HttpRequestMessage request, string name) =>
            request.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class FakeJwtProvider : IApnsJwtProvider
    {
        public int InvalidateCount { get; private set; }
        public string GetToken() => "JWT-STUB";
        public void Invalidate() => InvalidateCount++;
        public bool HasUsableKey() => true;
    }

    private sealed class CapturingLogger : ILogger<ApnsLiveActivityClient>
    {
        public List<string> Warnings { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) Warnings.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private sealed class FakeApnsConfig : IApnsConfig
    {
        public bool Enabled { get; set; }
        public string KeyId { get; set; } = string.Empty;
        public string TeamId { get; set; } = string.Empty;
        public string PrivateKeyPem { get; set; } = string.Empty;
        public string CustomerBundleId { get; set; } = string.Empty;
        public bool UseSandbox { get; set; }
    }
}
