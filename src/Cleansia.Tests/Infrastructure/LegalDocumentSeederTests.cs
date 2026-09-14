using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Seed.Legal;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cleansia.Tests.Infrastructure;

/// <summary>
/// The seeder is how a legal text reaches the database, and the one rule it enforces is the owner's:
/// a document in force is immutable — a changed file under a date that has passed is logged and
/// skipped, never applied — while a document not yet in force may still be corrected. Idempotent by
/// (audience, type, country, effective date, language) plus the content hash, so every host start
/// runs it and a re-run writes nothing.
/// </summary>
public sealed class LegalDocumentSeederTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 9, 14);
    private const string Czechia = "country-cze";

    private readonly SqliteConnection _connection;

    public LegalDocumentSeederTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var ctx = NewContext();
        ctx.Database.EnsureCreated();
        var czechia = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        czechia.Id = Czechia;
        czechia.Created("seed", DateTimeOffset.UtcNow);
        ctx.Countries.Add(czechia);
        ctx.SaveChanges();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options;
        return new CleansiaDbContext(options, new TestUserSessionProvider("system", "system@cleansia.test"), new DefaultTenantProvider());
    }

    private async Task<LegalSeedOutcome> SeedAsync(IReadOnlyList<LegalSeedResource>? resources = null, DateOnly? today = null)
    {
        await using var ctx = NewContext();
        var seeder = new LegalDocumentSeeder(ctx, NullLogger<LegalDocumentSeeder>.Instance);
        return resources is null
            ? await seeder.SeedAsync(today ?? Today, CancellationToken.None)
            : await seeder.SeedAsync(resources, today ?? Today, CancellationToken.None);
    }

    private async Task<List<LegalDocument>> DocumentsAsync()
    {
        await using var ctx = NewContext();
        return await ctx.LegalDocuments.Include(d => d.Texts).AsNoTracking().OrderBy(d => d.Type).ToListAsync();
    }

    private static LegalSeedResource Resource(
        LegalDocumentType type = LegalDocumentType.TermsOfService,
        string? countryIso = null,
        DateOnly? effectiveFrom = null,
        string language = "en",
        string title = "Terms of Service",
        string body = "## One\n\nThe text.") =>
        new(LegalDocumentAudience.Customer, type, countryIso, effectiveFrom ?? Today, language, title, body);

    [Fact]
    public async Task The_Embedded_Seed_Creates_Both_Customer_Documents_In_Five_Languages_Effective_Today()
    {
        var outcome = await SeedAsync();

        Assert.Equal(2, outcome.AddedDocuments);
        var documents = await DocumentsAsync();
        Assert.Equal(2, documents.Count);
        foreach (var document in documents)
        {
            Assert.Equal(LegalDocumentAudience.Customer, document.Audience);
            Assert.Null(document.CountryId);
            Assert.Equal(new DateOnly(2026, 9, 14), document.EffectiveFrom);
            Assert.Equal("2026-09-14", document.Version);
            Assert.Equal(new[] { "cs", "en", "ru", "sk", "uk" }, document.Texts.Select(t => t.Language).OrderBy(l => l));
            foreach (var text in document.Texts)
            {
                Assert.Equal(LegalDocumentText.HashOf(text.ContentMarkdown), text.ContentHash);
                Assert.DoesNotContain("\r", text.ContentMarkdown);
                Assert.DoesNotContain("---", text.ContentMarkdown[..3]);
                Assert.Contains("## ", text.ContentMarkdown);
                Assert.False(string.IsNullOrWhiteSpace(text.Title));
            }
        }

        Assert.Contains(documents, d => d.Type == LegalDocumentType.TermsOfService);
        Assert.Contains(documents, d => d.Type == LegalDocumentType.PrivacyPolicy);
        Assert.Equal("Terms of Service", documents.Single(d => d.Type == LegalDocumentType.TermsOfService).TextFor("en")!.Title);
    }

    // The money figure in the terms comes from the market (ADR-0060): the seed carries the placeholder,
    // never a currency word, so the API fills it the way the locale copy was filled before.
    [Fact]
    public async Task The_Embedded_Terms_Carry_The_Currency_Placeholder_In_Every_Language_And_The_Privacy_Policy_None()
    {
        await SeedAsync();

        var documents = await DocumentsAsync();
        var terms = documents.Single(d => d.Type == LegalDocumentType.TermsOfService);
        var privacy = documents.Single(d => d.Type == LegalDocumentType.PrivacyPolicy);

        Assert.All(terms.Texts, t => Assert.Contains("{{currency}}", t.ContentMarkdown));
        Assert.All(privacy.Texts, t => Assert.DoesNotContain("{{", t.ContentMarkdown));
    }

    [Fact]
    public async Task A_Second_Run_Writes_Nothing()
    {
        await SeedAsync();
        var before = (await DocumentsAsync()).SelectMany(d => d.Texts).Select(t => (t.Id, t.ContentHash)).OrderBy(x => x.Id).ToList();

        var outcome = await SeedAsync();

        Assert.False(outcome.Changed);
        var after = (await DocumentsAsync()).SelectMany(d => d.Texts).Select(t => (t.Id, t.ContentHash)).OrderBy(x => x.Id).ToList();
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task A_Changed_Text_Under_A_Date_In_Force_Is_Skipped_And_The_Stored_Text_Stays()
    {
        await SeedAsync([Resource(body: "## One\n\nThe text.")]);

        var outcome = await SeedAsync([Resource(body: "## One\n\nThe text, reworded.")]);

        Assert.Equal(1, outcome.SkippedImmutable);
        Assert.Equal(0, outcome.UpdatedTexts);
        var text = Assert.Single(Assert.Single(await DocumentsAsync()).Texts);
        Assert.Equal("## One\n\nThe text.", text.ContentMarkdown);
    }

    [Fact]
    public async Task A_Changed_Title_Under_A_Date_In_Force_Is_Skipped_Too()
    {
        await SeedAsync([Resource(title: "Terms of Service")]);

        var outcome = await SeedAsync([Resource(title: "Terms and Conditions")]);

        Assert.Equal(1, outcome.SkippedImmutable);
        Assert.Equal("Terms of Service", Assert.Single(Assert.Single(await DocumentsAsync()).Texts).Title);
    }

    [Fact]
    public async Task A_Changed_Text_Under_A_Future_Date_Is_Applied()
    {
        var tomorrow = Today.AddDays(1);
        await SeedAsync([Resource(effectiveFrom: tomorrow, body: "Draft one.")]);

        var outcome = await SeedAsync([Resource(effectiveFrom: tomorrow, body: "Draft two.")]);

        Assert.Equal(1, outcome.UpdatedTexts);
        var text = Assert.Single(Assert.Single(await DocumentsAsync()).Texts);
        Assert.Equal("Draft two.", text.ContentMarkdown);
        Assert.Equal(LegalDocumentText.HashOf("Draft two."), text.ContentHash);
    }

    [Fact]
    public async Task A_New_Language_Is_Added_To_A_Future_Document_And_Refused_On_One_In_Force()
    {
        var tomorrow = Today.AddDays(1);
        await SeedAsync([Resource(effectiveFrom: tomorrow), Resource(effectiveFrom: Today)]);

        var outcome = await SeedAsync([
            Resource(effectiveFrom: tomorrow), Resource(effectiveFrom: tomorrow, language: "cs", title: "Podmínky"),
            Resource(effectiveFrom: Today), Resource(effectiveFrom: Today, language: "cs", title: "Podmínky")]);

        Assert.Equal(1, outcome.AddedTexts);
        Assert.Equal(1, outcome.SkippedImmutable);
        var documents = await DocumentsAsync();
        Assert.Equal(2, documents.Single(d => d.EffectiveFrom == tomorrow).Texts.Count);
        Assert.Single(documents.Single(d => d.EffectiveFrom == Today).Texts);
    }

    [Fact]
    public async Task A_New_Effective_Date_Is_A_New_Document_Beside_The_Old_One()
    {
        await SeedAsync([Resource(effectiveFrom: new DateOnly(2025, 1, 1))]);

        var outcome = await SeedAsync([Resource(effectiveFrom: new DateOnly(2025, 1, 1)), Resource(effectiveFrom: Today, body: "New wording.")]);

        Assert.Equal(1, outcome.AddedDocuments);
        var documents = await DocumentsAsync();
        Assert.Equal(2, documents.Count);
        Assert.Equal(new[] { "2025-01-01", "2026-09-14" }, documents.Select(d => d.Version).OrderBy(v => v));
    }

    [Fact]
    public async Task A_Country_Folder_Resolves_To_The_Catalogue_Country_And_An_Unknown_One_Is_Skipped()
    {
        var outcome = await SeedAsync([Resource(countryIso: "CZE"), Resource(countryIso: "XXX")]);

        Assert.Equal(1, outcome.AddedDocuments);
        Assert.Equal(1, outcome.SkippedUnknownCountry);
        Assert.Equal(Czechia, Assert.Single(await DocumentsAsync()).CountryId);
    }

    [Fact]
    public void A_Seed_File_Is_Identified_By_Its_Path_And_Titled_By_Its_Front_Matter()
    {
        var resource = LegalSeedResource.Parse(
            @"Seed/Legal/customer\privacy-policy\cze\2026-09-14\Cs.md",
            "---\r\ntitle: Ochrana osobních údajů\r\n---\r\n\r\n> Návrh\r\n\r\n## Jedna\r\n\r\nText.\r\n");

        Assert.Equal(LegalDocumentAudience.Customer, resource.Audience);
        Assert.Equal(LegalDocumentType.PrivacyPolicy, resource.Type);
        Assert.Equal("CZE", resource.CountryIsoCode);
        Assert.Equal(new DateOnly(2026, 9, 14), resource.EffectiveFrom);
        Assert.Equal("cs", resource.Language);
        Assert.Equal("Ochrana osobních údajů", resource.Title);
        Assert.Equal("> Návrh\n\n## Jedna\n\nText.", resource.ContentMarkdown);
    }

    [Fact]
    public void The_Any_Folder_Is_The_Platform_Wide_Text()
    {
        var resource = LegalSeedResource.Parse("Seed/Legal/employee/terms-of-service/any/2027-01-01/en.md", "---\ntitle: T\n---\nBody");

        Assert.Null(resource.CountryIsoCode);
        Assert.Equal(LegalDocumentAudience.Employee, resource.Audience);
    }

    [Theory]
    [InlineData("Seed/Legal/customer/terms-of-service/any/2026-09-14/en.md", "no front matter")]
    [InlineData("Seed/Legal/customer/terms-of-service/any/2026-09-14/en.md", "---\nsubtitle: x\n---\nBody")]
    [InlineData("Seed/Legal/customer/terms-of-service/any/2026-09-14/en.md", "---\ntitle: x\n---\n")]
    [InlineData("Seed/Legal/customer/terms-of-service/any/2026-09-14/en.md", "---\ntitle: x\nBody")]
    [InlineData("Seed/Legal/customer/terms-of-service/any/14-09-2026/en.md", "---\ntitle: x\n---\nBody")]
    [InlineData("Seed/Legal/customer/cookies/any/2026-09-14/en.md", "---\ntitle: x\n---\nBody")]
    [InlineData("Seed/Legal/visitor/terms-of-service/any/2026-09-14/en.md", "---\ntitle: x\n---\nBody")]
    [InlineData("Seed/Legal/customer/terms-of-service/any/2026-09-14/english.md", "---\ntitle: x\n---\nBody")]
    [InlineData("Seed/Legal/customer/terms-of-service/2026-09-14/en.md", "---\ntitle: x\n---\nBody")]
    public void A_Malformed_Seed_File_Fails_Naming_Itself(string logicalName, string content)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => LegalSeedResource.Parse(logicalName, content));

        Assert.Contains(logicalName, ex.Message);
    }

    [Fact]
    public void Every_Embedded_Seed_File_Parses()
    {
        var resources = LegalSeedResource.ReadAll();

        Assert.Equal(10, resources.Count);
        Assert.All(resources, r => Assert.Equal(LegalDocumentAudience.Customer, r.Audience));
        Assert.All(resources, r => Assert.Null(r.CountryIsoCode));
        Assert.All(resources, r => Assert.Equal(new DateOnly(2026, 9, 14), r.EffectiveFrom));
    }

    private sealed class DefaultTenantProvider : ITenantProvider
    {
        public string? GetCurrentTenantId() => TestTenants.Default;
        public void SetTenantOverride(string tenantId) { }
        public void ClearTenantOverride() { }
    }
}
