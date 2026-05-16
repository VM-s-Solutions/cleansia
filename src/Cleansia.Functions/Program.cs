using System.Reflection;
using Cleansia.Config;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((context, config) =>
    {
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

        services.AddScoped<IPayPeriodBackgroundService, PayPeriodBackgroundService>();
        services.AddScoped<IPeriodReminderBackgroundService, PeriodReminderBackgroundService>();
        services.AddScoped<IDataRetentionBackgroundService, DataRetentionBackgroundService>();
        services.AddScoped<IRefreshTokenCleanupService, RefreshTokenCleanupService>();
    })
    .Build();

host.Run();
