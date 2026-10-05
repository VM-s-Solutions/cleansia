using System.Collections.Concurrent;
using System.Text;
using Cleansia.ServiceDefaults;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Sentry;
using Sentry.AspNetCore;
using Sentry.Extensibility;
using Sentry.Protocol.Envelopes;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// Sentry.AspNetCore copies the inbound query string and every request header but the cookie onto the scope,
/// and both ride on every event and transaction a request raises: a guest token, a typed address, the client
/// IP behind the platform's front end, the CSRF token, the device id. The request a Sentry item keeps is its
/// path-only URL and four content-negotiation headers.
///
/// <para>Captured through a <see cref="SentryClient"/> built from the hosts' own options over a recording
/// transport, so the process-wide SDK is never started and what is asserted is the serialized envelope.</para>
/// </summary>
public class SentryRequestQueryScrubTests
{
    private const string SampleDsn = "https://0123456789abcdef0123456789abcdef@o0.ingest.example.invalid/1";
    private const string FailureMessage = "probe-failure-message";
    private const string KeptUserAgent = "probe-agent/1.0";

    private static readonly string[] Secrets =
        ["gst-SECRET-123", "Vinohradska", "203.0.113.7", "csrf-SECRET", "device-SECRET"];

    [Fact]
    public async Task An_error_event_carries_no_query_string_and_only_the_allowed_headers()
    {
        var envelope = await CaptureAsync((client, scope) =>
            client.CaptureEvent(new SentryEvent(new InvalidOperationException(FailureMessage)), scope));

        Assert.Contains("\"type\":\"event\"", envelope, StringComparison.Ordinal);
        Assert.Contains(FailureMessage, envelope, StringComparison.Ordinal);
        AssertScrubbed(envelope);
    }

    [Fact]
    public async Task A_transaction_carries_no_query_string_and_only_the_allowed_headers()
    {
        var envelope = await CaptureAsync((client, scope) =>
        {
            var tracer = new TransactionTracer(
                Mock.Of<IHub>(),
                new TransactionContext("GET /api/Order/Lookup", "http.server", isSampled: true));
            tracer.Finish();
            client.CaptureTransaction(new SentryTransaction(tracer), scope, null);
        });

        Assert.Contains("\"type\":\"transaction\"", envelope, StringComparison.Ordinal);
        AssertScrubbed(envelope);
    }

    private static void AssertScrubbed(string envelope)
    {
        Assert.Contains(KeptUserAgent, envelope, StringComparison.Ordinal);
        foreach (var secret in Secrets)
        {
            Assert.DoesNotContain(secret, envelope, StringComparison.Ordinal);
        }
    }

    private static async Task<string> CaptureAsync(Action<SentryClient, Scope> capture)
    {
        var options = HostSentryOptions();
        var transport = new RecordingTransport();
        options.Transport = transport;

        var scope = new Scope(options);
        scope.Request.Url = "https://api.example.test/api/Order/Lookup";
        scope.Request.QueryString = "?token=gst-SECRET-123&q=Vinohradska";
        scope.Request.Headers["X-Original-URL"] = "/api/Order/Lookup?token=gst-SECRET-123";
        scope.Request.Headers["X-WAWS-Unencoded-URL"] = "/api/AddressSearch/search?q=Vinohradska 12";
        scope.Request.Headers["X-Client-IP"] = "203.0.113.7";
        scope.Request.Headers["X-Forwarded-For"] = "203.0.113.7:51000";
        scope.Request.Headers["X-CSRF-Token"] = "csrf-SECRET";
        scope.Request.Headers["X-Device-Id"] = "device-SECRET";
        scope.Request.Headers["User-Agent"] = KeptUserAgent;

        using (var client = new SentryClient(options))
        {
            capture(client, scope);
            await client.FlushAsync(TimeSpan.FromSeconds(10));
        }

        Assert.NotEmpty(transport.Envelopes);
        return string.Join('\n', transport.Envelopes);
    }

    private static SentryAspNetCoreOptions HostSentryOptions()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["Sentry:Dsn"] = SampleDsn;
        builder.WebHost.UseSentryMonitoring();

        using var provider = builder.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<SentryAspNetCoreOptions>>().Value;
        Assert.Equal(SampleDsn, options.Dsn);
        return options;
    }

    private sealed class RecordingTransport : ITransport
    {
        public ConcurrentQueue<string> Envelopes { get; } = new();

        public async Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken = default)
        {
            using var stream = new MemoryStream();
            await envelope.SerializeAsync(stream, null, cancellationToken);
            Envelopes.Enqueue(Encoding.UTF8.GetString(stream.ToArray()));
        }
    }
}
