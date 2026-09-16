using System.Net.Http.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// Operator and partner resources stay company-scoped. Customer order actions instead admit the proven
/// owner across operators and refuse other customers without revealing the resource.
/// </summary>
public sealed class Ac9CrossTenantRejectionTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string TenantA = HostTestTenants.A;
    private const string TenantB = HostTestTenants.B;

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
    public async Task Cross_market_create_dispute_serves_the_owner_and_rejects_another_customer_without_changes()
    {
        string ownerId = "", outsiderId = "", orderId = "";
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var owner = DomainSeed.Customer("xt-owner@hosttests.local", tenantId: TenantB);
            var outsider = DomainSeed.Customer("xt-outsider@hosttests.local", tenantId: TenantB);
            ctx.Users.AddRange(owner, outsider);
            var order = DomainSeed.NewOrder(owner.Id, owner.Email, tenantId: TenantA, cleaningDateTime: DateTime.UtcNow.AddHours(-2));
            ctx.Orders.Add(order);
            ownerId = owner.Id;
            outsiderId = outsider.Id;
            orderId = order.Id;
        });

        HttpContent Body() => JsonContent.Create(new
        {
            OrderId = orderId,
            Reason = (int)DisputeReason.Other,
            Description = "A problem with the cross-market cleaning",
        });
        var outsiderToken = TestJwtFactory.Mint(CustomerAudience, outsiderId, "xt-outsider@hosttests.local", UserProfile.Customer, tenantId: TenantB);
        await HttpAssert.RejectedAsync(await CustomerClient(outsiderToken).PostAsync("/api/Dispute/Create", Body()), BusinessErrorMessage.OrderNotFound);
        Assert.Equal(0, await QueryAsync(ctx => ctx.Disputes.IgnoreQueryFilters().CountAsync()));

        var ownerToken = TestJwtFactory.Mint(CustomerAudience, ownerId, "xt-owner@hosttests.local", UserProfile.Customer, tenantId: TenantB);
        HttpAssert.IsOk(await CustomerClient(ownerToken).PostAsync("/api/Dispute/Create", Body()));
        var dispute = await QueryAsync(ctx => ctx.Disputes.IgnoreQueryFilters().SingleAsync());
        Assert.Equal(orderId, dispute.OrderId);
        Assert.Equal(ownerId, dispute.UserId);
        Assert.Equal(TenantA, dispute.TenantId);
        var order = await QueryAsync(ctx => ctx.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == orderId));
        Assert.Equal(OrderStatus.New, order.CurrentStatus);
        Assert.Equal(TenantA, order.TenantId);
        var audits = await QueryAsync(ctx => ctx.CustomerActionAudits.IgnoreQueryFilters().ToListAsync());
        Assert.Equal(TenantA, Assert.Single(audits, a => a.Success && a.UserId == ownerId).TenantId);
        Assert.Equal(TenantB, Assert.Single(audits, a => !a.Success && a.UserId == outsiderId).TenantId);
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
