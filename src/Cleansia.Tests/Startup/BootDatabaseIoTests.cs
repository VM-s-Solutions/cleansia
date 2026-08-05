using System.Net;
using System.Net.Sockets;
using Cleansia.Config;
using Cleansia.Config.Database;
using Cleansia.Infra.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cleansia.Tests.Startup;

/// <summary>
/// Boot cost, pinned at the only two places it is observable without a deployed host: whether composing
/// the DI graph touches the database, and whether the EF model warm-up stays off the startup path.
///
/// <para>The probe is a loopback listener that accepts and immediately closes, so a connection attempt is
/// COUNTED rather than timed out — the assertions are about whether a socket was opened at all, not about
/// how long anything took, which is what keeps them from being flaky on a loaded machine.</para>
/// </summary>
public class BootDatabaseIoTests
{
    /// <summary>
    /// Driven through each host's REAL <c>AddServices</c> rather than through
    /// <c>AddCoreBindings</c> directly, because the thing that can regress is a call site: the opt-in is
    /// an optional argument, so a host that starts passing <c>true</c> — or a flipped default — is
    /// invisible in every other test. Four-of-five would be a hole, so the list is asserted complete.
    /// </summary>
    [Theory]
    [MemberData(nameof(ApiHostCompositions))]
    public void ComposingAnApiHostGraphOpensNoDatabaseConnection(string host)
    {
        using var probe = new ClosingLoopbackListener();

        var services = NewServiceCollection(probe.ConnectionString, out var configuration);
        InvokeAddServices(host, services, configuration);

        Assert.Equal(0, probe.AcceptedConnections);
    }

