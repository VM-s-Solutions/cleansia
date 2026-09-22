using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Infrastructure;

/// <summary>
/// Which stored text is "in force" for a market on a day: only documents whose effective date has
/// arrived, the latest of them, and a market's own copy ahead of the platform-wide one even when the
/// platform-wide one is newer. Run over the real model on SQLite in-memory so the ordering is the query's,
/// not a mock's.
/// </summary>
public sealed class LegalDocumentRepositoryInForceTests : IDisposable
{
    private const string Czechia = "country-cze";
    private const string Slovakia = "country-svk";
    private static readonly DateOnly Today = new(2026, 9, 14);

    private readonly SqliteConnection _connection;

    public LegalDocumentRepositoryInForceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var ctx = NewContext();
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options;
        return new CleansiaDbContext(options, new TestUserSessionProvider("system", "system@cleansia.test"), new DefaultTenantProvider());
    }

    private async Task SeedAsync(params LegalDocument[] documents)
    {
        await using var ctx = NewContext();
        ctx.Countries.AddRange(
            NewCountry(Czechia, "Czechia", "CZE", "CZ"),
            NewCountry(Slovakia, "Slovakia", "SVK", "SK"));
        ctx.LegalDocuments.AddRange(documents);
        await ctx.SaveChangesAsync();
    }

    private static Core.Domain.Internationalization.Country NewCountry(string id, string name, string iso3, string iso2)
    {
        var country = Core.Domain.Internationalization.Country.Create(name, iso3, iso2, isServiced: true);
        country.Id = id;
        country.Created("seed", DateTimeOffset.UtcNow);
        return country;
    }

    private static LegalDocument Terms(DateOnly effectiveFrom, string? countryId = null, string? note = null)
    {
        var document = LegalDocument.Create(LegalDocumentAudience.Customer, LegalDocumentType.TermsOfService, countryId, effectiveFrom, note);
        document.AddText("en", "Terms", $"Terms {countryId ?? "any"} {effectiveFrom:yyyy-MM-dd}");
        return document;
    }

    private async Task<LegalDocument?> InForceAsync(string? countryId, LegalDocumentType type = LegalDocumentType.TermsOfService, DateOnly? today = null)
    {
        await using var ctx = NewContext();
        return await new LegalDocumentRepository(ctx).GetInForceAsync(LegalDocumentAudience.Customer, type, countryId, today ?? Today, CancellationToken.None);
    }

    [Fact]
    public async Task The_Latest_Effective_Document_Wins_And_A_Future_One_Is_Ignored()
    {
        await SeedAsync(
            Terms(new DateOnly(2025, 1, 1), note: "older"),
            Terms(new DateOnly(2026, 9, 14), note: "current"),
            Terms(new DateOnly(2027, 1, 1), note: "future"));

        var inForce = await InForceAsync(Czechia);

        Assert.Equal("current", inForce!.Notes);
        Assert.Equal("2026-09-14", inForce.Version);
        Assert.Single(inForce.Texts);
    }

    [Fact]
    public async Task On_The_Effective_Date_Itself_The_Document_Is_Already_In_Force()
    {
        await SeedAsync(Terms(new DateOnly(2025, 1, 1), note: "older"), Terms(Today, note: "today"));

        Assert.Equal("today", (await InForceAsync(Czechia, today: Today))!.Notes);
        Assert.Equal("older", (await InForceAsync(Czechia, today: Today.AddDays(-1)))!.Notes);
    }

    [Fact]
    public async Task A_Markets_Own_Copy_Beats_A_Newer_Platform_Wide_One()
    {
        await SeedAsync(
            Terms(new DateOnly(2026, 9, 14), note: "platform"),
            Terms(new DateOnly(2026, 1, 1), Czechia, note: "czech"));

        Assert.Equal("czech", (await InForceAsync(Czechia))!.Notes);
        Assert.Equal("platform", (await InForceAsync(Slovakia))!.Notes);
        Assert.Equal("platform", (await InForceAsync(null))!.Notes);
    }

    [Fact]
    public async Task A_Markets_Future_Copy_Does_Not_Hide_The_Platform_Wide_One()
    {
        await SeedAsync(
            Terms(new DateOnly(2026, 9, 14), note: "platform"),
            Terms(new DateOnly(2027, 1, 1), Czechia, note: "czech-future"));

        Assert.Equal("platform", (await InForceAsync(Czechia))!.Notes);
    }

    [Fact]
    public async Task Type_And_Audience_Are_Part_Of_The_Identity()
    {
        var employee = LegalDocument.Create(LegalDocumentAudience.Employee, LegalDocumentType.TermsOfService, null, new DateOnly(2026, 1, 1), "employee");
        employee.AddText("en", "Employee terms", "Employee text");
        await SeedAsync(Terms(new DateOnly(2026, 1, 1), note: "customer"), employee);

        Assert.Equal("customer", (await InForceAsync(Czechia))!.Notes);
        Assert.Null(await InForceAsync(Czechia, LegalDocumentType.PrivacyPolicy));
    }

    [Fact]
    public async Task Nothing_Seeded_Is_Null()
    {
        await SeedAsync();

        Assert.Null(await InForceAsync(Czechia));
    }

    private sealed class DefaultTenantProvider : ITenantProvider
    {
        public string? GetCurrentTenantId() => TestTenants.Default;
        public void SetTenantOverride(string tenantId) { }
        public void ClearTenantOverride() { }
    }
}
