using System.Text.RegularExpressions;
using Cleansia.Core.AppServices.Features.Legal;
using Cleansia.Core.AppServices.Features.Orders;
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

    /// <summary>Every document the embedded seed carries: a wording change is a new dated folder.</summary>
    private static readonly (LegalDocumentType Type, DateOnly EffectiveFrom)[] SeededVersions =
    [
        (LegalDocumentType.TermsOfService, new DateOnly(2026, 9, 14)),
        (LegalDocumentType.TermsOfService, new DateOnly(2026, 9, 27)),
        (LegalDocumentType.TermsOfService, new DateOnly(2026, 9, 29)),
        (LegalDocumentType.PrivacyPolicy, new DateOnly(2026, 9, 14)),
        (LegalDocumentType.PrivacyPolicy, new DateOnly(2026, 9, 29)),
        (LegalDocumentType.WorkContract, new DateOnly(2026, 9, 20)),
        (LegalDocumentType.ComplaintsProcedure, new DateOnly(2026, 9, 29)),
    ];

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
    public async Task The_Embedded_Seed_Creates_Every_Customer_Document_Version_In_Five_Languages()
    {
        var outcome = await SeedAsync();

        Assert.Equal(SeededVersions.Length, outcome.AddedDocuments);
        var documents = await DocumentsAsync();
        Assert.Equal(
            SeededVersions.OrderBy(v => v.Type).ThenBy(v => v.EffectiveFrom),
            documents.Select(d => (d.Type, d.EffectiveFrom)).OrderBy(v => v.Type).ThenBy(v => v.EffectiveFrom));
        foreach (var document in documents)
        {
            Assert.Equal(LegalDocumentAudience.Customer, document.Audience);
            Assert.Null(document.CountryId);
            Assert.Equal(LegalDocument.VersionFor(document.EffectiveFrom), document.Version);
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

        Assert.All(documents.Where(d => d.Type == LegalDocumentType.TermsOfService),
            d => Assert.Equal("Terms of Service", d.TextFor("en")!.Title));
        Assert.Equal("Contract for Work", documents.Single(d => d.Type == LegalDocumentType.WorkContract).TextFor("en")!.Title);
    }

    // The contract is a template: it names the job by what the acceptance shows and binds the figures
    // through the frozen facts, never through a number, a name, an address or a date in the text.
    [Fact]
    public async Task The_Embedded_Work_Contract_Is_A_Template_With_The_Currency_Placeholder_And_No_Figure_In_Any_Language()
    {
        await SeedAsync();

        var contract = (await DocumentsAsync()).Single(d => d.Type == LegalDocumentType.WorkContract);
        Assert.Equal(new[] { "cs", "en", "ru", "sk", "uk" }, contract.Texts.Select(t => t.Language).OrderBy(l => l));
        foreach (var text in contract.Texts)
        {
            Assert.Contains("{{currency}}", text.ContentMarkdown);
            Assert.DoesNotContain(text.ContentMarkdown, c => char.IsDigit(c));
            Assert.StartsWith("> ", text.ContentMarkdown);
        }
    }

    // The money figure in the terms comes from the market (ADR-0060): the seed carries the placeholder,
    // never a currency word, so the API fills it the way the locale copy was filled before. The privacy
    // policy states no price, so no version of it carries the currency.
    [Fact]
    public async Task The_Embedded_Terms_Carry_The_Currency_Placeholder_In_Every_Language_And_No_Privacy_Policy_Does()
    {
        await SeedAsync();

        var documents = await DocumentsAsync();
        var terms = documents.Where(d => d.Type == LegalDocumentType.TermsOfService).SelectMany(d => d.Texts).ToList();
        var privacy = documents.Where(d => d.Type == LegalDocumentType.PrivacyPolicy).SelectMany(d => d.Texts).ToList();

        Assert.Equal(SeededVersions.Count(v => v.Type == LegalDocumentType.TermsOfService) * 5, terms.Count);
        Assert.All(terms, t => Assert.Contains("{{currency}}", t.ContentMarkdown));
        Assert.Equal(SeededVersions.Count(v => v.Type == LegalDocumentType.PrivacyPolicy) * 5, privacy.Count);
        Assert.All(privacy, t => Assert.DoesNotContain("{{currency}}", t.ContentMarkdown));
    }

    /// <summary>
    /// The terms a customer accepts state the minutes the platform applies. Read off the minute phrases
    /// alone, in order — the Plus benefit, then the cancellation grace on a first booking before the
    /// standard one (owner ruling 2026-09-28), then the cleaner's wait before a lockout — so the hour
    /// figures elsewhere in the text cannot stand in for them, and moving, swapping or equalising any of
    /// these constants without publishing a new terms version fails here.
    /// </summary>
    [Fact]
    public void The_Newest_Terms_State_The_Cancellation_Grace_And_Lockout_Wait_Minutes_In_Every_Language()
    {
        var newest = NewestTerms();

        Assert.Equal(new[] { "cs", "en", "ru", "sk", "uk" }, newest.Select(r => r.Language).Order());
        Assert.All(newest, r => Assert.Equal(
            new[]
            {
                BookingPolicy.OopsWindowMinutesPlus,
                BookingPolicy.OopsWindowMinutesFirstBooking,
                BookingPolicy.OopsWindowMinutesStandard,
                BookingPolicy.LockoutWaitMinutes,
            },
            MinutePhrase.Matches(r.ContentMarkdown).Select(m => int.Parse(m.Groups[1].Value))));
    }

    /// <summary>
    /// The operating company sells in its own name (the 2026-09-27 ruling; decision 54): the newest terms
    /// name the seller through the placeholders the read path fills from the market operator's company
    /// record, in every language, and carry no identity of their own — the 2026-09-27 terms named nobody
    /// and hard-coded an e-mail and a phone. A placeholder outside this set would reach the customer as
    /// braces, because nothing fills it — which is why the VAT number is not in it: a company that is not a
    /// VAT payer, the launch state, holds none (its record clears it), and the price section states the VAT
    /// position instead.
    /// </summary>
    [Fact]
    public void The_Newest_Terms_Name_The_Seller_Only_Through_The_Company_Placeholders_In_Every_Language()
    {
        var newest = NewestTerms();

        Assert.Equal(new[] { "cs", "en", "ru", "sk", "uk" }, newest.Select(r => r.Language).Order());
        Assert.All(newest, r =>
        {
            Assert.Equal(TermsPlaceholders, LegalMarkdownRenderer.PlaceholdersIn(r.ContentMarkdown).Order(StringComparer.Ordinal));
            Assert.DoesNotContain("@cleansia", r.ContentMarkdown);
            Assert.DoesNotContain("+420", r.ContentMarkdown);
            Assert.DoesNotContain("s.r.o.", r.ContentMarkdown);
        });
    }

    private static readonly string[] TermsPlaceholders = new[]
    {
        LegalMarkdownRenderer.CurrencyPlaceholder,
        LegalMarkdownRenderer.CompanyLegalNamePlaceholder,
        LegalMarkdownRenderer.CompanyRegistrationNumberPlaceholder,
        LegalMarkdownRenderer.CompanySeatPlaceholder,
        LegalMarkdownRenderer.CompanyEmailPlaceholder,
        LegalMarkdownRenderer.CompanyPhonePlaceholder,
    }.Order(StringComparer.Ordinal).ToArray();

    /// <summary>
    /// The privacy policy names its controller and the complaints procedure the company that decides a
    /// complaint — the market's operating company, as the terms name the seller (decision 54) — through
    /// the placeholders the read path fills from its company record, in every language, and neither
    /// carries an identity of its own: the 2026-09-14 privacy policy hard-coded an e-mail and a phone.
    /// </summary>
    [Theory]
    [InlineData(LegalDocumentType.PrivacyPolicy)]
    [InlineData(LegalDocumentType.ComplaintsProcedure)]
    public void The_Newest_Privacy_Policy_And_Complaints_Procedure_Name_The_Company_Only_Through_Its_Placeholders(LegalDocumentType type)
    {
        var newest = LegalSeedResource.ReadAll()
            .Where(r => r.Type == type)
            .GroupBy(r => r.EffectiveFrom)
            .MaxBy(g => g.Key)?
            .ToList() ?? [];

        Assert.Equal(new[] { "cs", "en", "ru", "sk", "uk" }, newest.Select(r => r.Language).Order());
        Assert.All(newest, r =>
        {
            Assert.Equal(CompanyPlaceholders, LegalMarkdownRenderer.PlaceholdersIn(r.ContentMarkdown).Order(StringComparer.Ordinal));
            Assert.DoesNotContain("@cleansia", r.ContentMarkdown);
            Assert.DoesNotContain("+420", r.ContentMarkdown);
            Assert.DoesNotContain("s.r.o.", r.ContentMarkdown);
        });
    }

    private static readonly string[] CompanyPlaceholders = TermsPlaceholders
        .Where(p => p != LegalMarkdownRenderer.CurrencyPlaceholder)
        .Append(LegalMarkdownRenderer.CompanyVatNumberPlaceholder)
        .Order(StringComparer.Ordinal)
        .ToArray();

    /// <summary>
    /// The 2026-09-14 terms promised cash on delivery to everyone; the cash rule admits it only for a
    /// signed-in customer whose booking needs a single cleaner. That payment sentence must not come
    /// back in any language of the newest terms.
    /// </summary>
    [Fact]
    public void The_Newest_Terms_Do_Not_Carry_The_Unconditional_Cash_Promise_In_Any_Language()
    {
        var unconditional = LegalSeedResource.ReadAll()
            .Where(r => r.Type == LegalDocumentType.TermsOfService && r.EffectiveFrom == new DateOnly(2026, 9, 14))
            .ToDictionary(r => r.Language, r => r.ContentMarkdown
                .Split('\n')
                .Select(line => line.Trim())
                .Single(line => line.Contains("Stripe")));
        var newest = NewestTerms();

        Assert.Equal(unconditional.Keys.Order(), newest.Select(r => r.Language).Order());
        Assert.All(newest, r => Assert.DoesNotContain(unconditional[r.Language], r.ContentMarkdown));
    }

    private static readonly Regex MinutePhrase = new(@"(\d+)\s+(?:min|хвилин|минут)", RegexOptions.IgnoreCase);

    private static List<LegalSeedResource> NewestTerms() =>
        LegalSeedResource.ReadAll()
            .Where(r => r.Type == LegalDocumentType.TermsOfService)
            .GroupBy(r => r.EffectiveFrom)
            .MaxBy(g => g.Key)!
            .ToList();

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

    /// <summary>
    /// Owner ruling 2026-09-28: the cleaner's three documents are read from the employee folder, the complaints
    /// procedure from the customer one.
    /// </summary>
    [Theory]
    [InlineData("Seed/Legal/employee/framework-contract/any/2026-12-01/cs.md", LegalDocumentAudience.Employee, LegalDocumentType.CleanerFrameworkContract)]
    [InlineData("Seed/Legal/employee/self-billing-agreement/any/2026-12-01/cs.md", LegalDocumentAudience.Employee, LegalDocumentType.SelfBillingAgreement)]
    [InlineData("Seed/Legal/employee/data-processing-agreement/cze/2026-12-01/cs.md", LegalDocumentAudience.Employee, LegalDocumentType.CleanerDataProcessingAgreement)]
    [InlineData("Seed/Legal/customer/complaints-procedure/any/2026-12-01/cs.md", LegalDocumentAudience.Customer, LegalDocumentType.ComplaintsProcedure)]
    public void The_Cleaner_Documents_And_The_Complaints_Procedure_Have_Their_Folders(
        string logicalName, LegalDocumentAudience audience, LegalDocumentType type)
    {
        var resource = LegalSeedResource.Parse(logicalName, "---\ntitle: T\n---\nBody");

        Assert.Equal(audience, resource.Audience);
        Assert.Equal(type, resource.Type);
    }

    [Theory]
    [InlineData("Seed/Legal/customer/framework-contract/any/2026-12-01/cs.md")]
    [InlineData("Seed/Legal/customer/self-billing-agreement/any/2026-12-01/cs.md")]
    [InlineData("Seed/Legal/customer/data-processing-agreement/any/2026-12-01/cs.md")]
    public void A_Cleaner_Document_Under_The_Customer_Folder_Fails_Naming_Itself(string logicalName)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => LegalSeedResource.Parse(logicalName, "---\ntitle: T\n---\nBody"));

        Assert.Contains(logicalName, ex.Message);
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

        Assert.Equal(SeededVersions.Length * 5, resources.Count);
        Assert.All(resources, r => Assert.Equal(LegalDocumentAudience.Customer, r.Audience));
        Assert.All(resources, r => Assert.Null(r.CountryIsoCode));
        Assert.Equal(
            SeededVersions.OrderBy(v => v.Type).ThenBy(v => v.EffectiveFrom),
            resources.Select(r => (r.Type, r.EffectiveFrom)).Distinct().OrderBy(v => v.Type).ThenBy(v => v.EffectiveFrom));
    }

    private sealed class DefaultTenantProvider : ITenantProvider
    {
        public string? GetCurrentTenantId() => TestTenants.Default;
        public void SetTenantOverride(string tenantId) { }
        public void ClearTenantOverride() { }
    }
}
