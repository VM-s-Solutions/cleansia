using System.Text;
using System.Text.RegularExpressions;
using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using AssemblyReference = Cleansia.Infra.Database.AssemblyReference;

namespace Cleansia.IntegrationTests.Deploy;

/// <summary>
/// deploy/db/grant-app-login.sql first runs for real on a production deploy, where every API host and the
/// Functions app connect as the login it creates. So it runs here the way deploy-azure.yml runs it: psql,
/// twice, as a non-superuser administrator with CREATEROLE that ran the migration and owns what it made
/// (Azure's administrator shape), and the login is then used the way the hosts use it.
/// </summary>
[Collection("PostgresCollection")]
public class AppLoginGrantScriptTests(PostgresContainerFixture fixture)
{
    private const string Database = "grant_script";
    private const string Admin = "grant_script_admin";
    private const string AdminPassword = "AdminPassword0";
    private const string AppLogin = "grant_script_app";
    private const string AppPassword = "AppPassword0";

    private static Task? prepared;

    [Fact]
    public async Task The_app_login_reads_and_writes_rows_the_way_the_hosts_do()
    {
        await PrepareOnceAsync();

        await using (var connection = await OpenAsAppLoginAsync())
        {
            await BaseIntegrationTest.SeedTenantRegistryAsync(connection);
        }

        await using (var context = NewAppLoginContext())
        {
            context.OutboxMessages.Add(OutboxMessage.Create(QueueNames.GenerateReceipt, "receipt:ORDER-1", "{}", null));
            await context.CommitAsync(CancellationToken.None);
        }

        await using (var context = NewAppLoginContext())
        {
            var now = DateTimeOffset.UtcNow;
            var claimed = Assert.Single(await new OutboxMessageRepository(context)
                .ClaimPendingBatchAsync("drainer-1", 10, now, now.AddMinutes(-2), CancellationToken.None));
            context.OutboxMessages.Remove(claimed);
            await context.CommitAsync(CancellationToken.None);
        }

        await using (var context = NewAppLoginContext())
        {
            var counters = new FiscalCounterRepository(context, new FixedTenantProvider(TestTenants.Default), SystemSession());
            Assert.Equal(1L, await counters.AllocateNextAsync(2026, "cz-eet2", CancellationToken.None));
            Assert.Equal(2L, await counters.AllocateNextAsync(2026, "cz-eet2", CancellationToken.None));
            Assert.False(await context.OutboxMessages.IgnoreQueryFilters().AnyAsync());
        }
    }

