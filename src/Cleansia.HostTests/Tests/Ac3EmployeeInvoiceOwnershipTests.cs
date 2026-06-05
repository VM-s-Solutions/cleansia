using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;
using Cleansia.Core.AppServices.Common;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// AC3 (Note A, paired fix T-0100) — an Employee is scoped to their OWN invoices. The payroll
/// invoice-read permissions are EmployeeOrAdmin (so a cleaner can see their own pay) + an inner
/// handler ownership check ([OWN-DATA]): requesting ANOTHER employee's invoice returns the not-found
/// business error, never the other cleaner's invoice; requesting your own returns it. End-to-end
/// through the Partner host with a real employee-audience JWT carrying the employee_id claim.
/// </summary>
public sealed class Ac3EmployeeInvoiceOwnershipTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private sealed record Arranged(string Emp1Id, string Emp1Email, string Own1InvoiceId, string Other2InvoiceId);

    private async Task<Arranged> ArrangeTwoEmployeesWithInvoicesAsync()
    {
        string emp1Id = "", emp1Email = "emp1@hosttests.local";
        string own1 = "", other2 = "";

        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);

            var u1 = DomainSeed.EmployeeUser(emp1Email);
            var u2 = DomainSeed.EmployeeUser("emp2@hosttests.local");
            ctx.Users.AddRange(u1, u2);

            var e1 = DomainSeed.ApprovedEmployee(u1);
            var e2 = DomainSeed.ApprovedEmployee(u2);
            ctx.Employees.AddRange(e1, e2);
            ctx.EmployeeDocuments.AddRange(
                DomainSeed.ActiveDocument(e1.Id),
                DomainSeed.ActiveDocument(e2.Id));

            var period = DomainSeed.PayPeriod();
            ctx.PayPeriods.Add(period);

            var inv1 = DomainSeed.Invoice(e1.Id, period.Id);
            var inv2 = DomainSeed.Invoice(e2.Id, period.Id);
            ctx.EmployeeInvoices.AddRange(inv1, inv2);

            emp1Id = e1.Id;
            own1 = inv1.Id;
            other2 = inv2.Id;
        });

        return new Arranged(emp1Id, emp1Email, own1, other2);
    }

    [Fact]
    public async Task Employee_requesting_another_employees_invoice_is_rejected_with_not_found()
    {
        var a = await ArrangeTwoEmployeesWithInvoicesAsync();
        var token = TestJwtFactory.Mint(PartnerAudience, "u-emp1", a.Emp1Email, UserProfile.Employee, employeeId: a.Emp1Id);

        var resp = await PartnerClient(token).GetAsync($"/api/EmployeePayroll/GetInvoiceById/{a.Other2InvoiceId}");

        // Never the other employee's invoice: the not-found business error (mapped to 400 here) or a
        // policy-layer 403/404.
        await HttpAssert.RejectedAsync(resp, BusinessErrorMessage.InvoiceNotFound);
    }

    [Fact]
    public async Task Employee_requesting_their_own_invoice_succeeds()
    {
        var a = await ArrangeTwoEmployeesWithInvoicesAsync();
        var token = TestJwtFactory.Mint(PartnerAudience, "u-emp1", a.Emp1Email, UserProfile.Employee, employeeId: a.Emp1Id);

        var resp = await PartnerClient(token).GetAsync($"/api/EmployeePayroll/GetInvoiceById/{a.Own1InvoiceId}");

        HttpAssert.IsOk(resp);
    }
}
