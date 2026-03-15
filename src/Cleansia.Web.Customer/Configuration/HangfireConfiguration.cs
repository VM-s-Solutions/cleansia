using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Web.Customer.Configuration;

public static class HangfireConfiguration
{
    public static IServiceCollection AddHangfireServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ConnectionString");

        // Add Hangfire services
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options =>
            {
                options.UseNpgsqlConnection(connectionString);
            }));

        // Add the processing server as IHostedService
        services.AddHangfireServer(options =>
        {
            options.WorkerCount = 2;
            options.ServerName = "Cleansia-Customer-Server";
        });

        // Register background services
        services.AddScoped<IDataRetentionBackgroundService, DataRetentionBackgroundService>();
        services.AddScoped<IStaleOrderCleanupService, StaleOrderCleanupService>();

        return services;
    }

    public static IApplicationBuilder UseHangfireConfiguration(this IApplicationBuilder app)
    {
        // Configure Hangfire Dashboard
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = new[] { new HangfireAuthorizationFilter() },
            DashboardTitle = "Cleansia Customer Background Jobs",
            StatsPollingInterval = 10000 // 10 seconds
        });

        return app;
    }

    public static void ConfigureRecurringJobs()
    {
        // Data retention cleanup - runs weekly Sunday at 3 AM UTC
        RecurringJob.AddOrUpdate<IDataRetentionBackgroundService>(
            "customer-data-retention-cleanup",
            service => service.RunAllRetentionTasksAsync(CancellationToken.None),
            "0 3 * * 0", // Sunday 3:00 AM UTC
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // Stale order cleanup - runs every 15 minutes to cancel abandoned Stripe checkout orders
        RecurringJob.AddOrUpdate<IStaleOrderCleanupService>(
            "stale-order-cleanup",
            service => service.CancelStaleOrdersAsync(CancellationToken.None),
            "*/15 * * * *", // Every 15 minutes
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });
    }
}
