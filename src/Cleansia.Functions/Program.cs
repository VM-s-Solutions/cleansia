using Cleansia.Config;
using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.ConfigureFunctionsApplicationInsights();

        // Required by UserSessionProvider and TenantProvider which depend on IHttpContextAccessor.
        // In Functions context there is no HTTP context, but the providers handle null gracefully.
        services.AddHttpContextAccessor();

        services.AddCoreBindings(context.Configuration, context.HostingEnvironment);

        services.AddScoped<IPayPeriodBackgroundService, PayPeriodBackgroundService>();
        services.AddScoped<IPeriodReminderBackgroundService, PeriodReminderBackgroundService>();
        services.AddScoped<IDataRetentionBackgroundService, DataRetentionBackgroundService>();
    })
    .Build();

host.Run();
