using System.Text.RegularExpressions;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Cleansia.Config.Abstractions;
using Cleansia.ServiceDefaults;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// That the Application Insights exporter is REACHED from the path the five API hosts actually run.
///
/// <para>The defect these pin is not a missing exporter. <c>UseAzureMonitor</c> was added under a commit
/// message reading "now every API host ships its OTel pipeline to App Insights", and it shipped nothing:
/// it hung off <c>AddServiceDefaults(IHostApplicationBuilder)</c>, and the only call site in the solution
/// — <c>CleansiaStartupBase.ConfigureServices</c>, which all five APIs reach through
/// <c>UseStartup&lt;Startup&gt;</c> — takes the <see cref="IServiceCollection"/> overload. Real code,
/// correct code, on a path nothing called, through every deploy that followed, while seven hosts were
/// handed the connection string. Nothing failed and nothing warned; the only symptom was an empty
/// resource.</para>
///
/// <para>So the assertions deliberately span the whole chain rather than the registration alone: the app
/// setting name comes out of the Bicep that sets it, the value is followed to the options the exporter
/// reads, and the overload the hosts use is exercised through the real <c>CleansiaStartupBase</c>. A test
/// that only called the extension method directly would have been green throughout the outage.</para>
/// </summary>
public class AppInsightsExporterWiringTests
{
    private const string SampleConnectionString =
        "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://example.invalid/";

    /// <summary>
    /// The app-setting name read out of <c>main.bicep</c> rather than typed here. The regex takes the
    /// SHAPE (an upper-case App Service setting name assigned the App Insights module output) and the
    /// NAME from the template, so renaming the setting in Bicep without renaming it in code fails here
    /// instead of on the next incident.
    /// </summary>
    private static string ConnectionStringAppSettingName()
    {
        var bicep = File.ReadAllText(RepoPath("deploy", "bicep", "main.bicep"));

        var names = Regex.Matches(bicep, @"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*appInsights\.outputs\.connectionString")
            .Select(match => match.Groups["name"].Value)
            .Where(name => name == name.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(names.Count == 1,
            $"Expected exactly one App Insights connection-string app setting in main.bicep, found: [{string.Join(", ", names)}].");

        return names[0];
    }

    private static ServiceCollection ServicesWith(string? connectionString)
    {
        var settings = new Dictionary<string, string?>();
        if (connectionString is not null)
        {
            settings[ConnectionStringAppSettingName()] = connectionString;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddServiceDefaults(configuration, new StubEnvironment());
        return services;
    }

    [Fact]
    public void TheOverloadTheApiHostsUseReachesTheExporter()
    {
        var connectionString = ServicesWith(SampleConnectionString)
            .BuildServiceProvider()
            .GetRequiredService<IOptions<AzureMonitorOptions>>()
            .Value.ConnectionString;

        Assert.Equal(SampleConnectionString, connectionString);
    }

    /// <summary>
    /// The anti-drift half. Both overloads must ship, or the next thing added to one of them is invisible
    /// on the other exactly as this exporter was.
    /// </summary>
    [Fact]
    public void TheHostApplicationBuilderOverloadReachesTheSameExporter()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ConnectionStringAppSettingName()] = SampleConnectionString,
        });

        builder.AddServiceDefaults();

        var connectionString = builder.Services
            .BuildServiceProvider()
            .GetRequiredService<IOptions<AzureMonitorOptions>>()
            .Value.ConnectionString;

        Assert.Equal(SampleConnectionString, connectionString);
    }

    /// <summary>
    /// The guard that keeps a developer's laptop and the test hosts off the wire: no connection string,
    /// no exporter registered at all — not an exporter pointed at nothing.
    /// </summary>
    [Fact]
    public void WithoutAConnectionStringNoExporterIsRegistered()
    {
        var services = ServicesWith(connectionString: null);

        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IConfigureOptions<AzureMonitorOptions>));

        var connectionString = services.BuildServiceProvider()
            .GetRequiredService<IOptions<AzureMonitorOptions>>()
            .Value.ConnectionString;

        Assert.Null(connectionString);
    }

    /// <summary>
    /// What an operator is actually owed: the LOG pipeline, not only request traces. A stack trace
    /// reaches App Insights as a log record, so if the exporter registered traces alone the resource
    /// would fill with green requests and still answer nothing about the 500 that produced one. The
    /// distro registers the OpenTelemetry <see cref="ILoggerProvider"/> itself — this asserts that
    /// contract with the dependency, so a distro upgrade that dropped it is a red test rather than a
    /// silent loss of the only signal worth having.
    /// </summary>
    [Fact]
    public void TheExporterBringsTheLogPipelineAndNotOnlyTraces()
    {
        Assert.Contains(ServicesWith(SampleConnectionString),
            descriptor => descriptor.ServiceType == typeof(ILoggerProvider));

        Assert.DoesNotContain(ServicesWith(connectionString: null),
            descriptor => descriptor.ServiceType == typeof(ILoggerProvider));
    }

    /// <summary>
    /// The leg the original defect lived in: not "does the extension register the exporter" but "does
    /// the startup every API host runs get there". Exercises the real
    /// <see cref="CleansiaStartupBase.ConfigureServices"/> body through a host-shaped subclass.
    /// </summary>
    [Fact]
    public void TheStartupEveryApiHostRunsReachesTheExporter()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ConnectionStringAppSettingName()] = SampleConnectionString,
                ["ForwardedHeaders:KnownNetworks"] = "100.64.0.0/10",
                ["CorsOrigins:0"] = "http://localhost:4200",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        new ProbeStartup(configuration, new StubEnvironment()).ConfigureServices(services);

        var connectionString = services.BuildServiceProvider()
            .GetRequiredService<IOptions<AzureMonitorOptions>>()
            .Value.ConnectionString;

        Assert.Equal(SampleConnectionString, connectionString);
    }

    private sealed class ProbeStartup(IConfiguration configuration, IWebHostEnvironment environment)
        : CleansiaStartupBase(configuration, environment)
    {
        protected override string CorsPolicyName => "probe";
        protected override string SwaggerTitle => "probe";
        protected override Type RequestLoggingMiddlewareType => typeof(ProbeStartup);

        protected override void AddProjectServices(IServiceCollection services) { }
    }

    private sealed class StubEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Cleansia.Tests";
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static string RepoPath(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !directory.EnumerateFiles("*.sln").Any())
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "Could not locate the solution directory from the test base directory.");

        var path = Path.GetFullPath(Path.Combine([directory!.FullName, "..", .. segments]));
        Assert.True(File.Exists(path), $"Expected deploy artifact not found: {path}");
        return path;
    }
}
