using System.Threading.RateLimiting;
using Cleansia.ServiceDefaults;
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

    protected abstract Type RequestLoggingMiddlewareType { get; }

    protected abstract void AddProjectServices(IServiceCollection services);

    /// <summary>
    /// Host-specific middleware that runs after authentication but before
    /// authorization. Default is no-op. Customer / Partner / Admin override
    /// to register CSRF validation; Mobile inherits the no-op.
    /// </summary>
    protected virtual void UseHostAuthMiddleware(IApplicationBuilder app) { }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        AddProjectServices(services);

        var corsOrigins = Configuration.GetSection("CorsOrigins").Get<string[]>() ?? [];
        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                policy.WithOrigins(corsOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    // Required so the browser sends our HttpOnly auth cookies
                    // on cross-origin requests (SPA on a different origin than
                    // the API). Note: incompatible with AllowAnyOrigin, which
                    // is why CorsOrigins is a fixed list.
                    .AllowCredentials()
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

        app.UseMiddleware(RequestLoggingMiddlewareType);

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
        // Hook for host-specific middleware that needs HttpContext.User populated
        // but should run before authorization (e.g. CSRF validation on web hosts;
        // mobile leaves this empty since it has no cookie surface).
        UseHostAuthMiddleware(app);
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapDefaultEndpoints();
        });
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

        // In non-Development environments, migrations are applied by the CI/CD pipeline
        // (EF migrations bundle) before any API is deployed. This prevents race conditions
        // when multiple APIs start simultaneously and try to run migrations concurrently.
        if (environment.IsDevelopment())
        {
            dbContext.Migrate();
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
