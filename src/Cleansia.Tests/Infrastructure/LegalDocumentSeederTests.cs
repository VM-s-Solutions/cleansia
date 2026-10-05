using System.Globalization;
using System.Text.RegularExpressions;
using Cleansia.Core.AppServices.Features.Legal;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Company;
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
        (LegalDocumentType.TermsOfService, new DateOnly(2026, 9, 30)),
        (LegalDocumentType.TermsOfService, new DateOnly(2026, 10, 3)),
        (LegalDocumentType.TermsOfService, new DateOnly(2026, 10, 5)),
        (LegalDocumentType.PrivacyPolicy, new DateOnly(2026, 9, 14)),
        (LegalDocumentType.PrivacyPolicy, new DateOnly(2026, 9, 29)),
        (LegalDocumentType.PrivacyPolicy, new DateOnly(2026, 10, 3)),
        (LegalDocumentType.WorkContract, new DateOnly(2026, 9, 20)),
        (LegalDocumentType.WorkContract, new DateOnly(2026, 9, 29)),
        (LegalDocumentType.WorkContract, new DateOnly(2026, 10, 5)),
        (LegalDocumentType.CleanerFrameworkContract, new DateOnly(2026, 9, 29)),
        (LegalDocumentType.CleanerFrameworkContract, new DateOnly(2026, 10, 5)),
        (LegalDocumentType.SelfBillingAgreement, new DateOnly(2026, 9, 29)),
        (LegalDocumentType.SelfBillingAgreement, new DateOnly(2026, 10, 5)),
        (LegalDocumentType.CleanerDataProcessingAgreement, new DateOnly(2026, 9, 29)),
        (LegalDocumentType.ComplaintsProcedure, new DateOnly(2026, 9, 29)),
    ];

    /// <summary>
    /// The contract for work binds the operating company and the cleaner (decision 45), and the cleaner's
    /// three documents are theirs: all four are employee texts; the rest are the customer's.
    /// </summary>
    private static LegalDocumentAudience AudienceOf(LegalDocumentType type) =>
        type == LegalDocumentType.WorkContract || LegalDocument.CleanerConsentTypeFor(type) is not null
            ? LegalDocumentAudience.Employee
            : LegalDocumentAudience.Customer;

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
    public async Task The_Embedded_Seed_Creates_Every_Document_Version_In_Five_Languages()
    {
        var outcome = await SeedAsync();

        Assert.Equal(SeededVersions.Length, outcome.AddedDocuments);
        var documents = await DocumentsAsync();
        Assert.Equal(
            SeededVersions.OrderBy(v => v.Type).ThenBy(v => v.EffectiveFrom),
            documents.Select(d => (d.Type, d.EffectiveFrom)).OrderBy(v => v.Type).ThenBy(v => v.EffectiveFrom));
        foreach (var document in documents)
        {
            Assert.Equal(AudienceOf(document.Type), document.Audience);
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
        Assert.All(documents.Where(d => d.Type == LegalDocumentType.WorkContract),
            d => Assert.Equal("Contract for Work", d.TextFor("en")!.Title));
    }

    // The contract is a template: it names the job by what the acceptance shows and binds the figures
    // through the frozen facts, never through a number, a name, an address or a date in the text.
    [Fact]
    public async Task The_Embedded_Work_Contract_Is_A_Template_With_The_Currency_Placeholder_And_No_Figure_In_Any_Language()
    {
        await SeedAsync();

        var contracts = (await DocumentsAsync()).Where(d => d.Type == LegalDocumentType.WorkContract).ToList();
        Assert.Equal(SeededVersions.Count(v => v.Type == LegalDocumentType.WorkContract), contracts.Count);
        foreach (var contract in contracts)
        {
            Assert.Equal(new[] { "cs", "en", "ru", "sk", "uk" }, contract.Texts.Select(t => t.Language).OrderBy(l => l));
            foreach (var text in contract.Texts)
            {
                Assert.Contains("{{currency}}", text.ContentMarkdown);
                Assert.DoesNotContain(text.ContentMarkdown, c => char.IsDigit(c));
                Assert.StartsWith("> ", text.ContentMarkdown);
            }
        }
    }

    /// <summary>
    /// The contract for work binds the operating company and the cleaner (decision 45): the newest text names
    /// the client — the company that operates the order — through the placeholders the partner read fills
    /// from its company record, in every language, beside the currency its reward is stated in. The
    /// 2026-09-20 text named the customer as the client and said Cleansia was not a party.
    /// </summary>
    [Fact]
    public void The_Newest_Work_Contract_Names_The_Operating_Company_As_The_Client_Through_Its_Placeholders()
    {
        var newest = NewestOf(LegalDocumentType.WorkContract);

        Assert.Equal(new[] { "cs", "en", "ru", "sk", "uk" }, newest.Select(r => r.Language).Order());
        Assert.All(newest, r =>
        {
            Assert.Equal(LegalDocumentAudience.Employee, r.Audience);
            Assert.Equal(
                new[]
                {
                    LegalMarkdownRenderer.CurrencyPlaceholder,
                    LegalMarkdownRenderer.CompanyLegalNamePlaceholder,
                    LegalMarkdownRenderer.CompanyRegistrationNumberPlaceholder,
                    LegalMarkdownRenderer.CompanySeatPlaceholder,
                }.Order(StringComparer.Ordinal),
                LegalMarkdownRenderer.PlaceholdersIn(r.ContentMarkdown).Order(StringComparer.Ordinal));
            Assert.DoesNotContain("Cleansia s.r.o.", r.ContentMarkdown);
        });
    }

    /// <summary>
    /// The cleaner's three documents (decision 47) are employee texts in five languages that name the market's
    /// operating company only through the placeholders the partner read fills from its company record — never
    /// the VAT number, which a company that is not a VAT payer, the launch state, does not hold, so its
    /// placeholder would reach the cleaner as braces — and carry no identity of their own. The framework
    /// contract also states the reward in the market's currency.
    /// </summary>
    [Theory]
    [InlineData(LegalDocumentType.CleanerFrameworkContract)]
    [InlineData(LegalDocumentType.SelfBillingAgreement)]
    [InlineData(LegalDocumentType.CleanerDataProcessingAgreement)]
    public void The_Cleaner_Documents_Name_The_Company_Only_Through_Its_Placeholders_In_Every_Language(LegalDocumentType type)
    {
        var newest = NewestOf(type);
        var expected = type == LegalDocumentType.CleanerFrameworkContract
            ? TermsPlaceholders
            : TermsPlaceholders.Where(p => p != LegalMarkdownRenderer.CurrencyPlaceholder).ToArray();

        Assert.Equal(new[] { "cs", "en", "ru", "sk", "uk" }, newest.Select(r => r.Language).Order());
        Assert.All(newest, r =>
        {
            Assert.Equal(LegalDocumentAudience.Employee, r.Audience);
            Assert.StartsWith("> ", r.ContentMarkdown);
            Assert.Equal(expected, LegalMarkdownRenderer.PlaceholdersIn(r.ContentMarkdown).Order(StringComparer.Ordinal));
            Assert.DoesNotContain("@cleansia", r.ContentMarkdown);
            Assert.DoesNotContain("+420", r.ContentMarkdown);
            Assert.DoesNotContain("s.r.o.", r.ContentMarkdown);
        });
    }

    /// <summary>
    /// The framework contract tells the cleaner how long to wait at a closed door before reporting a lockout —
    /// the wait the report is refused before — so moving that constant without publishing a new version of the
    /// contract fails here, in every language. It states no other minute figure.
    /// </summary>
    [Fact]
    public void The_Framework_Contract_States_The_Lockout_Wait_In_Every_Language()
    {
        var newest = NewestOf(LegalDocumentType.CleanerFrameworkContract);

        Assert.Equal(new[] { "cs", "en", "ru", "sk", "uk" }, newest.Select(r => r.Language).Order());
        Assert.All(newest, r => Assert.Equal(
            new[] { BookingPolicy.LockoutWaitMinutes },
            MinutePhrase.Matches(r.ContentMarkdown).Select(m => int.Parse(m.Groups[1].Value))));
    }

    private static List<LegalSeedResource> NewestOf(LegalDocumentType type) =>
        LegalSeedResource.ReadAll()
            .Where(r => r.Type == type)
            .GroupBy(r => r.EffectiveFrom)
            .MaxBy(g => g.Key)?
            .ToList() ?? [];

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
    /// The one address the privacy policy writes out is the data-protection contact
    /// (<see cref="PrivacyAddress"/>, owner decision 2026-10-03), which is not a field of the company record.
    /// Like the terms, neither names the VAT number: a company that is not a VAT payer, the launch state,
    /// holds none, so rendered from such a record the text must leave no placeholder behind.
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
        var nonPayer = LegalMarkdownRenderer.MarketPlaceholders(null, CompanyInfo.Create(
                legalName: "Seller Test a.s.", tradingName: "Seller", registrationNumber: "87654321",
                street: "Hlavná 1", city: "Bratislava", zipCode: "81101", countryId: Czechia,
                vatNumber: "SK2020123456", phone: "+421 900 000 000", email: "info@seller.test")
            .SetVatPayerStatus(false));

        Assert.Equal(new[] { "cs", "en", "ru", "sk", "uk" }, newest.Select(r => r.Language).Order());
        Assert.All(newest, r =>
        {
            Assert.Equal(CompanyPlaceholders, LegalMarkdownRenderer.PlaceholdersIn(r.ContentMarkdown).Order(StringComparer.Ordinal));
            Assert.DoesNotContain("@cleansia", r.ContentMarkdown.Replace(PrivacyAddress, string.Empty, StringComparison.Ordinal));
            Assert.DoesNotContain("+420", r.ContentMarkdown);
            Assert.DoesNotContain("s.r.o.", r.ContentMarkdown);

            var html = LegalMarkdownRenderer.Render(r.ContentMarkdown, nonPayer);
            Assert.Contains("Seller Test a.s.", html);
            Assert.DoesNotContain("{{", html);
        });
    }

    private static readonly string[] CompanyPlaceholders = TermsPlaceholders
        .Where(p => p != LegalMarkdownRenderer.CurrencyPlaceholder)
        .ToArray();

    private const string PrivacyAddress = "privacy@cleansia.cz";

    /// <summary>
    /// Owner decision 2026-10-03: questions about personal data go to the data-protection address, as the
    /// web privacy page and the partner GDPR copy already say. The newest privacy policy names it in both
    /// sentences that send the reader somewhere about their data — under the controller and under their
    /// rights — in every language, and names the company's e-mail only in the controller's identity line.
    /// </summary>
    [Fact]
    public void The_Newest_Privacy_Policy_Sends_Personal_Data_Questions_To_The_Privacy_Address()
    {
        var newest = NewestOf(LegalDocumentType.PrivacyPolicy);
        var companyEmail = "{{" + LegalMarkdownRenderer.CompanyEmailPlaceholder + "}}";

        Assert.Equal(new[] { "cs", "en", "ru", "sk", "uk" }, newest.Select(r => r.Language).Order());
        Assert.All(newest, r =>
        {
            Assert.Equal(2, Occurrences(r.ContentMarkdown, PrivacyAddress));
            var identity = Assert.Single(r.ContentMarkdown.Split('\n'), line => line.Contains(companyEmail, StringComparison.Ordinal));
            Assert.StartsWith("{{" + LegalMarkdownRenderer.CompanyLegalNamePlaceholder + "}}", identity);
        });
    }

    private static int Occurrences(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;

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

    /// <summary>
    /// Owner ruling 2026-09-30: Cleansia Plus has a 14-day free trial again, one per account, and the
    /// newest terms offer it in every language — as the only day figure in the Plus section, which used to
    /// say there was no free trial.
    /// </summary>
    [Fact]
    public void The_Newest_Terms_Offer_The_Plus_Free_Trial_In_Every_Language()
    {
        var newest = NewestTerms();

        Assert.Equal(new[] { "cs", "en", "ru", "sk", "uk" }, newest.Select(r => r.Language).Order());
        Assert.All(newest, r =>
        {
            var plusSection = r.ContentMarkdown.ReplaceLineEndings("\n")
                .Split("\n## ")
                .Single(section => section.StartsWith("10. Cleansia Plus"));
            Assert.Equal(
                new[] { PlusTrialDays },
                DayPhrase.Matches(plusSection).Select(m => int.Parse(m.Groups[1].Value)));
        });
    }

    private const int PlusTrialDays = 14;

    /// <summary>
    /// Owner rulings 2026-10-04: a referral earns both customers credit once the referred customer's first
    /// booking is completed within the referral window, counted from the code's acceptance, and a card refund
    /// is sent within 3 days. The newest terms state the window in the credit section, as the days the
    /// referral policy counts — once, so the window has one figure and one starting point — and the refund
    /// days in the cancellation and no-cleaner sections, each as the only day figure there, in every language.
    /// </summary>
    [Fact]
    public void The_Newest_Terms_State_The_Referral_Window_And_The_Refund_Days_In_Every_Language()
    {
        var newest = NewestTerms();

        Assert.Equal(new[] { "cs", "en", "ru", "sk", "uk" }, newest.Select(r => r.Language).Order());
        Assert.All(newest, r =>
        {
            Assert.Equal(new[] { ReferralPolicy.QualifyingWindowDays }, DaysIn(r.ContentMarkdown, section: 9));
            Assert.Equal(new[] { CardRefundDays }, DaysIn(r.ContentMarkdown, section: 13));
            Assert.Equal(new[] { CardRefundDays }, DaysIn(r.ContentMarkdown, section: 14));
        });
    }

    private const int CardRefundDays = 3;

    /// <summary>
    /// The owner's rulings of 2026-10-04 retire wording from the version each text replaces, and the newest
    /// version must carry none of it in any language. Each row names one block of the replaced version (its
    /// heading is block 0, then each paragraph or list) by the position of its section, which every language
    /// shares, and its comment says what that block said.
    /// </summary>
    [Theory]
    [InlineData(LegalDocumentType.TermsOfService, "2026-10-03", 7, 3)]            // cash needs a card saved as a guarantee
    [InlineData(LegalDocumentType.TermsOfService, "2026-10-03", 8, 0)]            // "The saved card for cash bookings"
    [InlineData(LegalDocumentType.TermsOfService, "2026-10-03", 8, 1)]            // the card may be charged without asking
    [InlineData(LegalDocumentType.TermsOfService, "2026-10-03", 8, 2)]            // a failed charge, and no cash without a card
    [InlineData(LegalDocumentType.TermsOfService, "2026-10-03", 13, 5)]           // refunds within 5 working days
    [InlineData(LegalDocumentType.TermsOfService, "2026-10-03", 14, 2)]           // a no-cleaner refund within 5 working days
    [InlineData(LegalDocumentType.CleanerFrameworkContract, "2026-09-29", 5, 2)]  // approval needs the insurance certificate
    [InlineData(LegalDocumentType.CleanerFrameworkContract, "2026-09-29", 8, 2)]  // cash held after a monthly settlement
    [InlineData(LegalDocumentType.CleanerFrameworkContract, "2026-09-29", 9, 1)]  // the reward without the extras
    [InlineData(LegalDocumentType.CleanerFrameworkContract, "2026-09-29", 9, 3)]  // a lockout pays half of the fee collected
    [InlineData(LegalDocumentType.CleanerFrameworkContract, "2026-09-29", 10, 0)] // "Monthly settlement"
    [InlineData(LegalDocumentType.CleanerFrameworkContract, "2026-09-29", 10, 1)] // settled after each monthly pay period
    [InlineData(LegalDocumentType.CleanerFrameworkContract, "2026-09-29", 11, 1)] // the claim as if every cleaner were insured
    [InlineData(LegalDocumentType.CleanerFrameworkContract, "2026-09-29", 11, 2)] // insurance kept, its certificate shown
    [InlineData(LegalDocumentType.CleanerFrameworkContract, "2026-09-29", 16, 2)] // losing the insurance ends the agreement at once
    [InlineData(LegalDocumentType.CleanerFrameworkContract, "2026-09-29", 16, 3)] // the last rewards at the next monthly settlement
    [InlineData(LegalDocumentType.SelfBillingAgreement, "2026-09-29", 2, 1)]      // invoices for completed jobs and any fee share
    [InlineData(LegalDocumentType.SelfBillingAgreement, "2026-09-29", 3, 1)]      // invoiced after each monthly pay period
    [InlineData(LegalDocumentType.SelfBillingAgreement, "2026-09-29", 3, 2)]      // the invoice lists any fee share
    [InlineData(LegalDocumentType.WorkContract, "2026-09-29", 4, 2)]              // paid on the monthly invoice
    [InlineData(LegalDocumentType.WorkContract, "2026-09-29", 7, 1)]              // a lockout pays a share of the fee
    public void The_Newest_Version_Carries_None_Of_The_Wording_The_2026_10_04_Rulings_Retired(
        LegalDocumentType type, string replaced, int section, int block)
    {
        var all = LegalSeedResource.ReadAll().Where(r => r.Type == type).ToList();
        var retired = all
            .Where(r => r.EffectiveFrom == DateOnly.Parse(replaced, CultureInfo.InvariantCulture))
            .ToDictionary(r => r.Language, r => BlocksOf(r.ContentMarkdown, section)[block]);
        var newest = NewestOf(type);

        Assert.Equal(new[] { "cs", "en", "ru", "sk", "uk" }, retired.Keys.Order());
        Assert.Equal(new[] { "cs", "en", "ru", "sk", "uk" }, newest.Select(r => r.Language).Order());
        Assert.All(newest, r => Assert.DoesNotContain(retired[r.Language], r.ContentMarkdown.ReplaceLineEndings("\n")));
    }

    /// <summary>
    /// The review of 2026-10-05 corrected the 2026-10-05 versions in place — allowed, because they are not in
    /// force yet — so that each states what the code does. The wording it replaced survives in no file, so each
    /// row quotes a phrase the section must now state in that language, and fails against the replaced wording.
    /// </summary>
    [Theory]
    // A cash booking is refused once the customer holds MaxOpenUnpaidCashBookings open and unpaid
    // (CustomerCashStanding): the two include the new one, as en, uk and ru already said.
    [InlineData(LegalDocumentType.TermsOfService, "cs", 7, "včetně této objednávky")]
    [InlineData(LegalDocumentType.TermsOfService, "sk", 7, "vrátane tejto objednávky")]
    // A code is accepted at registration (Register) or later on a booking (OrderLateReferralAcceptor) ...
    [InlineData(LegalDocumentType.TermsOfService, "en", 9, "or later on a booking")]
    [InlineData(LegalDocumentType.TermsOfService, "cs", 9, "nebo později u objednávky")]
    [InlineData(LegalDocumentType.TermsOfService, "sk", 9, "alebo neskôr pri objednávke")]
    [InlineData(LegalDocumentType.TermsOfService, "uk", 9, "або пізніше в замовленні")]
    [InlineData(LegalDocumentType.TermsOfService, "ru", 9, "или позже в заказе")]
    // ... and the window runs from Referral.AcceptedOn (ReferralService.ProcessOrderCompletedAsync), not registration ...
    [InlineData(LegalDocumentType.TermsOfService, "en", 9, "days of the code being accepted")]
    [InlineData(LegalDocumentType.TermsOfService, "cs", 9, "dní od přijetí kódu")]
    [InlineData(LegalDocumentType.TermsOfService, "sk", 9, "dní od prijatia kódu")]
    [InlineData(LegalDocumentType.TermsOfService, "uk", 9, "днів після прийняття коду")]
    [InlineData(LegalDocumentType.TermsOfService, "ru", 9, "дней после принятия кода")]
    // ... and a reversal takes back min(grant, balance) from each side (ReverseReferral), never more.
    [InlineData(LegalDocumentType.TermsOfService, "en", 9, "more than what is left of that credit on your balance")]
    [InlineData(LegalDocumentType.TermsOfService, "cs", 9, "víc, než kolik z tohoto kreditu zbývá na vašem zůstatku")]
    [InlineData(LegalDocumentType.TermsOfService, "sk", 9, "viac, ako koľko z tohto kreditu zostáva na vašom zostatku")]
    [InlineData(LegalDocumentType.TermsOfService, "uk", 9, "більше, ніж залишилося від цього кредиту на вашому залишку")]
    [InlineData(LegalDocumentType.TermsOfService, "ru", 9, "больше, чем осталось от этого кредита на вашем остатке")]
    public void The_Newest_Version_States_What_The_Code_Does_As_The_2026_10_05_Review_Worded_It(
        LegalDocumentType type, string language, int section, string phrase)
    {
        Assert.Contains(phrase, SectionOf(type, language, section));
    }

    /// <summary>
    /// The wording the review of 2026-10-05 retired must not come back: the cs and sk payment sections made
    /// "not more than two unpaid cash bookings" a precondition, which admits a third, after a stray "further".
    /// </summary>
    [Theory]
    [InlineData(LegalDocumentType.TermsOfService, "cs", 7, "nemáte více než dvě")]
    [InlineData(LegalDocumentType.TermsOfService, "cs", 7, "v hotovosti dále")]
    [InlineData(LegalDocumentType.TermsOfService, "sk", 7, "nemáte viac ako dve")]
    [InlineData(LegalDocumentType.TermsOfService, "sk", 7, "v hotovosti ďalej")]
    public void The_Newest_Version_Carries_None_Of_The_Wording_The_2026_10_05_Review_Retired(
        LegalDocumentType type, string language, int section, string phrase)
    {
        Assert.DoesNotContain(phrase, SectionOf(type, language, section));
    }

    private static string SectionOf(LegalDocumentType type, string language, int section) =>
        string.Join("\n\n", BlocksOf(NewestOf(type).Single(r => r.Language == language).ContentMarkdown, section));

    /// <summary>The blocks of the section at <paramref name="section"/>, counted by its <c>## </c> heading from 1.</summary>
    private static string[] BlocksOf(string markdown, int section) =>
        markdown.ReplaceLineEndings("\n").Split("\n## ")[section].Trim().Split("\n\n");

    private static int[] DaysIn(string markdown, int section) =>
        DayPhrase.Matches(string.Join("\n\n", BlocksOf(markdown, section)))
            .Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
            .ToArray();

    private static readonly Regex DayPhrase = new(@"(\d+)\s+(?:days|dní|днів|дні|дней)", RegexOptions.IgnoreCase);

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
        Assert.All(resources, r => Assert.Equal(AudienceOf(r.Type), r.Audience));
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
