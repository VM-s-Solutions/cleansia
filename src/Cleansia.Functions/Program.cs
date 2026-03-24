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
        var config = context.Configuration;
        var env = context.HostingEnvironment;

        services.AddCoreBindings(config, env);

        services.AddScoped<IPayPeriodBackgroundService, PayPeriodBackgroundService>();
        services.AddScoped<IPeriodReminderBackgroundService, PeriodReminderBackgroundService>();
        services.AddScoped<IDataRetentionBackgroundService, DataRetentionBackgroundService>();
    })
    .Build();

host.Run();
