using System.Net.Http.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// AC9 (cross-tenant rejection — testing.md must-cover #5) — a caller whose JWT tenant_id differs from
/// the target resource's TenantId gets NOT-FOUND (the EF global query filter hides the row), never the
/// cross-tenant resource. At least one case per category: user-by-id, dispute create, invoice-by-id,
/// order action. The attacker is given the genuine owning sub/employee_id so ONLY the tenant boundary
/// is under test — every other ownership gate would otherwise pass.
/// </summary>
public sealed class Ac9CrossTenantRejectionTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string TenantA = "tenant-A";
    private const string TenantB = "tenant-B";

    [Fact]
    public async Task Cross_tenant_get_user_by_id_returns_not_found()
    {
        string userId = "";
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var user = DomainSeed.EmployeeUser("xt-user@hosttests.local", tenantId: TenantA);
            ctx.Users.Add(user);
            userId = user.Id;
        });

        // sub == the tenant-A user id (so OwnerOrElevated passes), but the token's tenant is B.
        var token = TestJwtFactory.Mint(PartnerAudience, userId, "xt-user@hosttests.local",
            UserProfile.Employee, tenantId: TenantB);

        var resp = await PartnerClient(token).GetAsync($"/api/User/GetById?UserId={userId}");

        await HttpAssert.RejectedAsync(resp, BusinessErrorMessage.NotExistingUserWithId);
    }

    [Fact]
    public async Task Cross_tenant_create_dispute_returns_not_found_and_creates_no_dispute()
    {
        string ownerId = "", orderId = "";
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var owner = DomainSeed.Customer("xt-owner@hosttests.local", tenantId: TenantA);
            ctx.Users.Add(owner);
            var order = DomainSeed.NewOrder(owner.Id, "xt-owner@hosttests.local", tenantId: TenantA);
            ctx.Orders.Add(order);
            ownerId = owner.Id;
            orderId = order.Id;
        });

        // sub == the genuine order owner, but the token's tenant is B → the order is invisible.
        var token = TestJwtFactory.Mint(CustomerAudience, ownerId, "xt-owner@hosttests.local",
            UserProfile.Customer, tenantId: TenantB);

        var resp = await CustomerClient(token).PostAsync("/api/Dispute/Create", JsonContent.Create(new
        {
            OrderId = orderId,
            Reason = (int)DisputeReason.Other,
            Description = "cross-tenant dispute attempt body",
        }));

        await HttpAssert.RejectedAsync(resp, BusinessErrorMessage.OrderNotFound);

        var disputes = await QueryAsync(ctx =>
            ctx.Set<Dispute>().IgnoreQueryFilters().CountAsync(d => d.OrderId == orderId));
        Assert.Equal(0, disputes);
    }

    [Fact]
    public async Task Cross_tenant_get_invoice_by_id_returns_not_found()
    {
        string callerEmployeeId = "", invoiceId = "";
        const string callerEmail = "xt-emp@hosttests.local";
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);

            // Caller employee lives in tenant B (so [RequireCompleteProfile] resolves them) ...
            var callerUser = DomainSeed.EmployeeUser(callerEmail, tenantId: TenantB);
            ctx.Users.Add(callerUser);
            var callerEmp = DomainSeed.ApprovedEmployee(callerUser, tenantId: TenantB);
            ctx.Employees.Add(callerEmp);
            ctx.EmployeeDocuments.Add(DomainSeed.ActiveDocument(callerEmp.Id, tenantId: TenantB));

            // ... the target invoice belongs to a tenant-A employee.
            var targetUser = DomainSeed.EmployeeUser("xt-target@hosttests.local", tenantId: TenantA);
            ctx.Users.Add(targetUser);
            var targetEmp = DomainSeed.ApprovedEmployee(targetUser, tenantId: TenantA);
            ctx.Employees.Add(targetEmp);
            var period = DomainSeed.PayPeriod(tenantId: TenantA);
            ctx.PayPeriods.Add(period);
            var invoice = DomainSeed.Invoice(targetEmp.Id, period.Id, tenantId: TenantA);
            ctx.EmployeeInvoices.Add(invoice);

            callerEmployeeId = callerEmp.Id;
            invoiceId = invoice.Id;
        });

        var token = TestJwtFactory.Mint(PartnerAudience, "u-xtemp", callerEmail,
            UserProfile.Employee, employeeId: callerEmployeeId, tenantId: TenantB);

        var resp = await PartnerClient(token).GetAsync($"/api/EmployeePayroll/GetInvoiceById/{invoiceId}");

        await HttpAssert.RejectedAsync(resp, BusinessErrorMessage.InvoiceNotFound);
    }

    [Fact]
    public async Task Cross_tenant_take_order_returns_not_found_and_leaves_the_order_unassigned()
    {
        string employeeId = "", orderId = "";
        const string empEmail = "xt-cleaner@hosttests.local";
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);

            var custA = DomainSeed.Customer("xt-ordercust@hosttests.local", tenantId: TenantA);
            ctx.Users.Add(custA);
            var order = DomainSeed.NewOrder(custA.Id, "xt-ordercust@hosttests.local", tenantId: TenantA);
            ctx.Orders.Add(order);

            // Approved cleaner in tenant B.
            var empUser = DomainSeed.EmployeeUser(empEmail, tenantId: TenantB);
            ctx.Users.Add(empUser);
            var emp = DomainSeed.ApprovedEmployee(empUser, tenantId: TenantB);
            ctx.Employees.Add(emp);
            ctx.EmployeeDocuments.Add(DomainSeed.ActiveDocument(emp.Id, tenantId: TenantB));

            employeeId = emp.Id;
            orderId = order.Id;
        });

        var token = TestJwtFactory.Mint(PartnerAudience, "u-xtcleaner", empEmail,
            UserProfile.Employee, employeeId: employeeId, tenantId: TenantB);

        var resp = await PartnerClient(token).PostAsync("/api/Order/TakeOrder",
            JsonContent.Create(new { OrderId = orderId }));

        Assert.NotEqual(System.Net.HttpStatusCode.OK, resp.StatusCode);

        var assigned = await QueryAsync(ctx => ctx.Set<OrderEmployee>()
            .IgnoreQueryFilters().AnyAsync(oe => oe.OrderId == orderId));
        Assert.False(assigned);
    }
}
