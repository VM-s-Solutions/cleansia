using System.Net;
using System.Net.Http.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

public sealed class MarkMyPartDoneRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string Route = "/api/Order/MarkMyPartDone";

    private sealed record Crew(string OrderId, Employee Assignee, Employee Crewmate, Employee Outsider, Employee OtherTenant);

    private async Task<Crew> ArrangeAsync(OrderStatus status = OrderStatus.InProgress)
    {
        Crew? crew = null;
        await SeedAsync(async context =>
        {
            await DomainSeed.EnsureReferenceDataAsync(context);
            var customer = DomainSeed.Customer("mark-customer@hosttests.local", HostTestTenants.A);
            context.Users.Add(customer);

            Employee Cleaner(string email, string tenantId)
            {
                var user = DomainSeed.EmployeeUser(email, tenantId);
                var employee = DomainSeed.ApprovedEmployee(user, tenantId);
                context.Users.Add(user);
                context.Employees.Add(employee);
                context.EmployeeDocuments.Add(DomainSeed.ActiveDocument(employee.Id, tenantId));
                return employee;
            }

            var assignee = Cleaner("mark-assignee@hosttests.local", HostTestTenants.A);
            var crewmate = Cleaner("mark-crewmate@hosttests.local", HostTestTenants.A);
            var outsider = Cleaner("mark-outsider@hosttests.local", HostTestTenants.A);
            var otherTenant = Cleaner("mark-other-company@hosttests.local", HostTestTenants.B);
            var order = DomainSeed.NewOrder(customer.Id, customer.Email, HostTestTenants.A,
                cleaningDateTime: DateTime.UtcNow.AddMinutes(-30));
            order.SetMaxEmployees(2);
            DomainSeed.ConfirmAndAssign(order, assignee);
            order.AddAssignedEmployee(OrderEmployee.Create(order, crewmate));
            if (status == OrderStatus.InProgress)
            {
                var started = OrderStatusTrack.Create(OrderStatus.InProgress, order);
                started.TenantId = HostTestTenants.A;
                order.AddOrderStatus(started);
            }

            context.Orders.Add(order);
            crew = new Crew(order.Id, assignee, crewmate, outsider, otherTenant);
        });
        return crew!;
    }

    private HttpClient Client(bool mobile, Employee employee)
    {
        var token = TestJwtFactory.Mint(mobile ? MobileAudience : PartnerAudience,
            employee.UserId, employee.User!.Email, UserProfile.Employee, employee.Id, employee.TenantId);
        return mobile ? MobileClient(token) : PartnerClient(token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Assigned_Cleaner_Marks_Only_Their_Own_Seat_And_A_Repeat_Keeps_The_First_Stamp(bool mobile)
    {
        var crew = await ArrangeAsync();
        using var client = Client(mobile, crew.Assignee);

        var first = await client.PostAsJsonAsync(Route, new { OrderId = crew.OrderId });
        HttpAssert.IsOk(first);
        var firstMark = await first.Content.ReadFromJsonAsync<MarkMyPartDone.Response>();
        Assert.NotNull(firstMark);
        Assert.Equal(crew.OrderId, firstMark.OrderId);
        Assert.False(firstMark.CrewDone);

        var repeated = await client.PostAsJsonAsync(Route, new { OrderId = crew.OrderId });
        HttpAssert.IsOk(repeated);
        var repeatMark = await repeated.Content.ReadFromJsonAsync<MarkMyPartDone.Response>();
        Assert.NotNull(repeatMark);
        Assert.Equal(firstMark.OrderId, repeatMark.OrderId);
        Assert.Equal(firstMark.CrewDone, repeatMark.CrewDone);
        Assert.Equal(firstMark.CompletionConfirmedAt.Ticks / TimeSpan.TicksPerMicrosecond,
            repeatMark.CompletionConfirmedAt.Ticks / TimeSpan.TicksPerMicrosecond);

        var seats = await QueryAsync(context => context.Set<OrderEmployee>()
            .Where(s => s.OrderId == crew.OrderId).ToListAsync());
        Assert.Equal(repeatMark.CompletionConfirmedAt, seats.Single(s => s.EmployeeId == crew.Assignee.Id).CompletionConfirmedAt);
        Assert.Null(seats.Single(s => s.EmployeeId == crew.Crewmate.Id).CompletionConfirmedAt);
        Assert.Equal(OrderStatus.InProgress, await QueryAsync(context => context.Orders.IgnoreQueryFilters()
            .Where(o => o.Id == crew.OrderId).Select(o => o.CurrentStatus).SingleAsync()));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task An_Unassigned_Or_Other_Company_Cleaner_Is_Refused_Without_Stamping_Any_Seat(bool mobile, bool otherCompany)
    {
        var crew = await ArrangeAsync(OrderStatus.Confirmed);
        using var client = Client(mobile, otherCompany ? crew.OtherTenant : crew.Outsider);

        var response = await client.PostAsJsonAsync(Route, new { OrderId = crew.OrderId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await HttpAssert.AssertBusinessErrorAsync(response, otherCompany
            ? BusinessErrorMessage.OrderNotFound
            : BusinessErrorMessage.EmployeeNotAssignedToOrder);
        Assert.DoesNotContain(BusinessErrorMessage.OrderNotInProgress, await response.Content.ReadAsStringAsync());
        Assert.False(await QueryAsync(context => context.Set<OrderEmployee>()
            .AnyAsync(s => s.OrderId == crew.OrderId && s.CompletionConfirmedAt != null)));
    }
}
