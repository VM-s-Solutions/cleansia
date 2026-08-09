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
/// COUNTED rather than timed out. Counting is not by itself enough to make these assertions sound: the
/// connect is synchronous (<c>OpenConnection</c> inside <c>AddDbContextBindings</c>) and so is complete
/// before composition returns, but the accept that RECORDS it runs on the listener's own loop. A naked
/// read of the counter therefore races the observation — not the IO — and on a loaded machine loses.</para>
///
/// <para>Both directions of that race are closed here, and neither closure may be "simplified" away. The
/// <c>&gt; 0</c> leg waits on a signal the accept loop sets after the increment, so a starved thread pool
/// costs latency instead of a red. The <c>== 0</c> legs cannot wait for the absence of an event, so they
/// open one deliberate CONTROL connection after composing and block until the loop has accepted, counted
/// and closed THAT one, then assert the total is exactly 1: TCP hands connections to the accept loop in
/// the order their handshakes completed, so once the control connection has been counted, anything whose
/// connect COMPLETED during composition has been counted too — which is every connect composition can make
/// while the one it would make is synchronous.</para>
///
/// <para>Without that barrier the <c>== 0</c> legs pass VACUOUSLY — a connection opened during composition
/// but not yet accepted leaves the counter at 0 and the assertion green, so the five host legs report
/// success in exactly the case they exist to catch. Reading the counter directly is the bug, not the
/// simplification.</para>
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

        AssertNothingConnectedBesidesTheControlProbe(probe, $"{host}.AddServices");
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

        Assert.True(probe.WaitForConnection(ObservationTimeout),
            $"The eager type-catalog probe opened no connection within {ObservationTimeout.TotalSeconds:0}s " +
            "— either the opt-in stopped working or the loopback probe is no longer being reached, which " +
            "would also make the API-host assertions vacuous.");
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
        AssertNothingConnectedBesidesTheControlProbe(probe, "The EF model warm-up");
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
        AssertNothingConnectedBesidesTheControlProbe(probe, "Resolving CleansiaDbContext.Model");
    }

    private static readonly TimeSpan ObservationTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Returns as soon as the control connection comes back, so the generous timeout is paid only when the
    /// probe itself is broken — never on the pass path.
    /// </summary>
    private static void AssertNothingConnectedBesidesTheControlProbe(ClosingLoopbackListener probe, string subject)
    {
        var accepted = probe.DrainWithControlConnection(ObservationTimeout);

        Assert.True(accepted == 1,
            $"{subject} opened {accepted - 1} database connection(s). Only the test's own control " +
            "connection may be counted here.");
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
        private readonly ManualResetEventSlim connectionAccepted = new();
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

        public bool WaitForConnection(TimeSpan timeout) => connectionAccepted.Wait(timeout);

        /// <summary>
        /// The barrier the <c>== 0</c> assertions stand on: connects once and returns only after the accept
        /// loop has counted and closed THAT connection, so every earlier connect is already in the total it
        /// returns (the accept queue is FIFO). The close is what proves it — the loop increments before it
        /// disposes the accepted client, so a peer that sees the socket go away has been counted.
        /// </summary>
        public int DrainWithControlConnection(TimeSpan timeout)
        {
            using var control = new TcpClient();
            control.Connect(IPAddress.Loopback, Port);

            Assert.True(control.Client.Poll(timeout, SelectMode.SelectRead),
                "The loopback probe did not accept and close the control connection within " +
                $"{timeout.TotalSeconds:0}s, so the connection count cannot be trusted.");

            return AcceptedConnections;
        }

        private async Task AcceptLoopAsync()
        {
            while (!cancellation.IsCancellationRequested)
            {
                try
                {
                    // Increment and signal BEFORE the client is disposed: DrainWithControlConnection reads
                    // that close as proof the count already includes it.
                    using var client = await listener.AcceptTcpClientAsync(cancellation.Token);
                    Interlocked.Increment(ref acceptedConnections);
                    connectionAccepted.Set();
                }
                catch (OperationCanceledException) { return; }
                catch (ObjectDisposedException) { return; }
                catch (SocketException) { }
            }
        }

        public void Dispose()
        {
            cancellation.Cancel();
            listener.Dispose();
            cancellation.Dispose();
            connectionAccepted.Dispose();
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
