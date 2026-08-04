using System.Net.Http.Json;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// ADR-0034 D7 / verification 6(c) — deploy day. There is no backfill, so every cleaner already on the
/// platform has <c>HasPayoutDetails = false</c> and no <c>EmployeePayoutDetails</c> row the moment this
/// release ships. <c>[RequireCompleteProfile]</c> is class-level on the partner host's Order, Payroll,
/// Dashboard and Dispute controllers, so a gate that read the flag alone would return 403 to all of them
/// — an outage disguised as a data-model change.
///
/// <para>Run through the host so the aggregate is loaded by <c>EmployeeRepository.GetByUserEmailAsync</c>
/// with its real, hand-written include list. A unit test on a constructed <c>Employee</c> does not
/// discharge this: the whole failure class here is load-order dependence.</para>
/// </summary>
public sealed class PayoutGateDeployDayTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    [Fact]
    public async Task A_Cleaner_Whose_Payout_Details_Predate_The_Payout_Record_Keeps_Working()
    {
        string employeeId = "", orderId = "";
        const string employeeEmail = "legacy.payout@hosttests.local";

        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);

            var customerUser = DomainSeed.Customer("legacycust@hosttests.local");
            var employeeUser = DomainSeed.EmployeeUser(employeeEmail);
            ctx.Users.AddRange(customerUser, employeeUser);

            var employee = DomainSeed.LegacyPayoutApprovedEmployee(employeeUser);
            ctx.Employees.Add(employee);
            ctx.EmployeeDocuments.Add(DomainSeed.ActiveDocument(employee.Id));

            var order = DomainSeed.NewOrder(customerUser.Id, "legacycust@hosttests.local");
            ctx.Orders.Add(order);

            employeeId = employee.Id;
            orderId = order.Id;
        });

        var stored = await QueryAsync(ctx => ctx.Employees
            .IgnoreQueryFilters()
            .Where(e => e.Id == employeeId)
            .Select(e => new { e.HasPayoutDetails, e.IBAN })
            .SingleAsync());
        Assert.False(stored.HasPayoutDetails);
        Assert.False(string.IsNullOrEmpty(stored.IBAN));

        var payoutRows = await QueryAsync(ctx => ctx.EmployeePayoutDetails
            .IgnoreQueryFilters()
            .CountAsync(p => p.EmployeeId == employeeId));
        Assert.Equal(0, payoutRows);

        var token = TestJwtFactory.Mint(
            PartnerAudience, "u-legacy", employeeEmail, UserProfile.Employee, employeeId: employeeId);

        var response = await PartnerClient(token)
            .PostAsync("/api/Order/TakeOrder", JsonContent.Create(new { OrderId = orderId }));

        HttpAssert.IsOk(response);

        var assigned = await QueryAsync(ctx => ctx.Set<OrderEmployee>()
            .IgnoreQueryFilters().AnyAsync(oe => oe.OrderId == orderId && oe.EmployeeId == employeeId));
        Assert.True(assigned);
    }
}
