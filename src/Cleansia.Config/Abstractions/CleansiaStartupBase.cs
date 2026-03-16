using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.ServiceDefaults;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cleansia.Config.Abstractions;

public abstract class CleansiaStartupBase(IConfiguration configuration, IWebHostEnvironment environment)
{
    public IConfiguration Configuration { get; } = configuration;
    public IWebHostEnvironment Environment { get; } = environment;

    protected abstract string CorsPolicyName { get; }
    protected abstract string SwaggerTitle { get; }

    protected abstract void AddProjectServices(IServiceCollection services);

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        AddProjectServices(services);

        // Add Hangfire for background jobs
        services.AddHangfireServices(Configuration);

        var corsOrigins = Configuration.GetSection("CorsOrigins").Get<string[]>() ?? [];
        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                policy.WithOrigins(corsOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .WithExposedHeaders("Content-Disposition");
            });
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter("auth", limiterOptions =>
            {
                limiterOptions.PermitLimit = 10;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueLimit = 0;
            });
        });

        services.AddSwaggerGen();

        services.AddServiceDefaults(Configuration, Environment);
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IServiceProvider serviceProvider)
    {
        app.MigrateDatabase(env);

        app.Use((context, next) =>
        {
            context.Request.EnableBuffering();
            return next();
        });

        if (!env.IsProduction())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("swagger/v1/swagger.json", SwaggerTitle);
                c.RoutePrefix = string.Empty;
            });
        }

        app.UseMiddleware<RequestLoggingMiddleware>();

        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("An unexpected error occurred.");
            });
        });
        app.UseRouting();
        app.UseCors(CorsPolicyName);
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseHangfireConfiguration();
        HangfireConfiguration.ConfigureRecurringJobs();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapDefaultEndpoints();
        });
    }
}

public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<RequestLoggingMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        if (ShouldSkipLogging(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString();

        await LogRequestAsync(context, requestId);

        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);
            stopwatch.Stop();
            await LogResponseAsync(context, requestId, stopwatch.ElapsedMilliseconds);
            await responseBody.CopyToAsync(originalBodyStream);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogError(context, requestId, stopwatch.ElapsedMilliseconds, ex);
            throw;
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private async Task LogRequestAsync(HttpContext context, string requestId)
    {
        var request = context.Request;
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
        var userEmail = context.User?.FindFirst(ClaimTypes.Email)?.Value ?? "N/A";
        var requestBody = await ReadRequestBodyAsync(request);

        _logger.LogInformation(
            "[{RequestId}] {Method} {Path}{QueryString} | User: {UserId} ({UserEmail}) | IP: {IP} | Body: {Body}",
            requestId, request.Method, request.Path, request.QueryString,
            userId, userEmail,
            context.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
            string.IsNullOrEmpty(requestBody) ? "N/A" : requestBody);
    }

    private async Task LogResponseAsync(HttpContext context, string requestId, long durationMs)
    {
        var response = context.Response;
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
        var responseBody = await ReadResponseBodyAsync(response);

        var logLevel = response.StatusCode >= 500 ? LogLevel.Error :
                      response.StatusCode >= 400 ? LogLevel.Warning :
                      LogLevel.Information;

        _logger.Log(logLevel,
            "[{RequestId}] Response: {StatusCode} | Duration: {Duration}ms | User: {UserId} | Body: {Body}",
            requestId, response.StatusCode, durationMs, userId,
            string.IsNullOrEmpty(responseBody) ? "N/A" : TruncateBody(responseBody, 500));
    }

    private void LogError(HttpContext context, string requestId, long durationMs, Exception ex)
    {
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";

        _logger.LogError(ex,
            "[{RequestId}] Exception occurred | Duration: {Duration}ms | User: {UserId} | Path: {Path}",
            requestId, durationMs, userId, context.Request.Path);
    }

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        if (!request.Body.CanSeek) request.EnableBuffering();
        request.Body.Position = 0;
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        return TruncateBody(body, 1000);
    }

    private static async Task<string> ReadResponseBodyAsync(HttpResponse response)
    {
        response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(response.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        response.Body.Seek(0, SeekOrigin.Begin);
        return body;
    }

    private static string TruncateBody(string body, int maxLength)
    {
        if (string.IsNullOrEmpty(body) || body.Length <= maxLength) return body;
        return body.Substring(0, maxLength) + "... (truncated)";
    }

    private static bool ShouldSkipLogging(PathString path)
    {
        var pathValue = path.Value?.ToLower() ?? string.Empty;
        return pathValue.Contains("/health") ||
               pathValue.Contains("/swagger") ||
               pathValue.Contains(".js") ||
               pathValue.Contains(".css") ||
               pathValue.Contains(".map") ||
               pathValue.Contains("/hangfire");
    }
}

