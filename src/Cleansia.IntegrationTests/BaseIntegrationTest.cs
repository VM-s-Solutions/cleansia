using Cleansia.Config;
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
        var checkpoint = new Checkpoint
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToExclude = ["pg_catalog", "information_schema"]
        };
        await checkpoint.Reset(conn);
        await base.TestMethod(Setup, arrange, act, assert, cleanup, transactional);
        return;

        async Task Setup(IServiceCollection services)
        {
            services.AddLogging(builder => { builder.AddConsole(); });
            services.AddSingleton<IConfiguration>(_ => Configuration);
            services.AddCoreBindings(Configuration, new TestHostEnvironment());

            services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(new TestClaimsPrincipalUser())));
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