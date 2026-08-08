using System.Net.Http.Json;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// The owner transfers a cleaner's pay from their own bank, so the only thing that can tell the
/// platform the money moved is a person. End-to-end over the two hosts that meet at this fact:
/// <list type="bullet">
///   <item>an Administrator on the Admin host marks an approved invoice paid — the row gets its paid
///   stamp and the append-only <c>AdminActionAudit</c> row names the actor;</item>
///   <item>the cleaner, on the Partner host, reads <c>Paid</c> where they read <c>Approved</c> before,
///   on both the detail and the list;</item>
///   <item>a non-admin is refused at the policy gate and the invoice is untouched;</item>
///   <item>a second mark and a not-yet-approved invoice are each refused with their own code.</item>
/// </list>
/// </summary>
public sealed class MarkInvoicePaidAdminOnlyTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string AdminUserId = "u-admin-markpaid";
    private const string AdminEmail = "admin-markpaid@hosttests.local";
    private const string EmployeeEmail = "emp-markpaid@hosttests.local";

    private sealed record Arranged(string EmployeeId, string ApprovedInvoiceId, string PendingInvoiceId);

    private async Task<Arranged> ArrangeAsync()
    {
        string employeeId = "", approved = "", pending = "";

        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);

            var user = DomainSeed.EmployeeUser(EmployeeEmail);
            ctx.Users.Add(user);

            var employee = DomainSeed.ApprovedEmployee(user);
            ctx.Employees.Add(employee);
            ctx.EmployeeDocuments.Add(DomainSeed.ActiveDocument(employee.Id));

            // One invoice per (employee, period) is a unique index, so the two states need two periods.
            var approvedPeriod = PayPeriod.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15));
            var pendingPeriod = PayPeriod.Create(new DateOnly(2026, 1, 16), new DateOnly(2026, 1, 31));
            ctx.PayPeriods.AddRange(approvedPeriod, pendingPeriod);

            var approvedInvoice = DomainSeed.Invoice(employee.Id, approvedPeriod.Id);
            approvedInvoice.Approve(AdminEmail);
            ctx.EmployeeInvoices.Add(approvedInvoice);

            var pendingInvoice = DomainSeed.Invoice(employee.Id, pendingPeriod.Id);
            ctx.EmployeeInvoices.Add(pendingInvoice);

            employeeId = employee.Id;
            approved = approvedInvoice.Id;
            pending = pendingInvoice.Id;
        });

        return new Arranged(employeeId, approved, pending);
    }

    private static HttpContent MarkPaidBody(string invoiceId, string note = "VS 0001000001") =>
        JsonContent.Create(new
        {
            InvoiceId = invoiceId,
            BankTransferNote = note,
            AdminNotes = (string?)null,
        });

    private static string AdminToken() =>
        TestJwtFactory.Mint(AdminAudience, AdminUserId, AdminEmail, UserProfile.Administrator);

    private static string EmployeeToken(string employeeId) =>
        TestJwtFactory.Mint(PartnerAudience, "u-emp-markpaid", EmployeeEmail, UserProfile.Employee, employeeId: employeeId);

    private Task<EmployeeInvoice?> ReadInvoiceAsync(string invoiceId) =>
        QueryAsync(ctx => ctx.Set<EmployeeInvoice>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == invoiceId));

    private Task<List<AdminActionAudit>> ReadAuditsAsync() =>
        QueryAsync(ctx => ctx.AdminActionAudits.IgnoreQueryFilters().ToListAsync());

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    [Fact]
    public async Task Admin_marking_an_approved_invoice_paid_stamps_the_date_and_records_the_actor()
    {
        var a = await ArrangeAsync();

        var resp = await AdminClient(AdminToken()).PutAsync("/api/AdminInvoice/mark-paid", MarkPaidBody(a.ApprovedInvoiceId));

        HttpAssert.IsOk(resp);

        var invoice = await ReadInvoiceAsync(a.ApprovedInvoiceId);
        Assert.NotNull(invoice);
        Assert.Equal(EmployeeInvoiceStatus.Paid, invoice!.Status);
        Assert.NotNull(invoice.PaidAt);
        Assert.Equal("VS 0001000001", invoice.BankTransferNote);

        var audit = Assert.Single(await ReadAuditsAsync());
        Assert.Equal(AdminUserId, audit.ActorId);
        Assert.Equal(AdminEmail, audit.ActorEmail);
        Assert.Equal(nameof(MarkInvoicePaid), audit.Action);
        Assert.Equal(a.ApprovedInvoiceId, audit.ResourceId);
        Assert.True(audit.Success);
    }

    [Fact]
    public async Task The_cleaner_reads_the_invoice_as_paid_only_after_the_admin_marks_it()
    {
        var a = await ArrangeAsync();
        var cleaner = PartnerClient(EmployeeToken(a.EmployeeId));

        var before = await cleaner.GetAsync($"/api/EmployeePayroll/GetInvoiceById/{a.ApprovedInvoiceId}");
        HttpAssert.IsOk(before);
        var beforeBody = await BodyAsync(before);
        Assert.Equal((int)EmployeeInvoiceStatus.Approved, beforeBody.GetProperty("status").GetInt32());
        Assert.Equal(JsonValueKind.Null, beforeBody.GetProperty("paidAt").ValueKind);

        HttpAssert.IsOk(await AdminClient(AdminToken()).PutAsync("/api/AdminInvoice/mark-paid", MarkPaidBody(a.ApprovedInvoiceId)));

        var after = await cleaner.GetAsync($"/api/EmployeePayroll/GetInvoiceById/{a.ApprovedInvoiceId}");
        HttpAssert.IsOk(after);
        var afterBody = await BodyAsync(after);
        Assert.Equal((int)EmployeeInvoiceStatus.Paid, afterBody.GetProperty("status").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, afterBody.GetProperty("paidAt").ValueKind);

        var list = await cleaner.GetAsync("/api/EmployeePayroll/GetPagedInvoices?offset=0&limit=50");
        HttpAssert.IsOk(list);
        var rows = (await BodyAsync(list)).GetProperty("data").EnumerateArray().ToList();
        Assert.Equal(2, rows.Count);
        var listed = rows.Single(r => r.GetProperty("id").GetString() == a.ApprovedInvoiceId);
        Assert.Equal((int)EmployeeInvoiceStatus.Paid, listed.GetProperty("status").GetInt32());
        var stillPending = rows.Single(r => r.GetProperty("id").GetString() == a.PendingInvoiceId);
        Assert.Equal((int)EmployeeInvoiceStatus.Pending, stillPending.GetProperty("status").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, listed.GetProperty("paidAt").ValueKind);
    }

    [Fact]
    public async Task An_employee_cannot_mark_an_invoice_paid_and_the_invoice_is_untouched()
    {
        var a = await ArrangeAsync();
        var token = TestJwtFactory.Mint(AdminAudience, "u-emp-on-admin", EmployeeEmail, UserProfile.Employee);

        var resp = await AdminClient(token).PutAsync("/api/AdminInvoice/mark-paid", MarkPaidBody(a.ApprovedInvoiceId));

        HttpAssert.IsForbidden(resp);

        var invoice = await ReadInvoiceAsync(a.ApprovedInvoiceId);
        Assert.Equal(EmployeeInvoiceStatus.Approved, invoice!.Status);
        Assert.Null(invoice.PaidAt);
        Assert.Empty(await ReadAuditsAsync());
    }

    [Fact]
    public async Task Marking_an_already_paid_invoice_again_is_refused_and_the_first_stamp_survives()
    {
        var a = await ArrangeAsync();
        var admin = AdminClient(AdminToken());

        HttpAssert.IsOk(await admin.PutAsync("/api/AdminInvoice/mark-paid", MarkPaidBody(a.ApprovedInvoiceId)));
        var firstStamp = (await ReadInvoiceAsync(a.ApprovedInvoiceId))!.PaidAt;

        var second = await admin.PutAsync("/api/AdminInvoice/mark-paid", MarkPaidBody(a.ApprovedInvoiceId, "a second transfer"));

        await HttpAssert.AssertBusinessErrorAsync(second, BusinessErrorMessage.InvoiceAlreadyPaid);

        var invoice = await ReadInvoiceAsync(a.ApprovedInvoiceId);
        Assert.Equal(firstStamp, invoice!.PaidAt);
        Assert.Equal("VS 0001000001", invoice.BankTransferNote);
    }

    [Fact]
    public async Task Marking_a_pending_invoice_paid_is_refused_as_not_approved()
    {
        var a = await ArrangeAsync();

        var resp = await AdminClient(AdminToken()).PutAsync("/api/AdminInvoice/mark-paid", MarkPaidBody(a.PendingInvoiceId));

        await HttpAssert.AssertBusinessErrorAsync(resp, BusinessErrorMessage.InvoiceNotApproved);

        var invoice = await ReadInvoiceAsync(a.PendingInvoiceId);
        Assert.Equal(EmployeeInvoiceStatus.Pending, invoice!.Status);
        Assert.Null(invoice.PaidAt);
    }
}
