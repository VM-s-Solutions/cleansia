using System.Net;
using Cleansia.Infra.Clients.SendGrid;
using Cleansia.Infra.Clients.Stripe;
using Cleansia.Infra.Common.Configuration;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cleansia.Tests.Integration;

/// <summary>
/// ADR-0005 D1.2 + D2 boundary behavior, end-to-end through the named client's
/// resilience pipeline (the same <c>.AddStandardResilienceHandler()</c> the production
/// registration attaches). The spy primary handler counts outbound attempts so we can prove:
///   • a simulated TRANSIENT failure (HTTP 503) is RETRIED (&gt; 1 attempt) — Transient class;
///   • a 401/403 (AuthConfig class) is NOT retried (exactly 1 attempt).
///
/// AC5 boundary test, test-first (RED until <see cref="StripeExtensions.AddStripe"/> /
/// <see cref="SendGridExtensions.AddSendGrid"/> register the named clients with the standard
/// resilience handler). This is the transport-layer guarantee ADR-0005 folds the hand-rolled
/// SendGrid Polly (<c>EmailService.cs:32-44</c>) into.
/// </summary>
public class IntegrationClientRetryBehaviorTests
{
    [Theory]
    [InlineData("Stripe")]
    [InlineData("SendGrid")]
    public async Task Transient_503_Is_Retried_By_The_Standard_Handler(string clientName)
    {
        var spy = new AttemptCountingHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var provider = BuildProvider(clientName, spy);
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(clientName);

        await SafeSendAsync(client);

        Assert.True(spy.Attempts > 1,
            $"A 503 (Transient) must be retried by the standard resilience handler on '{clientName}'. " +
            $"Observed {spy.Attempts} attempt(s).");
    }

    [Theory]
    [InlineData("Stripe", HttpStatusCode.Unauthorized)]
    [InlineData("Stripe", HttpStatusCode.Forbidden)]
    [InlineData("SendGrid", HttpStatusCode.Unauthorized)]
    [InlineData("SendGrid", HttpStatusCode.Forbidden)]
    public async Task Auth_401_403_Is_Not_Retried(string clientName, HttpStatusCode status)
    {
        var spy = new AttemptCountingHandler(_ => new HttpResponseMessage(status));
        using var provider = BuildProvider(clientName, spy);
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(clientName);

        await SafeSendAsync(client);

        Assert.Equal(1, spy.Attempts);
    }

    private static async Task SafeSendAsync(HttpClient client)
    {
        try
        {
            using var response = await client.GetAsync("https://provider.test/ping");
        }
        catch
        {
            // The resilience handler may surface a final failure (e.g. after exhausting the
            // transient budget) as an exception; the assertion is on the attempt count, not the throw.
        }
    }

    private static ServiceProvider BuildProvider(string clientName, HttpMessageHandler primaryHandler)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Stripe:SecretKey"] = "sk_test_dummy",
                ["Stripe:PublishableKey"] = "pk_test_dummy",
                ["Stripe:WebhookSecret"] = "whsec_dummy",
                ["Stripe:SuccessUrlBase"] = "https://example.test/success",
                ["Stripe:CancelUrlBase"] = "https://example.test/cancel",
                ["SendGrid:ApiKey"] = "SG.dummy",
                ["SendGrid:AddressFrom"] = "noreply@example.test",
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IStripeConfig>(new StripeConfig(configuration));
        services.AddSingleton<ISendGridConfig>(new SendGridConfig(configuration));

        var env = new HostingEnvironmentStub();
        services.AddStripe(configuration, env);
        services.AddSendGrid();

        // Swap the primary (socket) handler for the spy so no real network call is made and we can
        // count attempts. This leaves the resilience handler the production registration attached in
        // place — exactly what we want to exercise.
        services.AddHttpClient(clientName).ConfigurePrimaryHttpMessageHandler(() => primaryHandler);

        return services.BuildServiceProvider();
    }

    private sealed class AttemptCountingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        private int _attempts;

        public int Attempts => _attempts;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _attempts);
            return Task.FromResult(responder(request));
        }
    }

    private sealed class HostingEnvironmentStub : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "Cleansia.Tests";
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
