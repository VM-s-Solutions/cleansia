using System.Net;
using System.Text;
using System.Text.Json;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// The customer incident file (<c>AdminGdprController.ExportCustomerIncidentFile</c>, Q-AUD-L6 ruling)
/// behind <c>Policy.CanAdminExportUserData</c> end-to-end against the real auth/authz pipeline: Employee
/// and Customer roles are 403'd at the gate, an anonymous caller is 401'd, an Administrator gets a PDF
/// named after the subject and the day with the <c>gdpr.user.incident_file</c> row committed behind it,
/// and an order that is not the subject's is a 400 <c>order.not_found</c>.
/// </summary>
public sealed class AdminGdprIncidentFilePolicyTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string SubjectId = "gdpr-incident-subject";
    private const string SubjectEmail = "incident-subject@hosttests.local";
    private const string StrangerId = "gdpr-incident-stranger";
    private const string OrderId = "gdpr-inc-order";
    private const string StrangerOrderId = "gdpr-inc-stranger-order";

    private static string Route(string userId, string? orderId = null) =>
        $"/api/v1/AdminGdpr/incident-file/{userId}" + (orderId is null ? string.Empty : $"?orderId={orderId}");

    [Fact]
    public async Task NonAdmin_Employee_is_403d_on_the_incident_file()
    {
        var token = TestJwtFactory.Mint(AdminAudience, "emp-1", "emp-1@hosttests.local", UserProfile.Employee);

        var resp = await AdminClient(token).PostAsync(Route(SubjectId), content: null);

        HttpAssert.IsForbidden(resp);
    }

    [Fact]
    public async Task NonAdmin_Customer_is_403d_on_the_incident_file()
    {
        var token = TestJwtFactory.Mint(AdminAudience, "cust-1", "cust-1@hosttests.local", UserProfile.Customer);

        var resp = await AdminClient(token).PostAsync(Route(SubjectId), content: null);

        HttpAssert.IsForbidden(resp);
    }

    [Fact]
    public async Task Anonymous_caller_is_401d_on_the_incident_file()
    {
        var resp = await AdminHost.CreateClient().PostAsync(Route(SubjectId), content: null);

        HttpAssert.IsUnauthorized(resp);
    }

    [Fact]
    public async Task Admin_clears_the_gate_gets_a_named_pdf_and_leaves_its_record()
    {
        await SeedSubjectWithAnOrderAndATrailAsync();
        var token = TestJwtFactory.Mint(AdminAudience, "admin-a", "admin-a@hosttests.local", UserProfile.Administrator);

        var resp = await AdminClient(token).PostAsync(Route(SubjectId, OrderId), content: null);

        HttpAssert.IsOk(resp);
        Assert.Equal("application/pdf", resp.Content.Headers.ContentType?.MediaType);
        Assert.Equal($"incident-{SubjectId}-{DateTimeOffset.UtcNow:yyyyMMdd}.pdf", resp.Content.Headers.ContentDisposition?.FileNameStar ?? resp.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));

        var audit = await QueryAsync(ctx => ctx.AdminActionAudits.IgnoreQueryFilters().SingleAsync(a => a.Action == "gdpr.user.incident_file"));
        Assert.True(audit.Success);
        Assert.Equal(SubjectId, audit.ResourceId);
        Assert.Equal("admin-a", audit.ActorId);
        var snapshot = JsonDocument.Parse(audit.AfterJson!).RootElement;
        Assert.Equal(OrderId, snapshot.GetProperty("orderId").GetString());
        Assert.Equal(1, snapshot.GetProperty("orderCount").GetInt32());
        Assert.Equal(1, snapshot.GetProperty("trailEntryCount").GetInt32());
        Assert.Matches("^[0-9a-f]{64}$", snapshot.GetProperty("dataSha256").GetString());
        Assert.DoesNotContain(SubjectEmail, audit.AfterJson!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_strangers_order_is_a_400_order_not_found()
    {
        await SeedSubjectWithAnOrderAndATrailAsync();
        var token = TestJwtFactory.Mint(AdminAudience, "admin-a", "admin-a@hosttests.local", UserProfile.Administrator);

        var resp = await AdminClient(token).PostAsync(Route(SubjectId, StrangerOrderId), content: null);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("order.not_found", body);
    }

    private Task SeedSubjectWithAnOrderAndATrailAsync() =>
        SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var subject = DomainSeed.Customer(SubjectEmail, HostTestTenants.Default);
            subject.Id = SubjectId;
            var stranger = DomainSeed.Customer("incident-stranger@hosttests.local", HostTestTenants.Default);
            stranger.Id = StrangerId;
            ctx.Users.AddRange(subject, stranger);

            var order = DomainSeed.NewOrder(SubjectId, SubjectEmail, HostTestTenants.Default);
            order.Id = OrderId;
            var strangerOrder = DomainSeed.NewOrder(StrangerId, "incident-stranger@hosttests.local", HostTestTenants.Default);
            strangerOrder.Id = StrangerOrderId;
            ctx.Orders.AddRange(order, strangerOrder);

            ctx.CustomerActionAudits.Add(DomainSeed.CustomerAudit("caud-incident-1", SubjectId, HostTestTenants.Default, resourceId: OrderId));
        });
}
