using System.Text.Json;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Legal;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// The stored legal texts end to end: a host boot seeds them (the hosted service is registered, not
/// just written), the anonymous <c>Legal/GetDocument</c> read on the Customer host serves the version in
/// force with the market's currency in the copy, the Partner host has no such route, and the admin
/// catalogue sits behind the real <c>AdminOnly</c> gate — anonymous 401, a customer 403, an
/// administrator 200 on both the version list and one text.
///
/// <para>Host coverage is <c>Web.Customer</c>; <c>Web.Mobile.Customer</c>'s controller is a
/// byte-identical sibling over the same handler, so a fifth host would add no claim.</para>
/// </summary>
public sealed class LegalDocumentRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string AdminId = "u-admin-legal";
    private const string AdminEmail = "admin-legal@hosttests.local";
    private const string CustomerId = "u-customer-legal";
    private const string CustomerEmail = "customer-legal@hosttests.local";

    private static string AdminToken() =>
        TestJwtFactory.Mint(AdminAudience, AdminId, AdminEmail, UserProfile.Administrator);

    private static string CustomerOnAdminToken() =>
        TestJwtFactory.Mint(AdminAudience, CustomerId, CustomerEmail, UserProfile.Customer);

    private static string CustomerRoute(string? countryId = null, string? language = null) =>
        $"/api/Legal/GetDocument?type={(int)LegalDocumentType.TermsOfService}"
        + (countryId is null ? "" : $"&countryId={countryId}")
        + (language is null ? "" : $"&language={language}");

    private Task ArrangeMarketAsync() => SeedAsync(DomainSeed.EnsureReferenceDataAsync);

    [Fact]
    public async Task Booting_a_host_seeds_the_three_customer_documents_in_five_languages()
    {
        await ArrangeMarketAsync();

        var documents = await QueryAsync(ctx => ctx.LegalDocuments.Include(d => d.Texts).AsNoTracking().ToListAsync());

        Assert.Equal(3, documents.Count);
        Assert.Contains(documents, d => d.Type == LegalDocumentType.WorkContract);
        Assert.All(documents, d => Assert.Equal(LegalDocumentAudience.Customer, d.Audience));
        Assert.All(documents, d => Assert.Null(d.CountryId));
        Assert.All(documents, d => Assert.Equal(LegalDocument.VersionFor(d.EffectiveFrom), d.Version));
        Assert.All(documents, d => Assert.Equal(5, d.Texts.Count));
    }

    [Fact]
    public async Task Anonymous_read_on_the_customer_host_serves_the_version_in_force_with_the_markets_currency()
    {
        await ArrangeMarketAsync();

        var resp = await CustomerClientAnonymous().GetAsync(CustomerRoute(DomainSeed.CountryId, "cs"));

        HttpAssert.IsOk(resp);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var body = doc.RootElement;
        var seeded = await QueryAsync(ctx => ctx.LegalDocuments.AsNoTracking()
            .SingleAsync(d => d.Type == LegalDocumentType.TermsOfService && d.CountryId == null));
        Assert.Equal(seeded.Version, body.GetProperty("version").GetString());
        Assert.Equal((int)LegalDocumentType.TermsOfService, body.GetProperty("type").GetInt32());
        Assert.Equal("cs", body.GetProperty("language").GetString());
        var html = body.GetProperty("contentHtml").GetString()!;
        Assert.Contains("<h2>", html);
        Assert.Contains("CZK", html);
        Assert.DoesNotContain("{{", html);
        Assert.Equal(64, body.GetProperty("contentHash").GetString()!.Length);
    }

    [Fact]
    public async Task No_market_and_an_unknown_language_read_the_default_market_in_English()
    {
        await ArrangeMarketAsync();

        var resp = await CustomerClientAnonymous().GetAsync(CustomerRoute(language: "de"));

        HttpAssert.IsOk(resp);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal("en", doc.RootElement.GetProperty("language").GetString());
        Assert.Equal("Terms of Service", doc.RootElement.GetProperty("title").GetString());
    }

    /// <summary>
    /// The contract for work is a customer-audience text (ADR-0068 D1), published where the customer's
    /// texts are: the wizard's sentence links to it before anyone signs in.
    /// </summary>
    [Fact]
    public async Task Anonymous_read_of_the_work_contract_answers_on_the_customer_host_with_the_markets_currency()
    {
        await ArrangeMarketAsync();

        var resp = await CustomerClientAnonymous().GetAsync(
            $"/api/Legal/GetDocument?type={(int)LegalDocumentType.WorkContract}&countryId={DomainSeed.CountryId}&language=en");

        HttpAssert.IsOk(resp);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal((int)LegalDocumentType.WorkContract, doc.RootElement.GetProperty("type").GetInt32());
        Assert.Equal("Contract for Work", doc.RootElement.GetProperty("title").GetString());
        var html = doc.RootElement.GetProperty("contentHtml").GetString()!;
        Assert.Contains("CZK", html);
        Assert.DoesNotContain("{{", html);
        Assert.Contains("<blockquote>", html);
    }

    [Fact]
    public async Task The_partner_host_has_no_legal_document_route()
    {
        var resp = await PartnerClientAnonymous().GetAsync(CustomerRoute());

        HttpAssert.IsNotFound(resp);
    }

    [Fact]
    public async Task Anonymous_caller_is_401d_on_the_admin_catalogue()
    {
        var versions = await AdminHost.CreateClient().GetAsync("/api/AdminLegal/get-versions");
        var document = await AdminHost.CreateClient().GetAsync("/api/AdminLegal/get-document/any-id");

        HttpAssert.IsUnauthorized(versions);
        HttpAssert.IsUnauthorized(document);
    }

    [Fact]
    public async Task A_customer_is_403d_on_the_admin_catalogue()
    {
        var versions = await AdminClient(CustomerOnAdminToken()).GetAsync("/api/AdminLegal/get-versions");
        var document = await AdminClient(CustomerOnAdminToken()).GetAsync("/api/AdminLegal/get-document/any-id");

        HttpAssert.IsForbidden(versions);
        HttpAssert.IsForbidden(document);
    }

    [Fact]
    public async Task An_administrator_lists_the_versions_and_reads_one_text_as_seeded()
    {
        await ArrangeMarketAsync();
        var client = AdminClient(AdminToken());

        var versions = await client.GetAsync($"/api/AdminLegal/get-versions?type={(int)LegalDocumentType.PrivacyPolicy}");

        HttpAssert.IsOk(versions);
        using var list = JsonDocument.Parse(await versions.Content.ReadAsStringAsync());
        var version = Assert.Single(list.RootElement.EnumerateArray());
        Assert.True(version.GetProperty("isInForce").GetBoolean());
        Assert.Equal(5, version.GetProperty("texts").GetArrayLength());

        var document = await client.GetAsync($"/api/AdminLegal/get-document/{version.GetProperty("id").GetString()}?language=uk");

        HttpAssert.IsOk(document);
        using var text = JsonDocument.Parse(await document.Content.ReadAsStringAsync());
        Assert.Equal("uk", text.RootElement.GetProperty("language").GetString());
        Assert.Equal(version.GetProperty("version").GetString(), text.RootElement.GetProperty("version").GetString());
        Assert.StartsWith("> ", text.RootElement.GetProperty("contentMarkdown").GetString());
        Assert.Contains("<blockquote>", text.RootElement.GetProperty("contentHtml").GetString());
    }
}
