using System.Net.Http.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// The lockout routes on the real hosts (owner ruling 2026-09-28, decisions 11 and 13): the assigned
/// cleaner reports "cannot get in" on either partner host once the entrance photo is up; a colleague not on
/// the job is answered not-found; an administrator then cancels the booking as a customer lockout, which on
/// a signed-in customer's unpaid cash booking opens a lockout receivable for the whole price. Without a
/// report there is nothing to confirm. Anonymous callers are 401 and customers 403 on every route.
/// </summary>
public sealed class LockoutRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string ReportRoute = "/api/Order/ReportLockout";
    private const string ConfirmRoute = "/api/AdminOrder/cancel-lockout";
    private const string CleanerId = "u-lockout-cleaner";
    private const string CleanerEmail = "lockout-cleaner@hosttests.local";
    private const string ColleagueId = "u-lockout-colleague";
    private const string ColleagueEmail = "lockout-colleague@hosttests.local";
    private const string CustomerEmail = "lockout-customer@hosttests.local";

    private sealed record Arranged(string OrderId, string CleanerEmployeeId, string ColleagueEmployeeId);

    private async Task<Arranged> ArrangeAsync()
    {
        Arranged arranged = null!;
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var customer = DomainSeed.Customer(CustomerEmail);
            var cleanerUser = DomainSeed.EmployeeUser(CleanerEmail);
            var colleagueUser = DomainSeed.EmployeeUser(ColleagueEmail);
            ctx.Users.AddRange(customer, cleanerUser, colleagueUser);
            var cleaner = DomainSeed.ApprovedEmployee(cleanerUser);
            var colleague = DomainSeed.ApprovedEmployee(colleagueUser);
            ctx.Employees.AddRange(cleaner, colleague);
            ctx.EmployeeDocuments.AddRange(DomainSeed.ActiveDocument(cleaner.Id), DomainSeed.ActiveDocument(colleague.Id));

            var order = DomainSeed.NewOrder(
                customer.Id, CustomerEmail, HostTestTenants.Default, cleaningDateTime: DateTime.UtcNow.AddMinutes(-30));
            DomainSeed.ConfirmAndAssign(order, cleaner);

            ctx.Orders.Add(order);
            ctx.Set<OrderPhoto>().Add(DomainSeed.OrderPhoto(
                order.Id, cleaner.Id, PhotoType.Entrance, "entrance.jpg", HostTestTenants.Default));

            arranged = new Arranged(order.Id, cleaner.Id, colleague.Id);
        });
        return arranged;
    }

    private static object ReportBody(string orderId) => new { orderId, callAttempts = "Called at 9:05 and 9:10, no answer." };

    private static object ConfirmBody(string orderId) => new { orderId };

    private Task<Order> ReadOrderAsync(string orderId) =>
        QueryAsync(ctx => ctx.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == orderId));

    [Fact]
    public async Task Anonymous_Is_401_On_Every_Lockout_Route()
    {
        HttpAssert.IsUnauthorized(await PartnerClientAnonymous().PostAsJsonAsync(ReportRoute, ReportBody("any")));
        HttpAssert.IsUnauthorized(await MobileClientAnonymous().PostAsJsonAsync(ReportRoute, ReportBody("any")));
        HttpAssert.IsUnauthorized(await AdminClientAnonymous().PostAsJsonAsync(ConfirmRoute, ConfirmBody("any")));
    }

    [Fact]
    public async Task A_Customer_Token_Is_403_On_The_Report_And_The_Confirmation()
    {
        var partner = PartnerClient(TestJwtFactory.Mint(PartnerAudience, "u-cust", CustomerEmail, UserProfile.Customer));
        var admin = AdminClient(TestJwtFactory.Mint(AdminAudience, "u-cust", CustomerEmail, UserProfile.Customer));

        HttpAssert.IsForbidden(await partner.PostAsJsonAsync(ReportRoute, ReportBody("any")));
        HttpAssert.IsForbidden(await admin.PostAsJsonAsync(ConfirmRoute, ConfirmBody("any")));
    }

    [Fact]
    public async Task The_Assigned_Cleaner_Reports_And_An_Administrator_Cancels_It_As_A_Lockout()
    {
        var arranged = await ArrangeAsync();
        var cleaner = MobileClient(TestJwtFactory.Mint(
            MobileAudience, CleanerId, CleanerEmail, UserProfile.Employee, employeeId: arranged.CleanerEmployeeId));

        HttpAssert.IsOk(await cleaner.PostAsJsonAsync(ReportRoute, ReportBody(arranged.OrderId)));

        var reported = await ReadOrderAsync(arranged.OrderId);
        Assert.NotNull(reported.LockoutReportedAt);
        Assert.Equal(arranged.CleanerEmployeeId, reported.LockoutReportedByEmployeeId);

        var admin = AdminClient(TestJwtFactory.Mint(AdminAudience, "u-lockout-admin", "lockout-admin@hosttests.local", UserProfile.Administrator));
        HttpAssert.IsOk(await admin.PostAsJsonAsync(ConfirmRoute, ConfirmBody(arranged.OrderId)));

        var cancelled = await ReadOrderAsync(arranged.OrderId);
        Assert.Equal(OrderStatus.Cancelled, cancelled.CurrentStatus);
        Assert.Equal(OrderCancellationReasons.CustomerLockout, cancelled.CancellationReason);
        Assert.Equal(1m, cancelled.CancellationFeeRate);
        var receivable = await QueryAsync(ctx => ctx.Receivables.IgnoreQueryFilters().SingleAsync(r => r.OrderId == arranged.OrderId));
        Assert.Equal(ReceivableKind.Lockout, receivable.Kind);
        Assert.Equal(1500m, receivable.Amount);
        Assert.Equal(HostTestTenants.Default, receivable.TenantId);
    }

    [Fact]
    public async Task A_Colleague_Not_On_The_Job_Is_Answered_Not_Found()
    {
        var arranged = await ArrangeAsync();
        var colleague = PartnerClient(TestJwtFactory.Mint(
            PartnerAudience, ColleagueId, ColleagueEmail, UserProfile.Employee, employeeId: arranged.ColleagueEmployeeId));

        var response = await colleague.PostAsJsonAsync(ReportRoute, ReportBody(arranged.OrderId));

        await HttpAssert.RejectedAsync(response, BusinessErrorMessage.OrderNotFound);
        Assert.Null((await ReadOrderAsync(arranged.OrderId)).LockoutReportedAt);
    }

    [Fact]
    public async Task Without_A_Report_The_Administrator_Has_Nothing_To_Confirm()
    {
        var arranged = await ArrangeAsync();
        var admin = AdminClient(TestJwtFactory.Mint(AdminAudience, "u-lockout-admin", "lockout-admin@hosttests.local", UserProfile.Administrator));

        var response = await admin.PostAsJsonAsync(ConfirmRoute, ConfirmBody(arranged.OrderId));

        await HttpAssert.RejectedAsync(response, BusinessErrorMessage.LockoutNotReported);
        Assert.Equal(OrderStatus.Confirmed, (await ReadOrderAsync(arranged.OrderId)).CurrentStatus);
    }
}
