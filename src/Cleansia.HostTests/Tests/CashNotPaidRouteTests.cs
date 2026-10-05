using System.Net.Http.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Contracts;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// The "customer did not pay" route on the real hosts (owner ruling 2026-10-06): the assigned cleaner of a
/// cash booking in progress reports it on either partner host, which completes the job and opens the unpaid
/// price as a debt under the order's company; a colleague not on the job is answered not-found and changes
/// nothing. Anonymous callers are 401 and customers 403, and neither the customer nor the admin host has
/// the route.
/// </summary>
public sealed class CashNotPaidRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string Route = "/api/Order/ReportCashNotPaid";
    private const string CleanerId = "u-door-cleaner";
    private const string CleanerEmail = "door-cleaner@hosttests.local";
    private const string ColleagueId = "u-door-colleague";
    private const string ColleagueEmail = "door-colleague@hosttests.local";
    private const string CustomerEmail = "door-customer@hosttests.local";

    private sealed record Arranged(string OrderId, string CleanerEmployeeId, string ColleagueEmployeeId);

    private async Task<Arranged> ArrangeAsync()
    {
        Arranged arranged = null!;
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var (contract, _) = await DomainSeed.WorkContractInForceAsync(ctx);
            var customer = DomainSeed.Customer(CustomerEmail);
            var cleanerUser = DomainSeed.EmployeeUser(CleanerEmail);
            var colleagueUser = DomainSeed.EmployeeUser(ColleagueEmail);
            ctx.Users.AddRange(customer, cleanerUser, colleagueUser);
            var cleaner = DomainSeed.ApprovedEmployee(cleanerUser);
            var colleague = DomainSeed.ApprovedEmployee(colleagueUser);
            ctx.Employees.AddRange(cleaner, colleague);
            ctx.EmployeeDocuments.AddRange(DomainSeed.ActiveDocument(cleaner.Id), DomainSeed.ActiveDocument(colleague.Id));

            var order = DomainSeed.NewOrder(
                customer.Id, CustomerEmail, HostTestTenants.Default,
                cleaningDateTime: DateTime.UtcNow.AddHours(-2), workContract: contract);
            DomainSeed.ConfirmAndAssign(order, cleaner);
            var now = DateTimeOffset.UtcNow;
            foreach (var track in order.OrderStatusHistory)
            {
                track.Created("seed", track.Status == OrderStatus.New ? now.AddMinutes(-15) : now.AddMinutes(-10));
            }

            var started = OrderStatusTrack.Create(OrderStatus.InProgress, order);
            started.Created("seed", now.AddMinutes(-5));
            started.TenantId = HostTestTenants.Default;
            order.AddOrderStatus(started);
            ctx.Orders.Add(order);

            var seat = order.AssignedEmployees.Single();
            var acceptance = WorkContractAcceptance.Create(
                order.Id, seat.Id, cleaner.Id, contract.TextFor(DomainSeed.LanguageCode)!, contract.Version, "cleansia.partner",
                ipAddress: null, deviceLabel: null, deviceId: null, factsJson: "{}");
            acceptance.TenantId = HostTestTenants.Default;
            ctx.WorkContractAcceptances.Add(acceptance);
            ctx.Set<OrderPhoto>().Add(DomainSeed.OrderPhoto(
                order.Id, cleaner.Id, PhotoType.After, "after.jpg", HostTestTenants.Default));

            arranged = new Arranged(order.Id, cleaner.Id, colleague.Id);
        });
        return arranged;
    }

    private static object Body(string orderId) => new { orderId };

    [Fact]
    public async Task Anonymous_Is_401_On_Both_Partner_Hosts()
    {
        HttpAssert.IsUnauthorized(await PartnerClientAnonymous().PostAsJsonAsync(Route, Body("any")));
        HttpAssert.IsUnauthorized(await MobileClientAnonymous().PostAsJsonAsync(Route, Body("any")));
    }

    [Fact]
    public async Task A_Customer_Token_Is_403()
    {
        var partner = PartnerClient(TestJwtFactory.Mint(PartnerAudience, "u-cust", CustomerEmail, UserProfile.Customer));

        HttpAssert.IsForbidden(await partner.PostAsJsonAsync(Route, Body("any")));
    }

    [Fact]
    public async Task Neither_The_Customer_Nor_The_Admin_Host_Has_The_Route()
    {
        var customer = CustomerClient(TestJwtFactory.Mint(CustomerAudience, "u-cust", CustomerEmail, UserProfile.Customer));
        var admin = AdminClient(TestJwtFactory.Mint(AdminAudience, "u-door-admin", "door-admin@hosttests.local", UserProfile.Administrator));

        HttpAssert.IsNotFound(await customer.PostAsJsonAsync(Route, Body("any")));
        HttpAssert.IsNotFound(await admin.PostAsJsonAsync(Route, Body("any")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_Assigned_Cleaner_Reports_It_And_The_Price_Is_Owed_Under_The_Orders_Company(bool mobile)
    {
        var arranged = await ArrangeAsync();
        var cleaner = mobile
            ? MobileClient(TestJwtFactory.Mint(MobileAudience, CleanerId, CleanerEmail, UserProfile.Employee, employeeId: arranged.CleanerEmployeeId))
            : PartnerClient(TestJwtFactory.Mint(PartnerAudience, CleanerId, CleanerEmail, UserProfile.Employee, employeeId: arranged.CleanerEmployeeId));

        HttpAssert.IsOk(await cleaner.PostAsJsonAsync(Route, Body(arranged.OrderId)));

        var order = await QueryAsync(ctx => ctx.Orders.IgnoreQueryFilters().Include(o => o.OrderStatusHistory)
            .SingleAsync(o => o.Id == arranged.OrderId));
        Assert.Equal((OrderStatus.Completed, PaymentStatus.Pending), (order.CurrentStatus, order.PaymentStatus));
        var debt = await QueryAsync(ctx => ctx.Receivables.IgnoreQueryFilters().SingleAsync(r => r.OrderId == arranged.OrderId));
        Assert.Equal(
            (ReceivableKind.UnpaidCash, ReceivableStatus.Open, 1500m, HostTestTenants.Default),
            (debt.Kind, debt.Status, debt.Amount, debt.TenantId));
    }

    [Fact]
    public async Task A_Colleague_Not_On_The_Job_Is_Answered_Not_Found_And_Nothing_Changes()
    {
        var arranged = await ArrangeAsync();
        var colleague = PartnerClient(TestJwtFactory.Mint(
            PartnerAudience, ColleagueId, ColleagueEmail, UserProfile.Employee, employeeId: arranged.ColleagueEmployeeId));

        var response = await colleague.PostAsJsonAsync(Route, Body(arranged.OrderId));

        await HttpAssert.RejectedAsync(response, BusinessErrorMessage.OrderNotFound);
        var order = await QueryAsync(ctx => ctx.Orders.IgnoreQueryFilters().Include(o => o.OrderStatusHistory)
            .SingleAsync(o => o.Id == arranged.OrderId));
        Assert.Equal(OrderStatus.InProgress, order.CurrentStatus);
        Assert.False(await QueryAsync(ctx => ctx.Receivables.IgnoreQueryFilters().AnyAsync(r => r.OrderId == arranged.OrderId)));
    }
}
