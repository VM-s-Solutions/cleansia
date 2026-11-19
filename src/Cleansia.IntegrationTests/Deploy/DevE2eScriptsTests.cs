using Cleansia.Config.Abstractions;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.IntegrationTests.Features.Legal;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using DbAssemblyReference = Cleansia.Infra.Database.AssemblyReference;

namespace Cleansia.IntegrationTests.Deploy;

/// <summary>
/// The sql-scripts/dev-e2e-*.sql scripts run only through execute-sql.yml against DEV, so before this test the
/// first database any of them met was DEV's, and a script that did not parse failed in the middle of an E2E run.
/// Each one runs here as a dispatch runs it — a fresh session that first sets <c>cleansia.confirm</c> to the
/// dispatch's <c>confirm</c> input, then the whole file — against a throwaway database built from the migration and
/// seeded as DEV is (the Development seed scripts, the published administrator, the legal seed), so the scripts'
/// own COMMITs touch nothing else.
/// </summary>
[Collection("PostgresCollection")]
public sealed class DevE2eScriptsTests : IAsyncLifetime
{
    private const string PublishScript = "dev-e2e-publish-cleaner-document.sql";

    private static readonly int[] CleanerDocumentTypes =
    [
        (int)LegalDocumentType.CleanerFrameworkContract,
        (int)LegalDocumentType.SelfBillingAgreement,
        (int)LegalDocumentType.CleanerDataProcessingAgreement,
    ];

    private readonly PostgresContainerFixture _fixture;
    private NpgsqlDataSource _dataSource = default!;
    private string _connectionString = default!;

    public DevE2eScriptsTests(PostgresContainerFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _connectionString = new NpgsqlConnectionStringBuilder(_fixture.GetConnectionString())
        {
            Database = "dev_e2e_scripts_test",
            Pooling = false,
        }.ConnectionString;

        var builder = new NpgsqlDataSourceBuilder(_connectionString);
        builder.EnableDynamicJson();
        builder.EnableUnmappedTypes();
        _dataSource = builder.Build();

        await using (var bootstrap = NewContext())
        {
            await bootstrap.Database.EnsureDeletedAsync();
            await bootstrap.Database.MigrateAsync();
        }

        await using (var conn = await _dataSource.OpenConnectionAsync())
        {
            await conn.ReloadTypesAsync();
            await using var seed = conn.CreateCommand();
            seed.CommandText = string.Join('\n', DatabaseMigrationExtensions.DevelopmentSeedScripts.Select(ReadScript));
            seed.CommandTimeout = 120;
            await seed.ExecuteNonQueryAsync();
        }

        await using var context = NewContext();
        await LegalSeed.SeedAsync(context);
    }

    public async Task DisposeAsync()
    {
        await using (var ctx = NewContext())
        {
            await ctx.Database.EnsureDeletedAsync();
        }

        await _dataSource.DisposeAsync();
    }

    [Fact]
    public async Task Every_dev_e2e_script_runs_on_a_migrated_dev_database()
    {
        var scripts = Directory.GetFiles(SqlScriptsDirectory(), "dev-e2e-*.sql").Select(Path.GetFileName).Order().ToList();
        Assert.Contains(PublishScript, scripts);
        Assert.True(scripts.Count >= 4, $"Only {scripts.Count} dev-e2e scripts were found.");

        var failures = new List<string>();
        foreach (var script in scripts)
        {
            try
            {
                await DispatchAsync(script!, confirm: script!);
            }
            catch (PostgresException ex)
            {
                failures.Add($"{script}: {ex.SqlState} {ex.MessageText} (at character {ex.Position} of its statement)");
            }
        }

        Assert.True(failures.Count == 0, "These dev-e2e scripts fail on a migrated DEV database:\n  " + string.Join("\n  ", failures));
    }

