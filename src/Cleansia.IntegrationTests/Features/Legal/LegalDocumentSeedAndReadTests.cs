using System.Security.Claims;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Legal;
using Cleansia.Core.AppServices.Features.Legal.DTOs;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.Legal;

/// <summary>
/// The legal texts on real Postgres, migration-built: the seed lands both customer documents in five
/// languages against the NULLS NOT DISTINCT identity index, a second run writes nothing, the anonymous
/// read serves the market's language with the market's currency in the copy (and English where the
/// language is unknown), and the admin catalogue lists what was seeded with its hashes.
/// </summary>
[Collection("PostgresCollection")]
public sealed class LegalDocumentSeedAndReadTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string Czechia = "country-cze-legal";
    private const string Slovakia = "country-svk-legal";
    private const string SlovakSeller = "Cleansia SK s.r.o.";

    private static Task Anonymous(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            new TestClaimsPrincipalUser(new ClaimsPrincipal(new ClaimsIdentity())))));
        return Task.CompletedTask;
    }

    private static Country NewCountry(string id, string name, string iso3, string iso2)
    {
        var country = Country.Create(name, iso3, iso2, isServiced: true);
        country.Id = id;
        return country;
    }

    private static async Task SeedMarketsAndTextsAsync(CleansiaDbContext context)
    {
        await LegalSeed.SeedAsync(context);
        context.Countries.AddRange(
            NewCountry(Czechia, "Czechia", "CZE", "CZ"),
            NewCountry(Slovakia, "Slovakia", "SVK", "SK"));
        context.CountryConfigurations.AddRange(
            CountryConfiguration.Create(Czechia, "CZK", "cs", 0.21m).AssignOperator(TestTenants.Default).SetAsDefaultMarket(true),
            CountryConfiguration.Create(Slovakia, "EUR", "sk", 0.20m).AssignOperator(TestTenants.Default));
        // The terms name the seller from the market operator's company record (decision 54). It is not a VAT
        // payer, the launch state, saved as the admin console saves one: the VAT number typed in is cleared.
        context.CompanyInfo.Add(CompanyInfo.Create(
                SlovakSeller, "Cleansia", "87654321", "Hlavná 1", "Bratislava", "81101", Slovakia,
                vatNumber: "SK2020123456", phone: "+421 900 000 000", email: "info@seller.test")
            .SetVatPayerStatus(false));
        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }

    private static Task<BusinessResult<LegalDocumentDto>> ReadAsync(IServiceProvider provider, LegalDocumentType type, string? countryId, string? language) =>
        provider.GetRequiredService<IMediator>().Send(new GetLegalDocument.Query(type, countryId, language));

    [Fact]
    public async Task The_Seed_Lands_Every_Document_Version_In_Five_Languages_And_A_Second_Run_Writes_Nothing()
    {
        await TestMethod(
            arrange: SeedMarketsAndTextsAsync,
            act: async provider => await LegalSeed.SeedAsync(provider.GetRequiredService<CleansiaDbContext>()),
            assert: async (context, second) =>
            {
                Assert.False(second.Changed);

                var documents = await context.LegalDocuments.Include(d => d.Texts).AsNoTracking().ToListAsync();
                Assert.Equal(18, documents.Count);
                Assert.All(documents, d => Assert.Null(d.CountryId));
                Assert.All(documents, d => Assert.Equal(LegalDocument.VersionFor(d.EffectiveFrom), d.Version));
                Assert.All(documents, d => Assert.Equal(5, d.Texts.Count));
                Assert.Equal(
                    [
                        LegalDocumentType.TermsOfService, LegalDocumentType.TermsOfService, LegalDocumentType.TermsOfService, LegalDocumentType.TermsOfService,
                        LegalDocumentType.TermsOfService, LegalDocumentType.TermsOfService,
                        LegalDocumentType.PrivacyPolicy, LegalDocumentType.PrivacyPolicy, LegalDocumentType.PrivacyPolicy,
                        LegalDocumentType.WorkContract, LegalDocumentType.WorkContract, LegalDocumentType.WorkContract,
                        LegalDocumentType.CleanerFrameworkContract, LegalDocumentType.CleanerFrameworkContract,
                        LegalDocumentType.SelfBillingAgreement, LegalDocumentType.SelfBillingAgreement,
                        LegalDocumentType.CleanerDataProcessingAgreement, LegalDocumentType.ComplaintsProcedure,
                    ],
                    documents.Select(d => d.Type).OrderBy(t => t));
                // The contract for work and the cleaner's three documents are employee texts (decisions 45 and 47).
                Assert.Equal(8, documents.Count(d => d.Audience == LegalDocumentAudience.Employee));
                Assert.Equal(90, await context.LegalDocumentTexts.CountAsync());
            },
            transactional: false);
    }

    /// <summary>
    /// The contract for work binds the operating company and the cleaner (decision 45), so it is seeded as
    /// an employee text and the customer's read of the legal texts does not serve it.
    /// </summary>
    [Fact]
    public async Task The_Anonymous_Read_Does_Not_Serve_The_Work_Contract()
    {
        await TestMethod(
            setup: Anonymous,
            arrange: SeedMarketsAndTextsAsync,
            act: async provider => await ReadAsync(provider, LegalDocumentType.WorkContract, Czechia, "cs"),
            assert: async (context, result) =>
            {
                Assert.False(result.IsSuccess);
                Assert.Equal(BusinessErrorMessage.LegalDocumentNotFound, result.Error!.Message);
                var seeded = await LegalSeed.PlatformWideAsync(context, LegalDocumentType.WorkContract, LegalDocumentAudience.Employee);
                Assert.Equal(LegalDocumentAudience.Employee, seeded.Audience);
            },
            transactional: false);
    }

    [Fact]
    public async Task The_Anonymous_Read_Serves_The_Named_Markets_Currency_In_The_Requested_Language()
    {
        await TestMethod(
            setup: Anonymous,
            arrange: SeedMarketsAndTextsAsync,
            act: async provider => await ReadAsync(provider, LegalDocumentType.TermsOfService, Slovakia, "sk"),
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                var dto = result.Value;
                var seeded = await LegalSeed.PlatformWideAsync(context, LegalDocumentType.TermsOfService);

                Assert.Equal(seeded.Version, dto.Version);
                Assert.Equal(seeded.EffectiveFrom, dto.EffectiveFrom);
                Assert.Equal("sk", dto.Language);
                Assert.Equal(seeded.TextFor("sk")!.Title, dto.Title);
                Assert.Equal(seeded.TextFor("sk")!.ContentHash, dto.ContentHash);
                Assert.Contains(" EUR ", dto.ContentHtml);
                Assert.Contains(SlovakSeller, dto.ContentHtml);
                Assert.Contains("87654321", dto.ContentHtml);
                Assert.DoesNotContain("{{", dto.ContentHtml);
                Assert.Contains("<h2>", dto.ContentHtml);
                Assert.Contains("<blockquote>", dto.ContentHtml);
            },
            transactional: false);
    }

    [Fact]
    public async Task No_Market_Named_Reads_The_Default_Market_And_An_Unknown_Language_Falls_Back_To_English()
    {
        await TestMethod(
            setup: Anonymous,
            arrange: SeedMarketsAndTextsAsync,
            act: async provider => await ReadAsync(provider, LegalDocumentType.PrivacyPolicy, null, "de"),
            assert: (_, result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                Assert.Equal("en", result.Value.Language);
                Assert.Equal("Privacy Policy", result.Value.Title);
                Assert.Equal(LegalDocumentType.PrivacyPolicy, result.Value.Type);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Country_That_Is_Not_A_Market_Is_Refused_Not_Served()
    {
        await TestMethod(
            setup: Anonymous,
            arrange: SeedMarketsAndTextsAsync,
            act: async provider => await ReadAsync(provider, LegalDocumentType.TermsOfService, "country-nowhere", "en"),
            assert: (_, result) =>
            {
                Assert.True(result.IsFailure);
                Assert.Equal(BusinessErrorMessage.CountryNotServiced, result.Error!.Message);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task The_Admin_Catalogue_Lists_The_Seeded_Versions_With_Their_Languages_And_Hashes_And_Serves_One_Text()
    {
        await TestMethod(
            arrange: SeedMarketsAndTextsAsync,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var versions = await mediator.Send(new AdminGetLegalVersions.Query(Type: LegalDocumentType.TermsOfService));
                var terms = versions.Value.First();
                var czech = await mediator.Send(new AdminGetLegalDocument.Query(terms.Id, "cs"));
                var missing = await mediator.Send(new AdminGetLegalDocument.Query("01ARZ3NDEKTSV4RRFFQ69G5FAV", "cs"));
                return (terms, czech, missing);
            },
            assert: async (context, tuple) =>
            {
                var (terms, czech, missing) = tuple;
                var seeded = await LegalSeed.PlatformWideAsync(context, LegalDocumentType.TermsOfService);

                Assert.Equal(seeded.Id, terms.Id);
                Assert.True(terms.IsInForce);
                Assert.Null(terms.CountryIsoCode);
                Assert.Equal(new[] { "cs", "en", "ru", "sk", "uk" }, terms.Texts.Select(t => t.Language));
                Assert.Equal(seeded.TextFor("en")!.ContentHash, terms.Texts.Single(t => t.Language == "en").ContentHash);

                Assert.True(czech.IsSuccess, czech.Error?.Message);
                Assert.Equal("cs", czech.Value.Language);
                Assert.Equal(seeded.TextFor("cs")!.ContentMarkdown, czech.Value.ContentMarkdown);
                Assert.Contains("{{currency}}", czech.Value.ContentHtml);

                Assert.True(missing.IsFailure);
                var refusal = Assert.IsAssignableFrom<IValidationResult>(missing);
                Assert.Equal(BusinessErrorMessage.LegalDocumentNotFound, Assert.Single(refusal.Errors).Message);
            },
            transactional: false);
    }

    [Fact]
    public async Task The_Admin_Catalogue_Marks_Only_The_Newest_Past_Version_Of_A_Group_In_Force()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var older = MarketTerms(today.AddYears(-2));
        var newer = MarketTerms(today.AddYears(-1));
        var future = MarketTerms(today.AddDays(30));

        await TestMethod(
            arrange: async context =>
            {
                await SeedMarketsAndTextsAsync(context);
                context.LegalDocuments.AddRange(older, newer, future);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var versions = await mediator.Send(new AdminGetLegalVersions.Query(
                    LegalDocumentAudience.Customer, LegalDocumentType.TermsOfService, Czechia));
                var single = new Dictionary<string, bool>();
                foreach (var document in new[] { older, newer, future })
                {
                    single[document.Id] = (await mediator.Send(new AdminGetLegalDocument.Query(document.Id, "en"))).Value.IsInForce;
                }
                return (versions.Value, single);
            },
            assert: (_, tuple) =>
            {
                var (versions, single) = tuple;
                var inForce = versions.ToDictionary(v => v.Id, v => v.IsInForce);

                Assert.Equal(3, inForce.Count);
                Assert.True(inForce[newer.Id]);
                Assert.False(inForce[older.Id]);
                Assert.False(inForce[future.Id]);
                Assert.Equal(inForce, single);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    private static LegalDocument MarketTerms(DateOnly effectiveFrom)
    {
        var document = LegalDocument.Create(
            LegalDocumentAudience.Customer, LegalDocumentType.TermsOfService, Czechia, effectiveFrom);
        document.AddText("en", "Terms of Service", "Terms effective " + LegalDocument.VersionFor(effectiveFrom));
        return document;
    }
}
