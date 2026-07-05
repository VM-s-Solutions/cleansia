using Cleansia.Config.RateLimiting;
using Cleansia.ServiceDefaults;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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

    // Swagger fail-closed allow-list. The old gate (`!env.IsProduction()`)
    // published the full API surface on Staging / QA / Demo and on any mis-set ASPNETCORE_ENVIRONMENT.
    // Swagger now mounts ONLY in Development; every other env string (Production, Staging, QA, Demo, or
    // an unrecognized value) fails closed. Development DX is preserved.
    public static bool SwaggerShouldServe(string environmentName) =>
        string.Equals(environmentName, Environments.Development, StringComparison.Ordinal);

    // ADR-0003 D3 pure-guard pattern (mirrors
    // RateLimitPolicies.ValidateForwardedHeadersConfig). If Swagger WOULD serve (the Development
    // branch) but CorsOrigins carries a public `cleansia.cz` origin — i.e. a prod-shaped config running
    // under a mis-set env string — the host REFUSES TO BOOT, so Swagger can never be exposed on a
    // production-shaped deployment. No-op otherwise (local origins, no origins, or Swagger off).
    public static void GuardSwaggerExposure(bool swaggerWouldServe, string[] corsOrigins)
    {
        if (!swaggerWouldServe) return;

        var publicOrigin = (corsOrigins ?? [])
            .FirstOrDefault(o => !string.IsNullOrWhiteSpace(o)
                && o.Contains("cleansia.cz", StringComparison.OrdinalIgnoreCase));

        if (publicOrigin is not null)
        {
            throw new InvalidOperationException(
                "Swagger would serve (Development branch) but CorsOrigins contains a public origin " +
                $"('{publicOrigin}'). This is a prod-shaped config under a mis-set ASPNETCORE_ENVIRONMENT. " +
                "Refusing to boot rather than expose the API surface (ADR-0003 D3). " +
                "Set ASPNETCORE_ENVIRONMENT correctly (Swagger is Development-only) or remove the public " +
                "cleansia.cz origin from CorsOrigins.");
        }
    }

    /// <summary>
    /// Host-specific middleware that runs after authentication but before
    /// authorization. Default is no-op. Customer / Partner / Admin override
    /// to register CSRF validation; Mobile inherits the no-op.
    /// </summary>
    protected virtual void UseHostAuthMiddleware(IApplicationBuilder app) { }

    /// <summary>
    /// The one host JSON configuration, shared by all five APIs and pinned by
    /// <c>TolerantDateOnlyConverterTests</c> so a dropped registration fails in CI.
    /// </summary>
    public static void ConfigureJsonSerialization(System.Text.Json.JsonSerializerOptions options)
    {
        // Allow incoming JSON to provide numeric properties as
        // strings (e.g. price `"12.50"` instead of `12.50`).
        options.NumberHandling =
            System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString;

        // Tolerant enum deserialization for Kotlin clients —
        // see [TolerantEnumConverterFactory] for the rationale.
        options.Converters.Add(new TolerantEnumConverterFactory());

        // Tolerant DateOnly deserialization for the Swift clients —
        // see [TolerantDateOnlyConverter] for the rationale.
        options.Converters.Add(new TolerantDateOnlyConverter());
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(options => ConfigureJsonSerialization(options.JsonSerializerOptions));
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

        // ADR-0003 (ADR-RATELIMIT) — partitioned "auth"/"interactive" policies (per real client IP
        // for anonymous, per JWT sub for authenticated), the global anonymous cardinality cap, and
        // the Retry-After/observability rejection behavior. Defined ONCE here for all five hosts;
        // policy NAMES are preserved so existing [EnableRateLimiting] sites are untouched. See D1/D2/
        // D6/D7/D8 + the partition-key functions in RateLimitPolicies.
        RateLimitPolicies.AddCleansiaRateLimiter(services, Configuration);

        // ADR-0003 D3 — establish the real client IP behind the App Service front end (trusted
        // X-Forwarded-For, config-driven ForwardLimit + narrow KnownNetworks/KnownProxies) and the
        // FAIL-CLOSED startup guard: in non-Development the host REFUSES TO BOOT on an unset or
        // over-broad trust config. Without this a per-IP partition collapses to one bucket (the
        // front-end IP). Must precede UseForwardedHeaders in the pipeline below.
        RateLimitPolicies.ConfigureForwardedHeaders(services, Configuration, Environment);

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

        // ADR-0003 D4 — UseForwardedHeaders at the TOP (before RequestLogging and UseRateLimiter) so
        // BOTH the audit-log IP and the rate-limiter per-IP partition read the REAL client IP, not
        // the App Service front-end IP. Trust boundary + fail-closed guard are configured in
        // ConfigureServices via RateLimitPolicies.ConfigureForwardedHeaders.
        app.UseForwardedHeaders();

        // Swagger fail-closed gate (Development-only allow-list) + the ADR-0003
        // D3 boot guard: refuse to boot if Swagger would serve under a prod-shaped CORS config.
        // Stays at the ADR-0003 pipeline position (EnableBuffering -> UseForwardedHeaders -> [Swagger
        // if Development] -> RequestLogging ...).
        var swaggerShouldServe = SwaggerShouldServe(env.EnvironmentName);
        GuardSwaggerExposure(
            swaggerShouldServe,
            Configuration.GetSection("CorsOrigins").Get<string[]>() ?? []);
        if (swaggerShouldServe)
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
        // ADR-0003 D4 — UseRateLimiter runs AFTER UseAuthentication so HttpContext.User is populated
        // and the per-`sub` partition branch can fire (anonymous → per-IP, authenticated → per-sub).
        app.UseAuthentication();
        app.UseRateLimiter();
        // Hook for host-specific middleware that needs HttpContext.User populated
        // but should run before authorization (e.g. CSRF validation on web hosts;
        // mobile leaves this empty since it has no cookie surface). ADR-0003 D4 keeps this CSRF hop
        // exactly here — after the limiter band, before UseAuthorization (unchanged position).
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
            // The migration's own connection is the data source's FIRST connection on a fresh DB —
            // it caches the Postgres type catalog BEFORE the migration creates the citext/pg_trgm
            // extensions, so every citext column afterwards reads as the unknown type "-.-"
            // (InvalidCastException) until the process restarts. Reloading rebuilds the catalog
            // with the extensions present.
            var connection = dbContext.Database.GetDbConnection();
            if (connection is Npgsql.NpgsqlConnection npgsqlConnection)
            {
                if (npgsqlConnection.State != System.Data.ConnectionState.Open)
                {
                    npgsqlConnection.Open();
                }

                npgsqlConnection.ReloadTypes();
            }

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