    [Fact]
    public void EveryApiHostIsCovered()
    {
        Assert.Equal(
            ["Cleansia.Web.Admin", "Cleansia.Web.Customer", "Cleansia.Web.Mobile.Customer",
             "Cleansia.Web.Mobile.Partner", "Cleansia.Web.Partner"],
            ApiHostExtensionTypes.Keys.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The counter-leg, and the reason the test above is not vacuous: the SAME composition with the
    /// Functions worker's opt-in does connect, synchronously, inside ConfigureServices. Flipping the
    /// default back turns the assertion above red rather than leaving five API hosts silently paying a
    /// blocking Postgres round trip (Npgsql's default connect timeout, 15s) on every cold start.
    /// </summary>
    [Fact]
    public void ComposingTheFunctionsWorkerGraphDoesOpenOne()
    {
        using var probe = new ClosingLoopbackListener();

        var services = NewServiceCollection(probe.ConnectionString, out var configuration);
        services.AddCoreBindings(configuration, ProbeEnvironment, eagerlyReloadNpgsqlTypeCatalog: true);

        Assert.True(probe.AcceptedConnections > 0,
            "The eager type-catalog probe opened no connection — either the opt-in stopped working or the " +
            "loopback probe is no longer being reached, which would also make the API-host assertion vacuous.");
    }

    [Fact]
    public async Task ModelWarmUpBuildsTheModelWithoutOpeningAConnection()
    {
        using var probe = new ClosingLoopbackListener();
        await using var provider = BuildGraph(probe.ConnectionString);

        var logger = new CapturingLogger();
        var service = new EfModelWarmupService(provider.GetRequiredService<IServiceScopeFactory>(), logger);

        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!;

        Assert.Contains(logger.Information, line => line.Contains("EF model warmed at boot", StringComparison.Ordinal));
        Assert.Empty(logger.Warnings);
        Assert.Equal(0, probe.AcceptedConnections);
    }

    /// <summary>
    /// The two composition facts that make the warm-up a warm-up rather than a slower start, and the only
    /// two that are OURS.
    ///
    /// <para>That the host does not wait for the work is the framework's guarantee, not this codebase's:
    /// on .NET 10 <see cref="BackgroundService"/> starts <c>ExecuteAsync</c> on the thread pool, so a
    /// <c>Thread.Sleep(3000)</c> at the top of it still lets <c>StartAsync</c> return in 0 ms. An
    /// assertion phrased as "the warm-up does not block startup" therefore passes no matter what the
    /// warm-up does, which is why there is no such test here. What CAN regress is the base type — a plain
    /// <see cref="IHostedService"/> awaiting its own body would block — and the registration ORDER: hosted
    /// services start sequentially, and <see cref="NpgsqlTypeCatalogInitializer"/> awaits a retry loop
    /// spanning up to ~2 minutes while a migration is in flight, so behind it the warm-up would not even
    /// begin until exactly the slow boot it exists to help was over.</para>
    /// </summary>
    [Fact]
    public void WarmUpIsABackgroundServiceRegisteredAheadOfTheRetryingInitializer()
    {
        using var probe = new ClosingLoopbackListener();
        var services = NewServiceCollection(probe.ConnectionString, out var configuration);
        services.AddCoreBindings(configuration, ProbeEnvironment);

        var hostedServiceImplementations = services
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
            .Select(descriptor => descriptor.ImplementationType)
            .ToList();

        var warmUp = hostedServiceImplementations.IndexOf(typeof(EfModelWarmupService));
        var typeCatalog = hostedServiceImplementations.IndexOf(typeof(NpgsqlTypeCatalogInitializer));

        Assert.True(warmUp >= 0, "The EF model warm-up is not registered as a hosted service.");
        Assert.True(warmUp < typeCatalog,
            "The EF model warm-up must be registered before NpgsqlTypeCatalogInitializer — hosted services " +
            $"start sequentially and that one awaits a ~2-minute retry loop (warm-up at {warmUp}, initializer at {typeCatalog}).");
        Assert.True(typeof(BackgroundService).IsAssignableFrom(typeof(EfModelWarmupService)),
            "The warm-up must stay a BackgroundService; a plain IHostedService is awaited inline by the host.");
    }

    /// <summary>
    /// The premise the warm-up rests on: EF caches one model per data source, so building it in a
    /// throw-away startup scope is what every later request scope reads. A model cache keyed per context
    /// instance — a custom <c>IModelCacheKeyFactory</c>, say — would leave the warm-up burning CPU for
    /// nothing, and this is where that shows up.
    /// </summary>
    [Fact]
    public async Task TheBuiltModelIsSharedAcrossScopes()
    {
        using var probe = new ClosingLoopbackListener();
        await using var provider = BuildGraph(probe.ConnectionString);

        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        var firstModel = first.ServiceProvider.GetRequiredService<CleansiaDbContext>().Model;
        var secondModel = second.ServiceProvider.GetRequiredService<CleansiaDbContext>().Model;

        Assert.Same(firstModel, secondModel);
        Assert.Equal(0, probe.AcceptedConnections);
    }

    private static readonly IHostEnvironment ProbeEnvironment = new BootProbeEnvironment();

    private static readonly IReadOnlyDictionary<string, Type> ApiHostExtensionTypes =
        new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["Cleansia.Web.Partner"] = typeof(Cleansia.Web.Partner.Extensions.ServiceExtensions),
            ["Cleansia.Web.Admin"] = typeof(Cleansia.Web.Admin.Extensions.ServiceExtensions),
            ["Cleansia.Web.Customer"] = typeof(Cleansia.Web.Customer.Extensions.ServiceExtensions),
            ["Cleansia.Web.Mobile.Partner"] = typeof(Cleansia.Web.Mobile.Partner.Extensions.ServiceExtensions),
            ["Cleansia.Web.Mobile.Customer"] = typeof(Cleansia.Web.Mobile.Customer.Extensions.ServiceExtensions),
        };

    public static TheoryData<string> ApiHostCompositions()
    {
        var data = new TheoryData<string>();
        foreach (var host in ApiHostExtensionTypes.Keys.Order(StringComparer.Ordinal))
        {
            data.Add(host);
        }

        return data;
    }

    private static void InvokeAddServices(string host, IServiceCollection services, IConfiguration configuration)
    {
        var method = ApiHostExtensionTypes[host].GetMethod(
            "AddServices", [typeof(IServiceCollection), typeof(IConfiguration), typeof(IHostEnvironment)]);

        Assert.NotNull(method);
        method.Invoke(null, [services, configuration, ProbeEnvironment]);
    }

    private static IServiceCollection NewServiceCollection(string connectionString, out IConfiguration configuration)
    {
        configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ConnectionString"] = connectionString,
                ["ConnectionStrings:QueueStorageConnectionString"] = "UseDevelopmentStorage=true",
                ["ConnectionStrings:BlobContainerConfigurationConnectionString"] = "UseDevelopmentStorage=true",
                ["JwtSettings:Secret"] = new string('k', 64),
                ["JwtSettings:Issuer"] = "cleansia",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddSingleton(configuration);
        services.AddHttpContextAccessor();
        return services;
    }

    private static ServiceProvider BuildGraph(string connectionString)
    {
        var services = NewServiceCollection(connectionString, out var configuration);
        // Explicit false so the warm-up assertions below measure the warm-up alone; whether the DEFAULT
        // connects is the separate question the per-host theory above owns.
        services.AddCoreBindings(configuration, ProbeEnvironment, eagerlyReloadNpgsqlTypeCatalog: false);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    /// <summary>
    /// Accepts and immediately closes, so an attempted connect is recorded and then fails fast. A
    /// listener that accepted and went silent would leave the test at the mercy of whether Npgsql's
    /// connect timeout also covers the startup handshake.
    /// </summary>
    private sealed class ClosingLoopbackListener : IDisposable
    {
        private readonly TcpListener listener;
        private readonly CancellationTokenSource cancellation = new();
        private int acceptedConnections;

        public ClosingLoopbackListener()
        {
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            _ = Task.Run(AcceptLoopAsync);
        }

        public int Port { get; }

        public int AcceptedConnections => Volatile.Read(ref acceptedConnections);

        public string ConnectionString =>
            $"Host=127.0.0.1;Port={Port};Database=cleansia;Username=probe;Password=probe;Timeout=2;Command Timeout=2";

        private async Task AcceptLoopAsync()
        {
            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    using var client = await listener.AcceptTcpClientAsync(cancellation.Token);
                    Interlocked.Increment(ref acceptedConnections);
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
        }

        public void Dispose()
        {
            cancellation.Cancel();
            listener.Dispose();
            cancellation.Dispose();
        }
    }

    private sealed class CapturingLogger : ILogger<EfModelWarmupService>
    {
        public List<string> Information { get; } = [];

        public List<string> Warnings { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Information) Information.Add(formatter(state, exception));
            if (logLevel == LogLevel.Warning) Warnings.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose() { }
        }
    }

    private sealed class BootProbeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "Cleansia.BootProbe";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
