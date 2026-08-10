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
/// That Application Insights is GONE and stays gone. The workspace and the component were removed from
/// both environments by owner ruling — App Insights is workspace-based only, so "keep the component,
/// drop the Log Analytics workspace" would have re-created the same bill under a name the template does
/// not control.
///
/// <para>This class used to pin the opposite, and the defect it was written for is worth keeping in view
/// because it is the failure mode of every wiring change: <c>UseAzureMonitor</c> was added under a commit
/// message reading "now every API host ships its OTel pipeline to App Insights", and it shipped nothing —
/// it hung off <c>AddServiceDefaults(IHostApplicationBuilder)</c>, while all five APIs reach service
/// registration through <c>CleansiaStartupBase.ConfigureServices</c> and the <see cref="IServiceCollection"/>
/// overload. Real code, correct code, on a path nothing called. So the legs are kept and inverted rather
/// than deleted: each one still exercises the path a host actually runs, and now asserts that nothing
/// arrives on it.</para>
///
/// <para>The exporter code itself is deliberately NOT deleted. It is guarded on its own configuration
/// value, no environment supplies that value, and leaving it dormant is what keeps reinstating App
/// Insights a Bicep change rather than a code change — which is the shape the removal commit relied on
/// ("the setting is ABSENT, not blank"). The last test pins that dormancy; the first pins that no
/// template has quietly woken it up.</para>
///
/// <para>Out of scope, and not a contradiction: <c>Cleansia.Functions</c> still calls
/// <c>AddApplicationInsightsTelemetryWorkerService()</c>. That is the classic SDK, not this OTel
/// exporter, and it no-ops without the same absent setting; the worker's real error signal is Sentry
/// (<c>FunctionsWorkerErrorTelemetryTests</c>).</para>
/// </summary>
public class AppInsightsRemovalGuardTests
{
    private const string ConnectionStringAppSetting = "APPLICATIONINSIGHTS_CONNECTION_STRING";

    private const string SampleConnectionString =
        "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://example.invalid/";

    /// <summary>
    /// The regression this pins is a well-meant reinstatement — someone re-adding the component, the
    /// workspace it drags back with it, or just the app setting on one host. Comments are stripped first
    /// on purpose: <c>functionApp.bicep</c> documents the setting's ABSENCE by name, and that sentence
    /// must stay legal.
    /// </summary>
    [Fact]
    public void NoTemplateHandsAnyHostAnAppInsightsConnectionString()
    {
        string[] banned =
        [
            ConnectionStringAppSetting,
            "Microsoft.Insights/components",
            "Microsoft.OperationalInsights/workspaces",
            "appInsights",
        ];

        var offences = DeployTemplates()
            .SelectMany(file => banned
                .Where(token => WithoutComments(File.ReadAllText(file)).Contains(token, StringComparison.OrdinalIgnoreCase))
                .Select(token => $"{Path.GetFileName(file)} → {token}"))
            .ToList();

        Assert.True(offences.Count == 0,
            "Application Insights / Log Analytics is back in the deployment templates: "
            + string.Join(", ", offences)
            + ". The workspace was the largest single line on the Azure bill and both resources were removed together.");
    }

    /// <summary>
    /// The overload all five APIs reach through <c>UseStartup&lt;Startup&gt;</c>, given the configuration
    /// they are actually deployed with: no exporter registered at all — not an exporter pointed at
    /// nothing. The log-pipeline half matters as much as the trace half, because the distro registers the
    /// OpenTelemetry <see cref="ILoggerProvider"/> alongside the exporter.
    /// </summary>
    [Fact]
    public void TheOverloadTheApiHostsUseRegistersNoExporter()
    {
        var services = ServicesWith(connectionString: null);

        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IConfigureOptions<AzureMonitorOptions>));

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ILoggerProvider));

        var connectionString = services.BuildServiceProvider()
            .GetRequiredService<IOptions<AzureMonitorOptions>>()
            .Value.ConnectionString;

        Assert.Null(connectionString);
    }

    /// <summary>
    /// The anti-drift half. The two overloads must stay in step in both directions — something added to
    /// one and not the other is invisible on the other exactly as this exporter once was.
    /// </summary>
    [Fact]
    public void TheHostApplicationBuilderOverloadRegistersNoExporterEither()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddServiceDefaults();

        var connectionString = builder.Services
            .BuildServiceProvider()
            .GetRequiredService<IOptions<AzureMonitorOptions>>()
            .Value.ConnectionString;

        Assert.Null(connectionString);
    }

    /// <summary>
    /// The leg the original defect lived in, kept because it is the only one that would catch a
    /// connection string hard-coded into the startup every API host runs.
    /// </summary>
    [Fact]
    public void TheStartupEveryApiHostRunsRegistersNoExporter()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
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

        Assert.Null(connectionString);
    }

    /// <summary>
    /// The dormant branch still works. Deleting it would make reinstating App Insights a code change on
    /// five hosts; leaving it guarded makes it one app setting. Both overloads are asserted because the
    /// day someone reinstates it is the day the drift above would bite.
    /// </summary>
    [Fact]
    public void ReinstatingAppInsightsStaysABicepChange()
    {
        var fromServiceCollection = ServicesWith(SampleConnectionString)
            .BuildServiceProvider()
            .GetRequiredService<IOptions<AzureMonitorOptions>>()
            .Value.ConnectionString;

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ConnectionStringAppSetting] = SampleConnectionString,
        });
        builder.AddServiceDefaults();

        var fromHostBuilder = builder.Services
            .BuildServiceProvider()
            .GetRequiredService<IOptions<AzureMonitorOptions>>()
            .Value.ConnectionString;

        Assert.Equal(SampleConnectionString, fromServiceCollection);
        Assert.Equal(SampleConnectionString, fromHostBuilder);
    }

    private static ServiceCollection ServicesWith(string? connectionString)
    {
        var settings = new Dictionary<string, string?>();
        if (connectionString is not null)
        {
            settings[ConnectionStringAppSetting] = connectionString;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddServiceDefaults(configuration, new StubEnvironment());
        return services;
    }

    private static string WithoutComments(string template) =>
        Regex.Replace(Regex.Replace(template, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline), @"//[^\n]*", string.Empty);

    private static IEnumerable<string> DeployTemplates()
    {
        var bicep = Path.GetDirectoryName(RepoPath("deploy", "bicep", "main.bicep"))!;

        var templates = Directory.EnumerateFiles(bicep, "*.bicep", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(bicep, "*.bicepparam", SearchOption.AllDirectories))
            .ToList();

        Assert.True(templates.Count > 1, $"Expected the Bicep module tree under {bicep}, found {templates.Count} file(s).");
        return templates;
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
