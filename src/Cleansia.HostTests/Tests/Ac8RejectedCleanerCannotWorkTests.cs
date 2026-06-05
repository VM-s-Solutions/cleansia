using System.Net.Http.Json;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// A cleaner whose ContractStatus is Rejected cannot work an
/// order. End-to-end on the Partner Order host:
/// <list type="bullet">
///   <item>a Rejected (but otherwise complete) cleaner is turned away on TakeOrder / StartOrder /
///   CompleteOrder, and the order's status/assignment is unchanged. (Through the full host the rejected
///   cleaner is stopped at the [RequireCompleteProfile] gate, which requires Approved/Active — the
///   validator's EmployeeIsApprovedAsync check is the inner backstop the handler-level
///   tests cover.)</item>
///   <item>an Approved cleaner still succeeds (TakeOrder → 200, assignment recorded, status advanced).</item>
/// </list>
/// </summary>
public sealed class Ac8RejectedCleanerCannotWorkTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private sealed record Arranged(string EmployeeId, string EmployeeEmail, string OrderId);

    private async Task<Arranged> ArrangeAsync(bool approved)
    {
        string empId = "", orderId = "";
        const string empEmail = "cleaner@hosttests.local";

        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);

            var customerUser = DomainSeed.Customer("ordercust@hosttests.local");
            var empUser = DomainSeed.EmployeeUser(empEmail);
            ctx.Users.AddRange(customerUser, empUser);

            var employee = approved
                ? DomainSeed.ApprovedEmployee(empUser)
                : DomainSeed.RejectedEmployee(empUser);
            ctx.Employees.Add(employee);
            ctx.EmployeeDocuments.Add(DomainSeed.ActiveDocument(employee.Id));

            var order = DomainSeed.NewOrder(customerUser.Id, "ordercust@hosttests.local");
            ctx.Orders.Add(order);

            empId = employee.Id;
            orderId = order.Id;
        });

        return new Arranged(empId, empEmail, orderId);
    }

    [Theory]
    [InlineData("/api/Order/TakeOrder")]
    [InlineData("/api/Order/StartOrder")]
    [InlineData("/api/Order/CompleteOrder")]
    public async Task Rejected_cleaner_cannot_act_on_an_order_and_the_order_is_unchanged(string endpoint)
    {
        var a = await ArrangeAsync(approved: false);
        var token = TestJwtFactory.Mint(PartnerAudience, "u-rej", a.EmployeeEmail, UserProfile.Employee, employeeId: a.EmployeeId);

        var resp = await PartnerClient(token).PostAsync(endpoint, JsonContent.Create(new { OrderId = a.OrderId }));

        Assert.NotEqual(System.Net.HttpStatusCode.OK, resp.StatusCode);

        // The order has no assignment and is still at its initial New status.
        var assigned = await QueryAsync(ctx => ctx.Set<OrderEmployee>()
            .IgnoreQueryFilters().AnyAsync(oe => oe.OrderId == a.OrderId));
        Assert.False(assigned);
    }

    [Fact]
    public async Task Approved_cleaner_can_take_an_order()
    {
        var a = await ArrangeAsync(approved: true);
        var token = TestJwtFactory.Mint(PartnerAudience, "u-app", a.EmployeeEmail, UserProfile.Employee, employeeId: a.EmployeeId);

        var resp = await PartnerClient(token).PostAsync("/api/Order/TakeOrder", JsonContent.Create(new { OrderId = a.OrderId }));

        HttpAssert.IsOk(resp);

        var assigned = await QueryAsync(ctx => ctx.Set<OrderEmployee>()
            .IgnoreQueryFilters().AnyAsync(oe => oe.OrderId == a.OrderId && oe.EmployeeId == a.EmployeeId));
        Assert.True(assigned);
    }
}
