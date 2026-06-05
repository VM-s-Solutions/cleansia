using System.Net.Http.Json;
using System.Text.Json;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// The partner analytics endpoints accept a [FromQuery]
/// EmployeeId, but a non-admin caller is server-scoped to their OWN employee id; a foreign EmployeeId
/// is ignored. End-to-end on the Partner Dashboard host: employee E1 (who has NO invoices) supplies
/// E2's EmployeeId (E2 has a 1000 invoice). E1 sees their OWN data (TotalEarnings == 0),
/// never E2's; the IDOR would have leaked E2's 1000.
/// </summary>
public sealed class Ac7PartnerAnalyticsIdorTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private sealed record Arranged(string E1Id, string E1Email, string E2Id);

    private async Task<Arranged> ArrangeAsync()
    {
        string e1Id = "", e2Id = "";
        const string e1Email = "analytics1@hosttests.local";

        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var u1 = DomainSeed.EmployeeUser(e1Email);
            var u2 = DomainSeed.EmployeeUser("analytics2@hosttests.local");
            ctx.Users.AddRange(u1, u2);

            var e1 = DomainSeed.ApprovedEmployee(u1);   // E1: complete + approved, NO invoices
            var e2 = DomainSeed.ApprovedEmployee(u2);   // E2: has an invoice
            ctx.Employees.AddRange(e1, e2);
            ctx.EmployeeDocuments.AddRange(
                DomainSeed.ActiveDocument(e1.Id),
                DomainSeed.ActiveDocument(e2.Id));

            var period = DomainSeed.PayPeriod();
            ctx.PayPeriods.Add(period);
            ctx.EmployeeInvoices.Add(DomainSeed.Invoice(e2.Id, period.Id)); // subTotal 1000 → E2 earnings

            e1Id = e1.Id;
            e2Id = e2.Id;
        });

        return new Arranged(e1Id, e1Email, e2Id);
    }

    [Fact]
    public async Task Employee_supplying_a_foreign_employee_id_gets_their_own_scoped_analytics_not_the_others()
    {
        var a = await ArrangeAsync();
        var token = TestJwtFactory.Mint(PartnerAudience, "u-an1", a.E1Email, UserProfile.Employee, employeeId: a.E1Id);

        var start = Uri.EscapeDataString(DateTime.UtcNow.AddDays(-30).ToString("o"));
        var end = Uri.EscapeDataString(DateTime.UtcNow.AddDays(1).ToString("o"));
        var resp = await PartnerClient(token).GetAsync(
            $"/api/Dashboard/GetEarningsAnalytics?EmployeeId={a.E2Id}&StartDate={start}&EndDate={end}");

        HttpAssert.IsOk(resp);

        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var total = doc.RootElement.GetProperty("totalEarnings").GetDecimal();

        // Scoped to E1 (no invoices) → 0. If the foreign EmployeeId were honored we'd see E2's 1000.
        Assert.Equal(0m, total);
    }
}