public static class HangfireConfiguration
{
    public static IServiceCollection AddHangfireServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ConnectionString");

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options =>
            {
                options.UseNpgsqlConnection(connectionString);
            }));

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = 2;
            options.ServerName = "Cleansia-Server";
        });

        services.AddScoped<IPayPeriodBackgroundService, PayPeriodBackgroundService>();
        services.AddScoped<IPeriodReminderBackgroundService, PeriodReminderBackgroundService>();
        services.AddScoped<IDataRetentionBackgroundService, DataRetentionBackgroundService>();

        return services;
    }

    public static IApplicationBuilder UseHangfireConfiguration(this IApplicationBuilder app)
    {
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = new[] { new HangfireAuthorizationFilter() },
            DashboardTitle = "Cleansia Background Jobs",
            StatsPollingInterval = 10000
        });

        return app;
    }

    public static void ConfigureRecurringJobs()
    {
        RecurringJob.AddOrUpdate<IPayPeriodBackgroundService>(
            "close-expired-pay-periods",
            service => service.CloseExpiredPeriodsAndOpenNewAsync(CancellationToken.None),
            Cron.Daily(2),
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        RecurringJob.AddOrUpdate<IPeriodReminderBackgroundService>(
            "send-period-end-reminders",
            service => service.SendPeriodEndRemindersAsync(CancellationToken.None),
            Cron.Daily(9),
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        RecurringJob.AddOrUpdate<IDataRetentionBackgroundService>(
            "data-retention-cleanup",
            service => service.RunAllRetentionTasksAsync(CancellationToken.None),
            "0 3 * * 0",
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }
}

public class HangfireAuthorizationFilter : Hangfire.Dashboard.IDashboardAuthorizationFilter
{
    public bool Authorize(Hangfire.Dashboard.DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

#if DEBUG
        return true;
#else
        return httpContext.User.Identity?.IsAuthenticated == true &&
               httpContext.User.IsInRole("Admin");
#endif
    }
}

public static class DatabaseMigrationExtensions
{
    public static void MigrateDatabase(this IApplicationBuilder app, IWebHostEnvironment environment)
    {
        var scopeFactory = app.ApplicationServices.GetService<IServiceScopeFactory>();
        if (scopeFactory is null) return;

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Cleansia.Infra.Database.CleansiaDbContext>();
        dbContext.Migrate();

        if (environment.IsDevelopment())
        {
            SeedDevelopmentData(dbContext, scope.ServiceProvider.GetRequiredService<ILogger<Cleansia.Infra.Database.CleansiaDbContext>>());
        }
    }

    private static void SeedDevelopmentData(Cleansia.Infra.Database.CleansiaDbContext dbContext, ILogger logger)
    {
        try
        {
            Console.WriteLine("[SEED] Starting seed data check...");

            // Check if seed data already exists (Languages table is populated by seed script)
            try
            {
                var hasData = dbContext.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*)::int AS \"Value\" FROM \"Languages\"").AsEnumerable().FirstOrDefault();
                if (hasData > 0)
                {
                    Console.WriteLine("[SEED] Seed data already exists, skipping.");
                    return;
                }
                Console.WriteLine("[SEED] Languages table is empty, will seed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SEED] Languages check failed ({ex.Message}), will attempt to seed.");
            }

            // Look for the seed SQL file relative to solution root
            var baseDir = AppContext.BaseDirectory;
            var solutionDir = FindSolutionDirectory(baseDir);
            Console.WriteLine($"[SEED] Base directory: {baseDir}");
            Console.WriteLine($"[SEED] Solution directory: {solutionDir ?? "NOT FOUND"}");

            if (solutionDir is null)
            {
                Console.WriteLine("[SEED] Could not locate solution directory. Skipping seed.");
                return;
            }

            var seedFilePath = Path.Combine(solutionDir, "Cleansia.Infra.Scripts", "SeedData", "insert_seed_data.sql");
            Console.WriteLine($"[SEED] Seed file path: {seedFilePath}");
            Console.WriteLine($"[SEED] File exists: {File.Exists(seedFilePath)}");

            if (!File.Exists(seedFilePath))
            {
                Console.WriteLine("[SEED] Seed file not found. Skipping seed.");
                return;
            }

            Console.WriteLine("[SEED] Reading and executing seed SQL...");
            var sql = File.ReadAllText(seedFilePath);

            // Use the raw connection directly to avoid EF Core parameter parsing issues
            var connection = dbContext.Database.GetDbConnection();
            var wasOpen = connection.State == System.Data.ConnectionState.Open;
            if (!wasOpen) connection.Open();
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.CommandTimeout = 120; // 2 min for large seed file
                command.ExecuteNonQuery();
            }
            finally
            {
                if (!wasOpen) connection.Close();
            }

            Console.WriteLine("[SEED] Development database seeded successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SEED] FAILED: {ex.Message}");
            logger.LogWarning(ex, "Failed to seed development data. This is non-fatal — you may need to seed manually.");
        }
    }

    private static string? FindSolutionDirectory(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            if (dir.GetFiles("*.sln").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
