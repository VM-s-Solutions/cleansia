using System.Reflection;
using Cleansia.Config;
using Cleansia.Config.Health;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Functions.Core;
using Cleansia.Functions.Middleware;
using Cleansia.ServiceDefaults;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(worker => worker.UseMiddleware<FunctionInvocationErrorMiddleware>())
    .ConfigureAppConfiguration((context, config) =>
    {
        // Committed production cron defaults for the four recurring/notification timers
        // (the %AppSetting% TimerTrigger tokens resolve from these). The Functions platform
        // app-settings (env) and, in dev, local.settings.json Values override them, so
        // promotion is config-only.
        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

        // HostBuilder doesn't auto-load user secrets like WebApplication does.
        if (context.HostingEnvironment.IsDevelopment())
        {
            config.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);
        }
    })
    // This host's ONLY off-box error signal. Sentry:Dsn arrives as the Sentry__Dsn app setting, the same
    // Key Vault secret the five API hosts read; blank or absent leaves the SDK disabled.
    .ConfigureLogging((context, logging) => logging.AddSentryMonitoring(context.Configuration))
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddHttpContextAccessor();

        // eagerlyReloadNpgsqlTypeCatalog: this worker is the one host whose triggers can fire before
        // IHostedService start completes, so it pays the synchronous type-catalog probe. The five API
        // hosts leave it off — see DbContextBindingExtensions.
        services.AddCoreBindings(
            context.Configuration, context.HostingEnvironment, eagerlyReloadNpgsqlTypeCatalog: true);

        // Sentinel binding — MediatR's assembly scan registers the Auth handlers
        // which depend on IHostAudienceProvider; the Functions host never issues
        // tokens but DI still validates the ctor at startup.
        services.AddSingleton<IHostAudienceProvider>(new HostAudienceProvider("cleansia.functions"));

        // The GET /api/health probe body (HealthFunction is its thin HTTP shell). Scoped — it resolves
        // the scoped CleansiaDbContext for its database probe. Stays here (a Cleansia.Config type the
        // Functions.Core registration extension deliberately doesn't reference).
        services.AddScoped<FunctionsHealthCheck>();

        // The background services + every per-trigger handler — the ONE registration list, shared with
        // FunctionsHostStartupGuardTests so a handler added but not registered fails CI, not production.
        services.AddFunctionsProcessing();
    })
    .Build();

host.Run();
