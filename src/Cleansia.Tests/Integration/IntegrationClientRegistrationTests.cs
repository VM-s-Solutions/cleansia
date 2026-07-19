using Cleansia.Infra.Clients.SendGrid;
using Cleansia.Infra.Clients.Stripe;
using Cleansia.Infra.Common.Configuration;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cleansia.Tests.Integration;

/// <summary>
/// ADR-0005 D1 — Stripe and SendGrid must be routed through
/// <see cref="IHttpClientFactory"/> so they inherit the standard resilience handler
/// (and, in a real host, OTel <c>AddHttpClientInstrumentation</c>) instead of newing a
/// fresh SDK socket per call. ADR-0005 verification #2 ("pooled handler present"):
/// each provider's named registration has the standard resilience handler attached.
///
/// These are the AC5 DI/registration tests, written test-first (RED until
/// <see cref="StripeExtensions.AddStripe"/> / <see cref="SendGridExtensions.AddSendGrid"/>
/// register the named clients). They mirror the Fiscal client registration shape
/// (<c>FiscalServiceCollectionExtensions.cs:54-55</c>) and the named "Mapbox" client.
/// </summary>
public class IntegrationClientRegistrationTests
{
    private static ServiceProvider BuildProvider()
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

        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData("Stripe")]
    [InlineData("SendGrid")]
    public void Named_Client_Is_Registered_And_Resolvable_From_The_Factory(string clientName)
    {
        using var provider = BuildProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        // A client newed by hand (the old StripeClient/SendGridClient pattern) is NOT
        // factory-managed; if the named registration is missing the factory still hands back
        // a default client, so the presence test is the resilience-handler check below.
        var client = factory.CreateClient(clientName);

        Assert.NotNull(client);
    }

    [Theory]
    [InlineData("Stripe")]
    [InlineData("SendGrid")]
    public void Named_Client_Has_The_Standard_Resilience_Handler_Attached(string clientName)
    {
        using var provider = BuildProvider();
        var handlerFactory = provider.GetRequiredService<IHttpMessageHandlerFactory>();

        using var handler = handlerFactory.CreateHandler(clientName);

        Assert.True(
            HasResilienceHandler(handler),
            $"Named client '{clientName}' must have the standard resilience handler in its " +
            "message-handler pipeline (ADR-0005 D1.2). Mirror FiscalServiceCollectionExtensions.cs:54-55 " +
            "(.AddStandardResilienceHandler()).");
    }

    // Walk the DelegatingHandler chain looking for the resilience handler the
    // AddStandardResilienceHandler() registration inserts. Matching on the type name keeps the
    // assertion robust to the internal type's exact namespace across resilience-package versions.
    private static bool HasResilienceHandler(HttpMessageHandler? handler)
    {
        var current = handler;
        while (current is not null)
        {
            if (current.GetType().Name.Contains("Resilience", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current is DelegatingHandler delegating ? delegating.InnerHandler : null;
        }

        return false;
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
