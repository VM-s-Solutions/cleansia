using Cleansia.Config;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Respawn;
using AssemblyReference = Cleansia.Infra.Database.AssemblyReference;

namespace Cleansia.IntegrationTests;

public abstract class BaseIntegrationTest : BaseTransactionalPostgresSqlTest<CleansiaDbContext>, IAsyncLifetime
{
    protected readonly IConfiguration Configuration;
    protected readonly PostgresContainerFixture Fixture;
    private static bool _migrationsApplied;

    protected BaseIntegrationTest(PostgresContainerFixture fixture)
    {
        Fixture = fixture;
        Configuration = BuildConfiguration();
    }

    protected virtual IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .AddJsonFile("appsettings.IntegrationTests.json")
#if DEBUG
            .AddJsonFile("appsettings.IntegrationTests.Development.json")
#endif
            // The Fixture is the single source of truth for the test DB connection. Testcontainers
            // assigns a random host port (no fixed binding — see PostgresContainerFixture), so the
            // appsettings "ConnectionString" is only a placeholder. Override it here so AddCoreBindings
            // (AddDbContextBindings reads ConnectionStrings:ConnectionString) builds the test DbContext
            // against the SAME container that migrations + Respawn use. Without this the DbContext would
            // connect to whatever else happens to sit on the placeholder's host:port.
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ConnectionString"] = Fixture.GetConnectionString()
            })
            .Build();
    }

    public async Task InitializeAsync()
    {
        if (!_migrationsApplied)
        {
            await ApplyMigrationsAsync();
            _migrationsApplied = true;
        }
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    private async Task ApplyMigrationsAsync()
    {
        try
        {
            var options = new DbContextOptionsBuilder<CleansiaDbContext>()
                .UseNpgsql(Fixture.GetConnectionString(), x => x.MigrationsAssembly(AssemblyReference.Assembly))
                // The model can sit slightly ahead of the latest committed migration (a snapshot the
                // owner regenerates manually — NO ef here; e.g. the RefreshToken xmin concurrency token,
                // which maps to a Postgres system column and needs no DDL). Demote the pending-model-
                // changes guard from error to no-op so the migration-defined schema still applies. This
                // is DB-setup plumbing in the TEST fixture only; it mirrors HostTestPostgresFixture and
                // touches no production DbContext configuration.
                .ConfigureWarnings(w => w.Ignore(
                    Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
                .Options;
            await using var dbContext = new CleansiaDbContext(options);
            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                Console.WriteLine($"Applying migrations: {string.Join(", ", pendingMigrations)}");
                await dbContext.Database.MigrateAsync();
                Console.WriteLine("Migrations applied successfully.");
            }
            else
            {
                Console.WriteLine("No pending migrations found.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Migration failed: {ex}");
            throw;
        }
    }

    protected override async Task TestMethod<TResult>(
        Func<IServiceCollection, Task>? setup = null,
        Func<CleansiaDbContext, Task>? arrange = null,
        Func<IServiceProvider, Task<TResult>>? act = null,
        Func<CleansiaDbContext, TResult, Task>? assert = null,
        Func<CleansiaDbContext, Task>? cleanup = null,
        bool transactional = true)
    {
        await using var conn = new NpgsqlConnection(Fixture.GetConnectionString());
        await conn.OpenAsync();
        var respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToExclude = ["pg_catalog", "information_schema"]
        });
        await respawner.ResetAsync(conn);
        await base.TestMethod(Setup, arrange, act, assert, cleanup, transactional);
        return;

        async Task Setup(IServiceCollection services)
        {
            services.AddLogging(builder => { builder.AddConsole(); });
            services.AddHttpContextAccessor();
            services.AddSingleton<IConfiguration>(_ => Configuration);
            services.AddCoreBindings(Configuration, new TestHostEnvironment());

            services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(new TestClaimsPrincipalUser())));
            services.AddSingleton<IHostAudienceProvider>(new HostAudienceProvider(JwtAudiences.Customer));
            services.Replace(ServiceDescriptor.Singleton<IDatabaseConnectionString>(_ => new DatabaseConnectionString(Configuration)
            {
                ConnectionString = Fixture.GetConnectionString() ?? throw new ArgumentNullException(nameof(Fixture.GetConnectionString))
            }));

            if (setup is not null)
            {
                await setup(services);
            }
        }
    }

    protected async Task TestMethod<TResult>(
        Func<CleansiaDbContext, Task>? arrange = null,
        Func<IServiceProvider, Task<TResult>>? act = null,
        Func<CleansiaDbContext, TResult, Task>? assert = null,
        Func<CleansiaDbContext, Task>? cleanup = null,
        bool transactional = true)
    {
        await TestMethod(setup: null, arrange, act, assert, cleanup, transactional);
    }
}