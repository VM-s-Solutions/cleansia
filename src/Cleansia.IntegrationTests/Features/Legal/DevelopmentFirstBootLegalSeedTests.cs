using Cleansia.Config.Abstractions;
using Cleansia.Core.Domain.Legal;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Seed.Legal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Cleansia.IntegrationTests.Features.Legal;

/// <summary>
/// The first boot of a Development host against a database that does not exist yet. The hosted
/// services — the legal-document seed among them — start before the request pipeline is built, and it
/// is the pipeline's <c>MigrateDatabase</c> that creates the schema in Development, so the seed's own
/// bounded attempts all run against no <c>LegalDocuments</c> table; since a booking now refuses to form
/// without a work-contract text in force, that host could not book until its next restart.
/// <c>MigrateDatabase</c> therefore seeds the texts itself, right after migrating: one pass, on a fresh
/// database, leaves the three customer documents in force. Real Postgres, in its own database on the
/// shared container, so that the migration really runs from nothing.
/// </summary>
[Collection("PostgresCollection")]
public sealed class DevelopmentFirstBootLegalSeedTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private NpgsqlDataSource _dataSource = default!;

    public async Task InitializeAsync()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.GetConnectionString())
        {
            Database = "first_boot_legal_seed_test",
            Pooling = false,
        }.ConnectionString;

        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.EnableDynamicJson();
        builder.EnableUnmappedTypes();
        _dataSource = builder.Build();

        await using var bootstrap = new CleansiaDbContext(Options());
        await bootstrap.Database.EnsureDeletedAsync();
    }

    public async Task DisposeAsync()
    {
        await using (var ctx = new CleansiaDbContext(Options()))
        {
            await ctx.Database.EnsureDeletedAsync();
        }

        await _dataSource.DisposeAsync();
    }

    [Fact]
    public async Task On_A_Fresh_Development_Database_One_Boot_Pass_Migrates_And_Leaves_The_Legal_Texts_In_Force()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<CleansiaDbContext>(options => options
            .UseNpgsql(_dataSource)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));
        services.AddScoped<LegalDocumentSeeder>();
        await using var provider = services.BuildServiceProvider();

        await using (var before = new CleansiaDbContext(Options()))
        {
            Assert.False(await before.Database.CanConnectAsync(), "the database must not exist before the boot pass");
        }

        new ApplicationBuilder(provider).MigrateDatabase(new DevelopmentEnvironment());

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CleansiaDbContext>();
        Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync());

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var documents = await context.LegalDocuments.Include(d => d.Texts).AsNoTracking().ToListAsync();
        Assert.Equal(
            [LegalDocumentType.TermsOfService, LegalDocumentType.PrivacyPolicy, LegalDocumentType.WorkContract],
            documents.Select(d => d.Type).Order());
        Assert.All(documents, d => Assert.Equal(LegalDocumentAudience.Customer, d.Audience));
        Assert.All(documents, d => Assert.True(d.IsInForceOn(today), $"{d.Type} {d.Version} is not in force"));

        var contract = documents.Single(d => d.Type == LegalDocumentType.WorkContract);
        Assert.Equal(["cs", "en", "ru", "sk", "uk"], contract.Texts.Select(t => t.Language).Order(StringComparer.Ordinal));
    }

    private DbContextOptions<CleansiaDbContext> Options() =>
        new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(_dataSource).Options;

    private sealed class DevelopmentEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Cleansia.Web.Partner";
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