    [Fact]
    public async Task The_publish_script_publishes_each_cleaner_document_once_and_a_second_run_changes_nothing()
    {
        var before = await ScalarAsync<long>("""SELECT count(*) FROM "LegalDocuments" """);

        await DispatchAsync(PublishScript, confirm: PublishScript);
        var published = await PublishedTodayAsync();
        var afterFirst = await ScalarAsync<long>("""SELECT count(*) FROM "LegalDocuments" """);

        await DispatchAsync(PublishScript, confirm: PublishScript);

        Assert.Equal(CleanerDocumentTypes, published.Select(p => p.Type).Order().ToArray());
        Assert.All(published, p =>
        {
            Assert.Matches("^[0-9A-HJKMNP-TV-Z]{26}$", p.Id);
            Assert.Equal(p.Today, p.Version);
            Assert.True(p.Texts > 0 && p.Texts == p.SourceTexts, $"type {p.Type}: {p.Texts} texts copied of {p.SourceTexts}");
        });
        Assert.Equal(before + CleanerDocumentTypes.Length, afterFirst);
        Assert.Equal(afterFirst, await ScalarAsync<long>("""SELECT count(*) FROM "LegalDocuments" """));
        Assert.Equal(published, await PublishedTodayAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("execute")]
    [InlineData("dev-e2e-confirm-accounts.sql")]
    public async Task The_publish_script_refuses_a_dispatch_that_does_not_name_it_as_its_confirmation(string confirm)
    {
        var before = await ScalarAsync<long>("""SELECT count(*) FROM "LegalDocuments" """);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => DispatchAsync(PublishScript, confirm));

        Assert.Contains("confirm", refused.MessageText);
        Assert.Equal(before, await ScalarAsync<long>("""SELECT count(*) FROM "LegalDocuments" """));
    }

    /// <summary>What execute-sql.yml's DEV job does: one new session, the confirmation, then the file.</summary>
    private async Task DispatchAsync(string script, string confirm)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using (var set = new NpgsqlCommand($"SET cleansia.confirm = '{confirm}'", conn))
        {
            await set.ExecuteNonQueryAsync();
        }

        await using var run = new NpgsqlCommand(ReadScript(script), conn) { CommandTimeout = 120 };
        await using var reader = await run.ExecuteReaderAsync();
        do
        {
            while (await reader.ReadAsync())
            {
            }
        }
        while (await reader.NextResultAsync());
    }

    private async Task<List<Published>> PublishedTodayAsync()
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT d."Id", d."Type", d."Version", to_char((now() AT TIME ZONE 'UTC')::date, 'YYYY-MM-DD'),
                   (SELECT count(*) FROM "LegalDocumentTexts" t WHERE t."LegalDocumentId" = d."Id"),
                   (SELECT count(*) FROM "LegalDocumentTexts" t
                    WHERE t."LegalDocumentId" = (SELECT s."Id" FROM "LegalDocuments" s
                                                 WHERE s."Audience" = d."Audience" AND s."Type" = d."Type"
                                                   AND s."EffectiveFrom" < d."EffectiveFrom"
                                                 ORDER BY s."EffectiveFrom" DESC LIMIT 1))
            FROM "LegalDocuments" d
            WHERE d."Audience" = 1 AND d."EffectiveFrom" = (now() AT TIME ZONE 'UTC')::date
            ORDER BY d."Type"
            """,
            conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        var rows = new List<Published>();
        while (await reader.ReadAsync())
        {
            rows.Add(new Published(reader.GetString(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetInt64(4), reader.GetInt64(5)));
        }

        return rows;
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        return (T)(await cmd.ExecuteScalarAsync())!;
    }

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>()
                .UseNpgsql(_dataSource, x => x.MigrationsAssembly(DbAssemblyReference.Assembly))
                .Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(TestTenants.Default));

    private static string ReadScript(string script) => File.ReadAllText(Path.Combine(SqlScriptsDirectory(), script));

    private static string SqlScriptsDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.GetFiles("*.sln").Length == 0)
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, "Could not locate the solution directory from the test base directory.");
        return Path.GetFullPath(Path.Combine(dir!.FullName, "..", "sql-scripts"));
    }

    private sealed record Published(string Id, int Type, string Version, string Today, long Texts, long SourceTexts);

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
