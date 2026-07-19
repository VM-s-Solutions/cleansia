using System.Reflection;
using Cleansia.Config;
using Cleansia.Config.Health;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Functions.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
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
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddHttpContextAccessor();

        services.AddCoreBindings(context.Configuration, context.HostingEnvironment);

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
