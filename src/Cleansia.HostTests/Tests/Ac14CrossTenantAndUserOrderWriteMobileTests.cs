using System.Net.Http.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// Order lifecycle write-path isolation (testing.md must-cover #5, S3/S8) on the MOBILE partner host —
/// the host the ticket requires beyond Ac9's Partner-host TakeOrder. Proving the boundary holds on the
/// Mobile host exercises its OWN auth registration (ADR-0001 D4), not just the Partner API.
/// <list type="bullet">
///   <item><b>cross-TENANT TakeOrder</b>: an approved cleaner in tenant B against an order in tenant A
///   → the tenant filter hides the order → rejection, and the order stays unassigned;</item>
///   <item><b>cross-USER (same tenant) StartOrder</b>: an order Confirmed and assigned to cleaner X;
///   cleaner Y (same tenant, approved, NOT assigned) tries to start it → <c>EmployeeNotAssignedToOrder</c>,
///   and the order does not advance to InProgress;</item>
///   <item><b>legitimate TakeOrder</b>: an approved in-tenant cleaner takes an unassigned order → 200
///   and the assignment is recorded (the rejection is the boundary, not a broken endpoint). TakeOrder
///   is used for the success leg because the StartOrder happy path 500s on a handler/validator load
///   divergence unrelated to authz — see the productionBugsFound finding.</item>
/// </list>
/// Both tenants carry NON-NULL distinct tenant_id claims so the multi-tenant filter branch is under test.
/// </summary>
public sealed class Ac14CrossTenantAndUserOrderWriteMobileTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string TenantA = "tenant-A";
    private const string TenantB = "tenant-B";

    [Fact]
    public async Task Cross_tenant_take_order_on_mobile_host_is_rejected_and_leaves_the_order_unassigned()
    {
        string employeeId = "", orderId = "";
        const string empEmail = "m-xt-cleaner@hosttests.local";
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);

            var cust = DomainSeed.Customer("m-xt-cust@hosttests.local", tenantId: TenantA);
            ctx.Users.Add(cust);
            var order = DomainSeed.NewOrder(cust.Id, "m-xt-cust@hosttests.local", tenantId: TenantA);
            ctx.Orders.Add(order);

            var empUser = DomainSeed.EmployeeUser(empEmail, tenantId: TenantB);
            ctx.Users.Add(empUser);
            var emp = DomainSeed.ApprovedEmployee(empUser, tenantId: TenantB);
            ctx.Employees.Add(emp);
            ctx.EmployeeDocuments.Add(DomainSeed.ActiveDocument(emp.Id, tenantId: TenantB));

            employeeId = emp.Id;
            orderId = order.Id;
        });

        var token = TestJwtFactory.Mint(MobileAudience, "u-m-xtcleaner", empEmail,
            UserProfile.Employee, employeeId: employeeId, tenantId: TenantB);

        var resp = await MobileClient(token).PostAsync("/api/Order/TakeOrder",
            JsonContent.Create(new { OrderId = orderId }));

        await HttpAssert.RejectedAsync(resp, BusinessErrorMessage.OrderNotFound);

        var assigned = await QueryAsync(ctx => ctx.Set<OrderEmployee>()
            .IgnoreQueryFilters().AnyAsync(oe => oe.OrderId == orderId));
        Assert.False(assigned);
    }

    private sealed record AssignedOrder(
        string AssigneeId, string AssigneeEmail, string OutsiderId, string OutsiderEmail, string OrderId);

    private async Task<AssignedOrder> ArrangeConfirmedOrderAssignedToXInTenantAAsync()
    {
        string assigneeId = "", outsiderId = "", orderId = "";
        const string assigneeEmail = "m-assignee@hosttests.local";
        const string outsiderEmail = "m-outsider@hosttests.local";
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);

            var cust = DomainSeed.Customer("m-cu-cust@hosttests.local", tenantId: TenantA);
            ctx.Users.Add(cust);

            var assigneeUser = DomainSeed.EmployeeUser(assigneeEmail, tenantId: TenantA);
            ctx.Users.Add(assigneeUser);
            var assignee = DomainSeed.ApprovedEmployee(assigneeUser, tenantId: TenantA);
            ctx.Employees.Add(assignee);
            ctx.EmployeeDocuments.Add(DomainSeed.ActiveDocument(assignee.Id, tenantId: TenantA));

            var outsiderUser = DomainSeed.EmployeeUser(outsiderEmail, tenantId: TenantA);
            ctx.Users.Add(outsiderUser);
            var outsider = DomainSeed.ApprovedEmployee(outsiderUser, tenantId: TenantA);
            ctx.Employees.Add(outsider);
            ctx.EmployeeDocuments.Add(DomainSeed.ActiveDocument(outsider.Id, tenantId: TenantA));

            var order = DomainSeed.NewOrder(cust.Id, "m-cu-cust@hosttests.local", tenantId: TenantA);
            DomainSeed.ConfirmAndAssign(order, assignee);
            ctx.Orders.Add(order);

            assigneeId = assignee.Id;
            outsiderId = outsider.Id;
            orderId = order.Id;
        });
        return new AssignedOrder(assigneeId, assigneeEmail, outsiderId, outsiderEmail, orderId);
    }

    [Fact]
    public async Task Cross_user_same_tenant_start_order_on_mobile_host_is_rejected_and_does_not_advance_the_order()
    {
        var a = await ArrangeConfirmedOrderAssignedToXInTenantAAsync();
        // Outsider: same tenant, approved, but NOT the assigned cleaner.
        var token = TestJwtFactory.Mint(MobileAudience, "u-m-outsider", a.OutsiderEmail,
            UserProfile.Employee, employeeId: a.OutsiderId, tenantId: TenantA);

        var resp = await MobileClient(token).PostAsync("/api/Order/StartOrder",
            JsonContent.Create(new { OrderId = a.OrderId }));

        await HttpAssert.RejectedAsync(resp, BusinessErrorMessage.EmployeeNotAssignedToOrder);

        Assert.False(await OrderInProgressAsync(a.OrderId));
    }

    /// <summary>The in-tenant legitimate-success leg for the Mobile order write path: an approved,
    /// in-tenant cleaner TAKES an unassigned order on the Mobile host → 200 and the assignment is
    /// recorded. (TakeOrder is the create/mutate happy path proven reachable end-to-end on the Mobile
    /// host; the StartOrder happy path is not asserted here because its handler dereferences
    /// infrastructure the host harness does not fully provision — see productionBugsFound note. The
    /// cross-user StartOrder REJECTION above is reached before that handler code, so it is exercised.)</summary>
    [Fact]
    public async Task In_tenant_approved_cleaner_takes_an_order_on_mobile_host_and_the_assignment_is_recorded()
    {
        string employeeId = "", orderId = "";
        const string empEmail = "m-legit-cleaner@hosttests.local";
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);

            var cust = DomainSeed.Customer("m-legit-cust@hosttests.local", tenantId: TenantA);
            ctx.Users.Add(cust);
            var order = DomainSeed.NewOrder(cust.Id, "m-legit-cust@hosttests.local", tenantId: TenantA);
            ctx.Orders.Add(order);

            var empUser = DomainSeed.EmployeeUser(empEmail, tenantId: TenantA);
            ctx.Users.Add(empUser);
            var emp = DomainSeed.ApprovedEmployee(empUser, tenantId: TenantA);
            ctx.Employees.Add(emp);
            ctx.EmployeeDocuments.Add(DomainSeed.ActiveDocument(emp.Id, tenantId: TenantA));

            employeeId = emp.Id;
            orderId = order.Id;
        });

        var token = TestJwtFactory.Mint(MobileAudience, "u-m-legit", empEmail,
            UserProfile.Employee, employeeId: employeeId, tenantId: TenantA);

        var resp = await MobileClient(token).PostAsync("/api/Order/TakeOrder",
            JsonContent.Create(new { OrderId = orderId }));

        HttpAssert.IsOk(resp);

        var assigned = await QueryAsync(ctx => ctx.Set<OrderEmployee>()
            .IgnoreQueryFilters().AnyAsync(oe => oe.OrderId == orderId && oe.EmployeeId == employeeId));
        Assert.True(assigned);
    }

    private async Task<bool> OrderInProgressAsync(string orderId)
    {
        var latest = await QueryAsync(ctx => ctx.Set<OrderStatusTrack>()
            .IgnoreQueryFilters()
            .Where(h => h.OrderId == orderId)
            .OrderByDescending(h => h.CreatedOn)
            .Select(h => (OrderStatus?)h.Status)
            .FirstOrDefaultAsync());
        return latest == OrderStatus.InProgress;
    }
}
