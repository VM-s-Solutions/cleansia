using System.Collections.Concurrent;
using Cleansia.Functions.Telemetry;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.WorkerService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// The Functions worker is the one process that sends to APNs, and it records its outbound calls through the
/// classic Application Insights dependency collector, not the OpenTelemetry span the API hosts redact. An APNs
/// URL ends in the device's push token.
///
/// <para>The request goes through a real <see cref="SocketsHttpHandler"/>: a stub handler raises no diagnostic
/// event, so nothing would be recorded and the assertion would pass on nothing.</para>
/// </summary>
public class FunctionsWorkerDependencyTelemetryTests
{
    private const string PushToken = "80f1c2a9d4e7b3f05c6a8e1d2b4f7a9c";
    private const string ApnsHost = "api.sandbox.push.apple.com";

    private const string ConnectionString =
        "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=http://127.0.0.1:9/;LiveEndpoint=http://127.0.0.1:9/";

    [Fact]
    public async Task An_Outbound_Dependency_Names_The_Host_And_Never_The_Path()
    {
        var channel = new CapturingChannel();
        await using var provider = WorkerComposition(channel);
        var client = provider.GetRequiredService<TelemetryClient>();

        using (var offline = new HttpClient(new SocketsHttpHandler
               {
                   ConnectCallback = (_, _) => throw new HttpRequestException("offline"),
               }))
        {
            await Assert.ThrowsAsync<HttpRequestException>(() =>
                offline.PostAsync($"https://{ApnsHost}/3/device/{PushToken}", new StringContent("{}")));
        }

        client.Flush();

        var apns = Assert.Single(
            channel.Items.OfType<DependencyTelemetry>(),
            dependency => dependency.Target?.StartsWith(ApnsHost, StringComparison.Ordinal) == true);

        Assert.Equal($"https://{ApnsHost}", apns.Data);
        Assert.Equal("POST /", apns.Name);
        Assert.DoesNotContain(
            new[] { apns.Name, apns.Data, apns.Target, apns.Context.Operation.Name }.Concat(apns.Properties.Values),
            value => value?.Contains(PushToken, StringComparison.Ordinal) == true);
    }

    private static ServiceProvider WorkerComposition(ITelemetryChannel channel)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = ConnectionString,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(channel);
        services.AddWorkerApplicationInsights();
        services.Configure<ApplicationInsightsServiceOptions>(options =>
        {
            options.ConnectionString = ConnectionString;
            options.EnableAdaptiveSampling = false;
            options.EnableQuickPulseMetricStream = false;
        });
        return services.BuildServiceProvider();
    }

    private sealed class CapturingChannel : ITelemetryChannel
    {
        public ConcurrentQueue<ITelemetry> Items { get; } = new();

        public bool? DeveloperMode { get; set; }

        public string EndpointAddress { get; set; } = string.Empty;

        public void Send(ITelemetry item) => Items.Enqueue(item);

        public void Flush()
        {
        }

        public void Dispose()
        {
        }
    }
}
