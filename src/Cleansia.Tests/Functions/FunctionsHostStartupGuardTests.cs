using Cleansia.Config;
using Cleansia.Config.Database;
using Cleansia.Config.Health;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Functions.Core;
using Cleansia.Functions.Core.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cleansia.Tests.Functions;

/// <summary>
/// The Functions host's startup safety net. A queue/timer host with alwaysOn crashes into an opaque
/// "Application Error" 503 (no request logs) when its DI graph can't resolve a handler — exactly the
/// silent failure mode behind the 2026-07-18 dev outage. This composes the SAME service graph
/// <c>Program.cs</c> builds, with the app-settings SHAPE the deployed host gets (connection strings
/// present, every secret empty), then resolves every <c>Cleansia.Functions.Core</c> handler + the
/// hosted services in a scope — mirroring the runtime (no ValidateOnBuild; the real host doesn't). A
/// future change that adds a required dependency a handler ctor can't satisfy, or forgets to register a
/// new collaborator, trips THIS test in CI instead of a production 503.
/// </summary>
public class FunctionsHostStartupGuardTests
{
    // The app-settings SHAPE the deployed Functions host receives: connection strings resolve (well-formed
    // but unreachable — the eager type-catalog probe fails and is swallowed by design), every downstream
    // secret is an empty string exactly as the dev Bicep leaves the un-provisioned ones. If startup needs
    // a value that isn't here, that IS the bug this test exists to surface.
    private static IConfiguration DeployedShapeConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ConnectionString"] = "Host=localhost;Port=5432;Database=cleansia;Username=probe;Password=probe;Timeout=1;Command Timeout=1",
                ["ConnectionStrings:QueueStorageConnectionString"] = "UseDevelopmentStorage=true",
                ["ConnectionStrings:BlobContainerConfigurationConnectionString"] = "UseDevelopmentStorage=true",
            })
            .Build();

    private static ServiceProvider BuildFunctionsHost()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));

        var configuration = DeployedShapeConfiguration();
        // The Functions HostBuilder provides these two ambiently; the bare ServiceCollection does not, so
        // register them to mirror the real host (Program.cs calls AddHttpContextAccessor; IConfiguration is
        // the host's own). Omitting them would flag the whole graph for a harness gap, not a real defect.
        services.AddSingleton<IConfiguration>(configuration);
        services.AddHttpContextAccessor();
        // Production, not Development — the deployed host is Production (skips user-secrets, the branch
        // Program.cs guards). AddCoreBindings takes the env.
        IHostEnvironment env = new ProbeHostEnvironment();

        // ── the exact Program.cs composition ──
        services.AddCoreBindings(configuration, env);
        services.AddSingleton<IHostAudienceProvider>(new HostAudienceProvider("cleansia.functions"));
        services.AddScoped<FunctionsHealthCheck>();
        // The SAME shared registration Program.cs calls — NOT a reflection re-registration. This is what
        // makes the guard real: a handler added to the Handlers namespace but omitted from
        // AddFunctionsProcessing is discovered by FunctionHandlerTypes() below yet is unresolvable here,
        // so the test fails (rather than the test silently re-registering it, as a reflection loop would).
        services.AddFunctionsProcessing();

        // validateScopes: true catches a singleton capturing a scoped dependency (a captive-dependency
        // bug that manifests as intermittent runtime failure). validateOnBuild stays OFF to mirror the
        // real host's .Build() — the deployed host constructs lazily, so eager singleton construction here
        // would be STRICTER than production and could flag benign lazy clients.
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    // Every concrete handler the Worker resolves per trigger. Reflection over the Handlers namespace so a
    // newly-added handler is covered automatically (no hand-maintained list to drift).
    private static IReadOnlyList<Type> FunctionHandlerTypes() =>
        typeof(SendPushNotificationHandler).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true }
                        && t.Namespace == "Cleansia.Functions.Core.Handlers"
                        && t.Name.EndsWith("Handler", StringComparison.Ordinal))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

    [Fact]
    public void EveryFunctionsHandlerResolvesFromTheStartupGraph()
    {
        using var provider = BuildFunctionsHost();
        using var scope = provider.CreateScope();

        var handlerTypes = FunctionHandlerTypes();
        Assert.True(handlerTypes.Count >= 20,
            $"Expected the full Functions handler set; reflection found only {handlerTypes.Count} — the namespace filter drifted.");

        var unresolvable = new List<string>();
        foreach (var handler in handlerTypes)
        {
            try
            {
                _ = scope.ServiceProvider.GetRequiredService(handler);
            }
            catch (Exception ex)
            {
                unresolvable.Add($"{handler.Name}: {ex.GetType().Name} — {ex.Message}");
            }
        }

        Assert.True(unresolvable.Count == 0,
            "Functions handlers that cannot be constructed from the host DI graph (would crash the host at " +
            "startup/first trigger — an opaque 503):\n  " + string.Join("\n  ", unresolvable));
    }

    [Fact]
    public void TheHealthCheckAndHostedServicesResolve()
    {
        using var provider = BuildFunctionsHost();
        using var scope = provider.CreateScope();

        // The health probe body must itself be constructible — a broken health check that 500s is worse
        // than none.
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<FunctionsHealthCheck>());

        // The hosted services (the Npgsql type-catalog initializer) start with the host; if one can't be
        // constructed the host never reaches Run().
        var hostedServices = scope.ServiceProvider.GetServices<IHostedService>().ToList();
        Assert.Contains(hostedServices, s => s is NpgsqlTypeCatalogInitializer);
    }

    private sealed class ProbeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Cleansia.Functions";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
