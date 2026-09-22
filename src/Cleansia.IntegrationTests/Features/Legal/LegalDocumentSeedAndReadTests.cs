using System.Security.Claims;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Legal;
using Cleansia.Core.AppServices.Features.Legal.DTOs;
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
        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }

    private static Task<BusinessResult<LegalDocumentDto>> ReadAsync(IServiceProvider provider, LegalDocumentType type, string? countryId, string? language) =>
        provider.GetRequiredService<IMediator>().Send(new GetLegalDocument.Query(type, countryId, language));

    [Fact]
    public async Task The_Seed_Lands_The_Three_Documents_In_Five_Languages_And_A_Second_Run_Writes_Nothing()
    {
        await TestMethod(
            arrange: SeedMarketsAndTextsAsync,
            act: async provider => await LegalSeed.SeedAsync(provider.GetRequiredService<CleansiaDbContext>()),
            assert: async (context, second) =>
            {
                Assert.False(second.Changed);

                var documents = await context.LegalDocuments.Include(d => d.Texts).AsNoTracking().ToListAsync();
                Assert.Equal(3, documents.Count);
                Assert.All(documents, d => Assert.Null(d.CountryId));
                Assert.All(documents, d => Assert.Equal(LegalDocument.VersionFor(d.EffectiveFrom), d.Version));
                Assert.All(documents, d => Assert.Equal(5, d.Texts.Count));
                Assert.Equal(
                    [LegalDocumentType.TermsOfService, LegalDocumentType.PrivacyPolicy, LegalDocumentType.WorkContract],
                    documents.Select(d => d.Type).OrderBy(t => t));
                Assert.Equal(15, await context.LegalDocumentTexts.CountAsync());
            },
            transactional: false);
    }

    /// <summary>
    /// The contract for work is a customer-audience text (ADR-0068 D1): published where the customer's
    /// texts are, so the wizard's sentence can link to it before anyone signs in.
    /// </summary>
    [Fact]
    public async Task The_Anonymous_Read_Serves_The_Work_Contract_With_The_Markets_Currency_And_No_Figure()
    {
        await TestMethod(
            setup: Anonymous,
            arrange: SeedMarketsAndTextsAsync,
            act: async provider => await ReadAsync(provider, LegalDocumentType.WorkContract, Czechia, "cs"),
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                var dto = result.Value;
                var seeded = await LegalSeed.PlatformWideAsync(context, LegalDocumentType.WorkContract);

                Assert.Equal(LegalDocumentType.WorkContract, dto.Type);
                Assert.Equal(seeded.Version, dto.Version);
                Assert.Equal("cs", dto.Language);
                Assert.Equal("Smlouva o dílo", dto.Title);
                Assert.Contains(" CZK", dto.ContentHtml);
                Assert.DoesNotContain("{{", dto.ContentHtml);
                Assert.Contains("<blockquote>", dto.ContentHtml);
                Assert.DoesNotContain(seeded.TextFor("cs")!.ContentMarkdown, c => char.IsDigit(c));
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
                var terms = versions.Value.Single();
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
}
