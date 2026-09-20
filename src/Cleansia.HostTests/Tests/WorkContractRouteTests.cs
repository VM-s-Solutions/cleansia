using System.Net.Http.Json;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Contracts;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// ADR-0068 (Verification #9) — the contract routes end to end on the real hosts: the two partner
/// routes and the read answer 401 anonymous; a customer-profile token is 403d on the partner routes
/// behind <c>CanTakeOrder</c>; an approved cleaner previews the order's contract and, once placed on
/// the crew, accepts it and reads it back; the read keyed on the acceptance answers the order's
/// customer on the Customer host and an administrator on the Admin host, and refuses another
/// customer and another cleaner with the same not-found the order itself would give.
///
/// <para>Host coverage is Partner, Mobile.Partner, Customer and Admin — the four the harness boots;
/// <c>Web.Mobile.Customer</c>'s controller is a byte-identical sibling over the same handler.</para>
/// </summary>
public sealed class WorkContractRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string CleanerId = "u-wc-cleaner";
    private const string CleanerEmail = "wc-cleaner@hosttests.local";
    private const string OtherCleanerId = "u-wc-other-cleaner";
    private const string OtherCleanerEmail = "wc-other-cleaner@hosttests.local";
    private const string CustomerEmail = "wc-customer@hosttests.local";
    private const string StrangerEmail = "wc-stranger@hosttests.local";
    private const string AdminId = "u-wc-admin";
    private const string AdminEmail = "wc-admin@hosttests.local";

    private sealed record Arranged(
        string OrderId, string TextEnId, string CleanerEmployeeId, string OtherEmployeeId, string CustomerUserId, string StrangerUserId);

    private async Task<Arranged> ArrangeAsync(bool assigned)
    {
        string orderId = "", textId = "", cleanerEmployeeId = "", otherEmployeeId = "", customerUserId = "", strangerUserId = "";

        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var (workContract, textEnId) = await DomainSeed.WorkContractInForceAsync(ctx);
            textId = textEnId;

            var customer = DomainSeed.Customer(CustomerEmail);
            var stranger = DomainSeed.Customer(StrangerEmail);
            var cleanerUser = DomainSeed.EmployeeUser(CleanerEmail);
            var otherUser = DomainSeed.EmployeeUser(OtherCleanerEmail);
            ctx.Users.AddRange(customer, stranger, cleanerUser, otherUser);

            var cleaner = DomainSeed.ApprovedEmployee(cleanerUser);
            var other = DomainSeed.ApprovedEmployee(otherUser);
            ctx.Employees.AddRange(cleaner, other);
            ctx.EmployeeDocuments.AddRange(DomainSeed.ActiveDocument(cleaner.Id), DomainSeed.ActiveDocument(other.Id));

            var order = DomainSeed.NewOrder(customer.Id, CustomerEmail, workContract: workContract);
            if (assigned)
            {
                DomainSeed.ConfirmAndAssign(order, cleaner);
            }

            ctx.Orders.Add(order);

            orderId = order.Id;
            cleanerEmployeeId = cleaner.Id;
            otherEmployeeId = other.Id;
            customerUserId = customer.Id;
            strangerUserId = stranger.Id;
        });

        return new Arranged(orderId, textId, cleanerEmployeeId, otherEmployeeId, customerUserId, strangerUserId);
    }

    private static string CleanerToken(string audience, string employeeId) =>
        TestJwtFactory.Mint(audience, CleanerId, CleanerEmail, UserProfile.Employee, employeeId: employeeId);

    private static string OtherCleanerToken(string audience, string employeeId) =>
        TestJwtFactory.Mint(audience, OtherCleanerId, OtherCleanerEmail, UserProfile.Employee, employeeId: employeeId);

    private static string CustomerToken(string audience, string userId, string email) =>
        TestJwtFactory.Mint(audience, userId, email, UserProfile.Customer);

    private static string AdminToken() =>
        TestJwtFactory.Mint(AdminAudience, AdminId, AdminEmail, UserProfile.Administrator);

    private static string PreviewRoute(string orderId) => $"/api/Order/GetWorkContractPreview?orderId={orderId}&language=en";
    private static string ReadRoute(string acceptanceId) => $"/api/Order/GetWorkContract?acceptanceId={acceptanceId}&language=en";
    private static string AdminReadRoute(string acceptanceId) => $"/api/AdminOrder/GetWorkContract?acceptanceId={acceptanceId}&language=en";
    private const string AcceptRoute = "/api/Order/AcceptWorkContract";

    // ── 401 anonymous, on every host ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Anonymous_callers_are_401d_on_every_contract_route()
    {
        HttpAssert.IsUnauthorized(await PartnerClientAnonymous().GetAsync(PreviewRoute("any")));
        HttpAssert.IsUnauthorized(await PartnerClientAnonymous().PostAsync(AcceptRoute, JsonContent.Create(new { OrderId = "any", AcceptedWorkContractTextId = "any" })));
        HttpAssert.IsUnauthorized(await PartnerClientAnonymous().GetAsync(ReadRoute("any")));
        HttpAssert.IsUnauthorized(await MobileClientAnonymous().GetAsync(PreviewRoute("any")));
        HttpAssert.IsUnauthorized(await MobileClientAnonymous().PostAsync(AcceptRoute, JsonContent.Create(new { OrderId = "any", AcceptedWorkContractTextId = "any" })));
        HttpAssert.IsUnauthorized(await MobileClientAnonymous().GetAsync(ReadRoute("any")));
        HttpAssert.IsUnauthorized(await CustomerClientAnonymous().GetAsync(ReadRoute("any")));
        HttpAssert.IsUnauthorized(await AdminClientAnonymous().GetAsync(AdminReadRoute("any")));
    }

    // ── 403 cross-audience on the partner routes ────────────────────────────────────────────────

    [Fact]
    public async Task A_customer_profile_token_is_403d_on_the_partner_take_routes()
    {
        var a = await ArrangeAsync(assigned: false);
        var customerOnPartner = CustomerToken(PartnerAudience, a.CustomerUserId, CustomerEmail);
        var customerOnMobile = CustomerToken(MobileAudience, a.CustomerUserId, CustomerEmail);

        HttpAssert.IsForbidden(await PartnerClient(customerOnPartner).GetAsync(PreviewRoute(a.OrderId)));
        HttpAssert.IsForbidden(await PartnerClient(customerOnPartner).PostAsync(AcceptRoute, JsonContent.Create(new { OrderId = a.OrderId, AcceptedWorkContractTextId = a.TextEnId })));
        HttpAssert.IsForbidden(await MobileClient(customerOnMobile).GetAsync(PreviewRoute(a.OrderId)));
        HttpAssert.IsForbidden(await MobileClient(customerOnMobile).PostAsync(AcceptRoute, JsonContent.Create(new { OrderId = a.OrderId, AcceptedWorkContractTextId = a.TextEnId })));
    }

    // ── 200 in audience ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_approved_cleaner_previews_the_orders_contract_on_both_partner_hosts()
    {
        var a = await ArrangeAsync(assigned: false);

        var partner = await PartnerClient(CleanerToken(PartnerAudience, a.CleanerEmployeeId)).GetAsync(PreviewRoute(a.OrderId));
        var mobile = await MobileClient(CleanerToken(MobileAudience, a.CleanerEmployeeId)).GetAsync(PreviewRoute(a.OrderId));

        HttpAssert.IsOk(partner);
        HttpAssert.IsOk(mobile);
        using var doc = JsonDocument.Parse(await partner.Content.ReadAsStringAsync());
        var body = doc.RootElement;
        Assert.Equal(a.TextEnId, body.GetProperty("legalDocumentTextId").GetString());
        Assert.Equal("en", body.GetProperty("language").GetString());
        Assert.Equal("Contract for Work", body.GetProperty("title").GetString());
        Assert.Contains("CZK", body.GetProperty("contentHtml").GetString());
        Assert.Equal("Brno · 602 xx", body.GetProperty("facts").GetProperty("locationApproximate").GetString());
        Assert.Equal(1500m, body.GetProperty("facts").GetProperty("totalPrice").GetDecimal());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("acceptance").ValueKind);
    }

    [Fact]
    public async Task A_placed_cleaner_accepts_the_contract_and_the_three_parties_read_it_while_strangers_are_refused()
    {
        var a = await ArrangeAsync(assigned: true);
        var cleaner = PartnerClient(CleanerToken(PartnerAudience, a.CleanerEmployeeId));

        var accept = await cleaner.PostAsync(AcceptRoute, JsonContent.Create(new { OrderId = a.OrderId, AcceptedWorkContractTextId = a.TextEnId }));

        HttpAssert.IsOk(accept);
        using var accepted = JsonDocument.Parse(await accept.Content.ReadAsStringAsync());
        var acceptanceId = accepted.RootElement.GetProperty("acceptanceId").GetString()!;
        Assert.Equal(a.OrderId, accepted.RootElement.GetProperty("orderId").GetString());

        var row = await QueryAsync(ctx => ctx.Set<WorkContractAcceptance>().IgnoreQueryFilters().SingleAsync(x => x.Id == acceptanceId));
        Assert.Equal(a.CleanerEmployeeId, row.EmployeeId);
        Assert.Equal(a.TextEnId, row.LegalDocumentTextId);
        Assert.Equal(PartnerAudience, row.ClientAudience);
        Assert.Null(row.DeviceId);

        // The double tap: the same seat answers the same row and writes nothing.
        var again = await cleaner.PostAsync(AcceptRoute, JsonContent.Create(new { OrderId = a.OrderId, AcceptedWorkContractTextId = a.TextEnId }));
        HttpAssert.IsOk(again);
        using var acceptedAgain = JsonDocument.Parse(await again.Content.ReadAsStringAsync());
        Assert.Equal(acceptanceId, acceptedAgain.RootElement.GetProperty("acceptanceId").GetString());
        Assert.Equal(1, await QueryAsync(ctx => ctx.Set<WorkContractAcceptance>().IgnoreQueryFilters().CountAsync(x => x.OrderId == a.OrderId)));

        // The accepting cleaner, the order's customer and an administrator read it.
        var byCleaner = await cleaner.GetAsync(ReadRoute(acceptanceId));
        var byCustomer = await CustomerClient(CustomerToken(CustomerAudience, a.CustomerUserId, CustomerEmail)).GetAsync(ReadRoute(acceptanceId));
        var byAdmin = await AdminClient(AdminToken()).GetAsync(AdminReadRoute(acceptanceId));
        HttpAssert.IsOk(byCleaner);
        HttpAssert.IsOk(byCustomer);
        HttpAssert.IsOk(byAdmin);
        using var read = JsonDocument.Parse(await byCustomer.Content.ReadAsStringAsync());
        Assert.Equal(a.TextEnId, read.RootElement.GetProperty("legalDocumentTextId").GetString());
        Assert.Equal("en", read.RootElement.GetProperty("acceptance").GetProperty("acceptedLanguage").GetString());
        Assert.Equal(a.CleanerEmployeeId, read.RootElement.GetProperty("acceptance").GetProperty("employeeId").GetString());
        Assert.Equal("Brno · 602 xx", read.RootElement.GetProperty("facts").GetProperty("locationApproximate").GetString());

        // Another customer and another cleaner are refused with the order's own not-found.
        var byStranger = await CustomerClient(CustomerToken(CustomerAudience, a.StrangerUserId, StrangerEmail)).GetAsync(ReadRoute(acceptanceId));
        var byOtherCleaner = await PartnerClient(OtherCleanerToken(PartnerAudience, a.OtherEmployeeId)).GetAsync(ReadRoute(acceptanceId));
        await HttpAssert.RejectedAsync(byStranger, BusinessErrorMessage.OrderNotFound);
        await HttpAssert.RejectedAsync(byOtherCleaner, BusinessErrorMessage.OrderNotFound);
        HttpAssert.ClearedTheGate(byStranger);
        HttpAssert.ClearedTheGate(byOtherCleaner);
    }

    [Fact]
    public async Task A_cleaner_not_on_the_crew_cannot_accept_and_a_text_of_no_document_is_a_mismatch()
    {
        var a = await ArrangeAsync(assigned: true);

        var outsider = await PartnerClient(OtherCleanerToken(PartnerAudience, a.OtherEmployeeId))
            .PostAsync(AcceptRoute, JsonContent.Create(new { OrderId = a.OrderId, AcceptedWorkContractTextId = a.TextEnId }));
        var mismatch = await PartnerClient(CleanerToken(PartnerAudience, a.CleanerEmployeeId))
            .PostAsync(AcceptRoute, JsonContent.Create(new { OrderId = a.OrderId, AcceptedWorkContractTextId = "01TEXT000000000000NOWHERE1" }));
        var noText = await PartnerClient(CleanerToken(PartnerAudience, a.CleanerEmployeeId))
            .PostAsync(AcceptRoute, JsonContent.Create(new { OrderId = a.OrderId }));

        await HttpAssert.RejectedAsync(outsider, BusinessErrorMessage.EmployeeNotAssignedToOrder);
        await HttpAssert.RejectedAsync(mismatch, BusinessErrorMessage.WorkContractTextMismatch);
        await HttpAssert.RejectedAsync(noText, BusinessErrorMessage.WorkContractNotAccepted);
        Assert.Equal(0, await QueryAsync(ctx => ctx.Set<WorkContractAcceptance>().IgnoreQueryFilters().CountAsync(x => x.OrderId == a.OrderId)));
    }
}
