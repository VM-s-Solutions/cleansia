using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Cleansia.Config;
using Cleansia.Infra.Clients.Apns;
using Cleansia.Infra.Clients.SendGrid;
using Cleansia.Infra.Clients.Stripe;
using Cleansia.Infra.Fiscal.Countries.Czechia;
using Cleansia.Infra.Services.BusinessRegistry;
using Cleansia.ServiceDefaults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using IStripeClient = Cleansia.Core.Clients.Abstractions.Stripe.IStripeClient;

namespace Cleansia.Tests.Integration;

/// <summary>
/// Every factory client composed the way an API host composes it: the clients registered by
/// <c>AddCoreBindings</c>, then the service defaults that give every client the standard resilience handler.
/// That default is the outermost layer, so a client bringing its own pipeline used to run inside it, and a
/// provider answering 429 was asked four times four. Each client's own pipeline is its only one.
///
/// <para>The client list is read from the composed collection rather than written down, so a client added
/// tomorrow is checked here too.</para>
/// </summary>
public class HostHttpClientPipelineTests
{
    private const string FiscalClientName = nameof(CzechEet2FiscalService);
    private const string MapboxClientName = "Mapbox";

    private static readonly string[] KnownClients =
    [
        StripeExtensions.HttpClientName,
        SendGridExtensions.HttpClientName,
        ApnsLiveActivityClient.HttpClientName,
        MapboxClientName,
        AresBusinessRegistry.HttpClientName,
        FiscalClientName,
    ];

    [Fact]
    public void Every_named_client_has_exactly_one_resilience_pipeline_under_the_host_defaults()
    {
        var services = HostComposition();
        var names = NamedClients(services);
        Assert.Superset(KnownClients.ToHashSet(StringComparer.Ordinal), names.ToHashSet(StringComparer.Ordinal));

        using var provider = services.BuildServiceProvider();
        var handlers = provider.GetRequiredService<IHttpMessageHandlerFactory>();

        var pipelines = names.ToDictionary(name => name, name => ResilienceHandlersIn(handlers.CreateHandler(name)));

        Assert.All(pipelines, client => Assert.True(client.Value == 1,
            $"'{client.Key}' runs {client.Value} resilience pipelines under the host defaults."));
    }

    public static TheoryData<string, int> RetryBudgets() => new()
    {
        { StripeExtensions.HttpClientName, 4 },
        { SendGridExtensions.HttpClientName, 4 },
        { ApnsLiveActivityClient.HttpClientName, 4 },
        { MapboxClientName, 4 },
        { FiscalClientName, 4 },
        { AresBusinessRegistry.HttpClientName, 3 },
    };

    [Theory]
    [MemberData(nameof(RetryBudgets))]
    public async Task A_client_sends_a_429_exactly_its_own_retry_budget(string name, int expectedSends)
    {
        var primary = new RecordingHandler(_ => RateLimited());
        var services = HostComposition();
        services.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(() => primary);
        await using var provider = services.BuildServiceProvider();

        try
        {
            using var response = await provider.GetRequiredService<IHttpClientFactory>()
                .CreateClient(name)
                .GetAsync("https://provider.test/ping");
        }
        catch (Exception)
        {
            // A spent budget may surface as an exception; what is asserted is how often the provider was asked.
        }

        Assert.Equal(expectedSends, primary.Requests.Count);
    }

    /// <summary>
    /// A POST that carries no key of ours is still safe to retry at the transport, because the Stripe SDK
    /// attaches one key per call and every retry re-sends that same request.
    /// </summary>
    [Fact]
    public async Task A_keyless_Stripe_POST_replays_one_idempotency_key_across_retries()
    {
        var replies = 0;
        var primary = new RecordingHandler(_ => Interlocked.Increment(ref replies) == 1
            ? RateLimited()
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"id":"pi_1","object":"payment_intent","status":"canceled"}""", Encoding.UTF8, "application/json"),
            });
        var services = HostComposition();
        services.AddHttpClient(StripeExtensions.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => primary);
        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IStripeClient>().CancelPaymentIntentAsync("pi_1", CancellationToken.None);

        Assert.Equal(2, primary.Requests.Count);
        Assert.All(primary.Requests, request => Assert.Equal(HttpMethod.Post, request.Method));
        var keys = primary.Requests.Select(request => request.IdempotencyKey).ToList();
        Assert.All(keys, key => Assert.False(string.IsNullOrEmpty(key)));
        Assert.Single(keys.Distinct());
    }

    private static HttpResponseMessage RateLimited()
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
        return response;
    }

    private static ServiceCollection HostComposition()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ConnectionString"] = "Host=127.0.0.1;Port=1;Database=probe;Username=probe;Password=probe",
                ["ConnectionStrings:QueueStorageConnectionString"] = "UseDevelopmentStorage=true",
                ["ConnectionStrings:BlobContainerConfigurationConnectionString"] = "UseDevelopmentStorage=true",
                ["JwtSettings:Secret"] = new string('k', 64),
                ["JwtSettings:Issuer"] = "cleansia",
                ["Stripe:SecretKey"] = "sk_test_dummy",
                [$"{CzechEet2Options.SectionName}:Enabled"] = "true",
            })
            .Build();
        var environment = new ProbeEnvironment();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddHttpContextAccessor();
        services.AddCoreBindings(configuration, environment);
        services.AddServiceDefaults(configuration, environment);
        return services;
    }

    private static List<string> NamedClients(IServiceCollection services) =>
        services
            .Where(descriptor => descriptor.ServiceType == typeof(IConfigureOptions<HttpClientFactoryOptions>))
            .Select(descriptor => descriptor.ImplementationInstance)
            .OfType<ConfigureNamedOptions<HttpClientFactoryOptions>>()
            .Select(options => options.Name)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static int ResilienceHandlersIn(HttpMessageHandler handler)
    {
        var count = 0;
        for (HttpMessageHandler? current = handler; current is not null; current = (current as DelegatingHandler)?.InnerHandler)
        {
            if (current is ResilienceHandler)
            {
                count++;
            }
        }

        return count;
    }

    private sealed record SentRequest(HttpMethod Method, string? IdempotencyKey);

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public ConcurrentQueue<SentRequest> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Enqueue(new SentRequest(
                request.Method,
                request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.SingleOrDefault() : null));
            return Task.FromResult(respond(request));
        }
    }

    private sealed class ProbeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Cleansia.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