    [Theory]
    [InlineData("CREATE TABLE \"Intruder\" (\"Id\" integer)")]
    [InlineData("ALTER TABLE \"OutboxMessages\" ADD COLUMN \"Intruder\" integer")]
    [InlineData("DROP TABLE \"OutboxMessages\"")]
    [InlineData("TRUNCATE \"OutboxMessages\"")]
    public async Task The_app_login_cannot_change_the_schema_or_empty_a_table(string statement)
    {
        await PrepareOnceAsync();

        await using var connection = await OpenAsAppLoginAsync();
        var refusal = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, statement));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refusal.SqlState);
    }

    [Fact]
    public async Task A_table_and_sequence_that_exist_when_the_script_runs_are_the_app_logins()
    {
        await PrepareOnceAsync();

        await using var connection = await OpenAsAppLoginAsync();
        await ExecuteAsync(connection, "INSERT INTO \"SerialBeforeGrant\" (\"Name\") VALUES ('probe')");
    }

    [Fact]
    public async Task A_table_and_sequence_the_administrator_creates_later_are_the_app_logins()
    {
        await PrepareOnceAsync();
        await ExecuteAsAdminAsync("CREATE TABLE \"SerialAfterGrant\" (\"Id\" bigserial PRIMARY KEY, \"Name\" text NOT NULL)");

        await using var connection = await OpenAsAppLoginAsync();
        await ExecuteAsync(connection, "INSERT INTO \"SerialAfterGrant\" (\"Name\") VALUES ('probe')");
    }

    [Fact]
    public void The_deploy_runs_the_script_after_the_migration_with_the_variables_it_reads()
    {
        var workflow = File.ReadAllText(RepoPath(".github", "workflows", "deploy-azure.yml")).ReplaceLineEndings("\n");

        var migration = workflow.IndexOf("./src/efbundle --connection", StringComparison.Ordinal);
        var grant = Regex.Match(
            workflow,
            @"- name: Grant the application login\n\s*if: env\.LEAST_PRIVILEGE_DB == 'true'\n(?:[^\n]*\n){1,8}?\s*" +
            @"-v ON_ERROR_STOP=1 -v app_login=""\$POSTGRES_APP_LOGIN"" -v app_password=""\$POSTGRES_APP_PASSWORD"" \\\n\s*" +
            @"-f deploy/db/grant-app-login\.sql\n");

        Assert.True(grant.Success, "deploy-azure.yml no longer runs grant-app-login.sql with app_login and app_password.");
        Assert.True(migration >= 0 && migration < grant.Index, "deploy-azure.yml must grant after the migration, which creates what the login is granted.");
    }

    private Task PrepareOnceAsync() => prepared ??= PrepareAsync();

    private async Task PrepareAsync()
    {
        await using (var superuser = new NpgsqlConnection(fixture.GetConnectionString()))
        {
            await superuser.OpenAsync();
            await ExecuteAsync(superuser, $"CREATE ROLE {Admin} LOGIN CREATEROLE CREATEDB PASSWORD '{AdminPassword}'");
            await ExecuteAsync(superuser, $"CREATE DATABASE {Database} OWNER {Admin}");
        }

        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(ConnectionString(Admin, AdminPassword), x => x.MigrationsAssembly(AssemblyReference.Assembly))
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;
        await using (var migrator = new CleansiaDbContext(options))
        {
            await migrator.Database.MigrateAsync();
        }

        await ExecuteAsAdminAsync("CREATE TABLE \"SerialBeforeGrant\" (\"Id\" bigserial PRIMARY KEY, \"Name\" text NOT NULL)");

        await RunGrantScriptAsync();
        await RunGrantScriptAsync();
    }

    private async Task RunGrantScriptAsync()
    {
        const string scriptPath = "/tmp/grant-app-login.sql";
        var script = (await File.ReadAllTextAsync(RepoPath("deploy", "db", "grant-app-login.sql"))).ReplaceLineEndings("\n");
        await fixture.CopyAsync(Encoding.UTF8.GetBytes(script), scriptPath);

        var result = await fixture.ExecAsync(
        [
            "psql", "--username", Admin, "--dbname", Database,
            "-v", "ON_ERROR_STOP=1", "-v", $"app_login={AppLogin}", "-v", $"app_password={AppPassword}",
            "-f", scriptPath,
        ]);

        Assert.True(result.ExitCode == 0, $"grant-app-login.sql failed ({result.ExitCode}): {result.Stderr}");
    }

    private string ConnectionString(string username, string password) =>
        new NpgsqlConnectionStringBuilder(fixture.GetConnectionString())
        {
            Database = Database,
            Username = username,
            Password = password,
            Pooling = false,
        }.ConnectionString;

    private async Task<NpgsqlConnection> OpenAsAppLoginAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString(AppLogin, AppPassword));
        await connection.OpenAsync();
        return connection;
    }

    private async Task ExecuteAsAdminAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString(Admin, AdminPassword));
        await connection.OpenAsync();
        await ExecuteAsync(connection, sql);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private CleansiaDbContext NewAppLoginContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(ConnectionString(AppLogin, AppPassword)).Options,
            SystemSession(),
            new FixedTenantProvider(TestTenants.Default));

    private static TestUserSessionProvider SystemSession() => new("system", "system@cleansia.test");

    private static string RepoPath(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !directory.EnumerateFiles("*.sln").Any())
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "Could not locate the solution directory from the test base directory.");

        var path = Path.GetFullPath(Path.Combine([directory!.FullName, "..", .. segments]));
        Assert.True(File.Exists(path), $"Expected deploy artifact not found: {path}");
        return path;
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
