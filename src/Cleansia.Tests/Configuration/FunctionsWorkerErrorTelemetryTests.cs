using Cleansia.Functions.Middleware;
using Cleansia.ServiceDefaults;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Sentry.Extensions.Logging;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// The isolated Functions worker's only off-box error signal. It runs every timer sweep and both fiscal
/// queues, it is not an ASP.NET host (so <c>UseSentryMonitoring</c> cannot reach it) and it builds no
/// OpenTelemetry pipeline (it never calls <c>AddServiceDefaults</c>), so what it gets is the Sentry
/// LOGGING provider: an <c>ILogger</c> error becomes a Sentry event.
///
/// <para>Two halves, and the second is the dangerous one. Wiring Sentry is worth nothing if a host that
/// has no DSN — dev, and any environment where the Key Vault reference has not landed yet — throws on
/// startup instead of running without telemetry. So the blank-DSN case is arranged explicitly rather
/// than left to a default.</para>
///
/// <para>The DSN-present case asserts the configured options and the registered provider rather than
/// building the logger factory: instantiating <c>SentryLoggerProvider</c> initialises the process-wide
/// <c>SentrySdk</c>, and a unit test must not leave a live SDK pointed at a fake ingest endpoint behind
/// for the rest of the run.</para>
/// </summary>
public class FunctionsWorkerErrorTelemetryTests
{
    private const string SampleDsn = "https://0123456789abcdef0123456789abcdef@o0.ingest.example.invalid/1";

    private static ServiceCollection WorkerLogging(string? dsn)
    {
        var settings = new Dictionary<string, string?>();
        if (dsn is not null)
        {
            settings["Sentry:Dsn"] = dsn;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddSentryMonitoring(configuration));
        return services;
    }

    [Fact]
    public void ADsnWiresTheSentryLoggerProvider()
    {
        var services = WorkerLogging(SampleDsn);

        // The provider type is internal to the SDK, so it is identified by its assembly.
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ILoggerProvider)
            && descriptor.ImplementationType?.Assembly == typeof(SentryLoggingOptions).Assembly);

        var options = services.BuildServiceProvider().GetRequiredService<IOptions<SentryLoggingOptions>>().Value;

        Assert.Equal(SampleDsn, options.Dsn);
    }

    /// <summary>
    /// <c>PoisonHandlerBase</c> step 2 is an <c>ILogger.LogError</c> call and nothing else, so the level
    /// at which a log becomes a Sentry EVENT rather than a breadcrumb is the alert threshold for every
    /// dead-lettered fiscal message. Pinned here rather than inherited from the SDK default.
    /// </summary>
    [Fact]
    public void AnErrorLogIsWhatBecomesAnEvent()
    {
        var options = WorkerLogging(SampleDsn)
            .BuildServiceProvider()
            .GetRequiredService<IOptions<SentryLoggingOptions>>()
            .Value;

        Assert.Equal(LogLevel.Error, options.MinimumEventLevel);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoDsnDisablesSentryAndTakesNoHostDown(string? dsn)
    {
        var provider = WorkerLogging(dsn).BuildServiceProvider();

        // Empty string is the SDK's explicit "disabled" sentinel; null would send it hunting for
        // SENTRY_DSN in the environment and whitespace fails initialisation.
        Assert.Equal(string.Empty, provider.GetRequiredService<IOptions<SentryLoggingOptions>>().Value.Dsn);

        // The startup this guard exists for: building the pipeline and logging through it, uncaught.
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("worker");
        logger.LogError(new InvalidOperationException("sweep failed"), "no DSN must not throw");
    }

    /// <summary>
    /// The leg the App Insights outage lived in — correct code on a path nothing called. Neither half of
    /// this wiring is reachable from a test of the extension alone, because the host is top-level
    /// statements; the assertion is therefore on the source the container actually runs.
    /// </summary>
    [Fact]
    public void TheWorkerHostWiresBothHalves()
    {
        var program = File.ReadAllText(RepoPath("src", "Cleansia.Functions", "Program.cs"));

        Assert.Contains(nameof(Extensions.AddSentryMonitoring), program, StringComparison.Ordinal);
        Assert.Contains(nameof(FunctionInvocationErrorMiddleware), program, StringComparison.Ordinal);
    }

    /// <summary>
    /// A failed invocation is reported to the Functions host over gRPC and logged in THAT process —
    /// outside this worker's <c>ILogger</c> pipeline, so past the Sentry provider. With Application
    /// Insights removed nothing reads the host side, so without this middleware a sweep that throws is
    /// silent everywhere.
    /// </summary>
    [Fact]
    public async Task AnUnhandledInvocationIsAlertedAndStillFails()
    {
        var logger = new CapturingLogger();
        var boom = new InvalidOperationException("sweep exploded");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new FunctionInvocationErrorMiddleware(logger).Invoke(Invocation(), _ => throw boom));

        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error && entry.Exception == boom);
    }

    /// <summary>
    /// Shutdown cancels every in-flight invocation. Alerting on those would page on every deploy, which
    /// is how an alert channel gets muted.
    /// </summary>
    [Fact]
    public async Task ShutdownCancellationIsNotAlerted()
    {
        var logger = new CapturingLogger();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new FunctionInvocationErrorMiddleware(logger).Invoke(Invocation(), _ => throw new OperationCanceledException()));

        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    private static FunctionContext Invocation()
    {
        var definition = new Mock<FunctionDefinition>();
        definition.SetupGet(d => d.Name).Returns("CleanupStalePendingOrders");

        var context = new Mock<FunctionContext>();
        context.SetupGet(c => c.FunctionDefinition).Returns(definition.Object);
        context.SetupGet(c => c.InvocationId).Returns("invocation-1");

        return context.Object;
    }

    private sealed class CapturingLogger : ILogger<FunctionInvocationErrorMiddleware>
    {
        public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, exception));
    }

    // Mirrors DeployWarmProbeCoverageTests — walk up to the *.sln, then out of src/ to the repo root.
    private static string RepoPath(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !directory.EnumerateFiles("*.sln").Any())
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "Could not locate the solution directory from the test base directory.");

        var path = Path.GetFullPath(Path.Combine([directory!.FullName, "..", .. segments]));
        Assert.True(File.Exists(path), $"Expected source file not found: {path}");
        return path;
    }
}
