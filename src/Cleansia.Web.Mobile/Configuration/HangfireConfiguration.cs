using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Web.Mobile.Configuration;

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
            options.WorkerCount = 2; // Number of concurrent jobs
            options.ServerName = "Cleansia-Mobile-Server";
        });

        // Register background services
        services.AddScoped<IPayPeriodBackgroundService, PayPeriodBackgroundService>();
        services.AddScoped<IPeriodReminderBackgroundService, PeriodReminderBackgroundService>();

        return services;
    }

    public static IApplicationBuilder UseHangfireConfiguration(this IApplicationBuilder app)
    {
        // Configure Hangfire Dashboard
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = new[] { new HangfireAuthorizationFilter() },
            DashboardTitle = "Cleansia Mobile Background Jobs",
            StatsPollingInterval = 10000 // 10 seconds
        });

        return app;
    }

    public static void ConfigureRecurringJobs()
    {
        // Close expired pay periods and open new ones - runs daily at 2 AM UTC
        RecurringJob.AddOrUpdate<IPayPeriodBackgroundService>(
            "close-expired-pay-periods",
            service => service.CloseExpiredPeriodsAndOpenNewAsync(CancellationToken.None),
            Cron.Daily(2), // 2 AM UTC
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // Send period end reminder emails - runs daily at 9 AM UTC
        RecurringJob.AddOrUpdate<IPeriodReminderBackgroundService>(
            "send-period-end-reminders",
            service => service.SendPeriodEndRemindersAsync(CancellationToken.None),
            Cron.Daily(9), // 9 AM UTC
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });
    }
}
