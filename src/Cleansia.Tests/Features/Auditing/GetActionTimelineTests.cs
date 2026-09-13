using System.Reflection;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auditing;
using Cleansia.Core.AppServices.Features.Auditing.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// ADR-0062 D6 — the three-source timeline. The validator admits exactly one key (a user, or a resource
/// pair); the page is one <c>OccurredOn DESC</c> order across the customer, admin and employee tables;
/// a user-keyed read reaches the admin and employee rows through the ids of what the user owns; and a
/// guest act is reachable by resource only. The real-Postgres leg (tenant filter, SQL translation of
/// the three arms) is <c>Cleansia.IntegrationTests/Features/Auditing/GetActionTimelineTests</c>.
/// </summary>
public class GetActionTimelineTests
{
    private const string UserId = "user-1";
    private const string OrderId = "order-1";

    private static readonly DateTimeOffset T0 = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<ICustomerActionAuditRepository> _customer = new();
    private readonly Mock<IAdminActionAuditRepository> _admin = new();
    private readonly Mock<IEmployeeActionAuditRepository> _employee = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IDisputeRepository> _disputes = new();
    private readonly Mock<IUserMembershipRepository> _memberships = new();

    public GetActionTimelineTests()
    {
        Seed(Array.Empty<CustomerActionAudit>(), Array.Empty<AdminActionAudit>(), Array.Empty<EmployeeActionAudit>());
        _orders.Setup(r => r.GetQueryable()).Returns(Array.Empty<Order>().AsQueryable().BuildMock());
        _disputes.Setup(r => r.GetQueryable()).Returns(Array.Empty<Dispute>().AsQueryable().BuildMock());
        _memberships.Setup(r => r.GetQueryable()).Returns(Array.Empty<UserMembership>().AsQueryable().BuildMock());
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("", "", "")]
    [InlineData("user-1", "Order", "order-1")]
    [InlineData("user-1", "Order", null)]
    [InlineData("user-1", null, "order-1")]
    [InlineData(null, "Order", null)]
    [InlineData(null, null, "order-1")]
    public async Task Validator_Refuses_Anything_But_Exactly_One_Key(string? userId, string? type, string? id)
    {
        var result = await new GetActionTimeline.Validator()
            .ValidateAsync(new GetActionTimeline.Request { UserId = userId, ResourceType = type, ResourceId = id });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.TimelineFilterRequired);
    }

    [Theory]
    [InlineData("user-1", null, null)]
    [InlineData(null, "Order", "order-1")]
    public async Task Validator_Accepts_A_User_Or_A_Resource_Pair(string? userId, string? type, string? id)
    {
        var result = await new GetActionTimeline.Validator()
            .ValidateAsync(new GetActionTimeline.Request { UserId = userId, ResourceType = type, ResourceId = id });

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_Refuses_A_Page_Larger_Than_MaxLimit_As_Page_Size_Exceeded()
    {
        var result = await new GetActionTimeline.Validator()
            .ValidateAsync(new GetActionTimeline.Request { UserId = UserId, Limit = GetActionTimeline.MaxLimit + 1 });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.PageSizeExceeded);
    }

    [Fact]
    public async Task Validator_Accepts_A_Page_Of_Exactly_MaxLimit()
    {
        var result = await new GetActionTimeline.Validator()
            .ValidateAsync(new GetActionTimeline.Request { UserId = UserId, Limit = GetActionTimeline.MaxLimit });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Page_Orders_Newest_First_Across_Sources_And_Breaks_Ties_By_Id()
    {
        var entries = new[]
        {
            Entry(TimelineSource.Employee, "e-1", T0.AddMinutes(1)),
            Entry(TimelineSource.Customer, "c-2", T0.AddMinutes(3)),
            Entry(TimelineSource.Admin, "a-1", T0.AddMinutes(3)),
            Entry(TimelineSource.Customer, "c-1", T0),
        };

        var page = GetActionTimeline.Page(entries, offset: 0, limit: 10);

        Assert.Equal(new[] { "a-1", "c-2", "e-1", "c-1" }, page.Select(e => e.Id));
    }

    [Fact]
    public void Page_Applies_Offset_And_Limit_After_The_Merge()
    {
        var entries = Enumerable.Range(0, 7)
            .Select(i => Entry((TimelineSource)(i % 3 + 1), $"id-{i}", T0.AddMinutes(i)))
            .ToArray();

        var second = GetActionTimeline.Page(entries, offset: 3, limit: 3);

        Assert.Equal(new[] { "id-3", "id-2", "id-1" }, second.Select(e => e.Id));
    }

    [Fact]
    public void Employee_Actions_Map_To_The_Dotted_Labels()
    {
        Assert.Equal("employee.order.cover_requested", GetActionTimeline.EmployeeActionLabel(EmployeeAuditAction.CoverRequested));
        Assert.Equal("employee.order.dropped", GetActionTimeline.EmployeeActionLabel(EmployeeAuditAction.OrderDropped));
    }

    [Fact]
    public void Every_Employee_Action_Has_A_Named_Label_And_None_Is_Synthesised()
    {
        foreach (var action in Enum.GetValues<EmployeeAuditAction>())
        {
            Assert.StartsWith("employee.", GetActionTimeline.EmployeeActionLabel(action), StringComparison.Ordinal);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => GetActionTimeline.EmployeeActionLabel((EmployeeAuditAction)int.MaxValue));
    }

    [Fact]
    public async Task ByUser_Merges_The_Customers_Rows_With_Admin_And_Employee_Rows_On_Their_Order()
    {
        SeedUserOwns(orderIds: [OrderId]);
        Seed(
            [CustomerRow("c-1", UserId, OrderId, T0.AddMinutes(1)), CustomerRow("c-2", UserId, OrderId, T0.AddMinutes(4))],
            [AdminRow("a-1", "Order", OrderId, T0.AddMinutes(3)), AdminRow("a-other", "Order", "order-9", T0.AddMinutes(9))],
            [EmployeeRow("e-1", OrderId, T0.AddMinutes(2)), EmployeeRow("e-other", "order-9", T0.AddMinutes(8))]);

        var result = await Handle(new GetActionTimeline.Request { UserId = UserId });

        Assert.True(result.IsSuccess);
        var page = result.Value!;
        Assert.Equal(4, page.Total);
        Assert.Equal(
            new[] { TimelineSource.Customer, TimelineSource.Admin, TimelineSource.Employee, TimelineSource.Customer },
            page.Data.Select(e => e.Source));
        Assert.Equal(new[] { "c-2", "a-1", "e-1", "c-1" }, page.Data.Select(e => e.Id));
        Assert.Equal("employee.order.dropped", page.Data.Single(e => e.Source == TimelineSource.Employee).Action);
    }

    [Fact]
    public async Task ByUser_Reaches_Admin_Rows_On_The_User_Their_Disputes_And_Their_Memberships()
    {
        SeedUserOwns(orderIds: [OrderId], disputeIds: ["dispute-1"], membershipIds: ["membership-1"]);
        Seed(
            [],
            [
                AdminRow("a-user", "User", UserId, T0.AddMinutes(1)),
                AdminRow("a-dispute", "Dispute", "dispute-1", T0.AddMinutes(2)),
                AdminRow("a-membership", "UserMembership", "membership-1", T0.AddMinutes(3)),
                AdminRow("a-stranger", "User", "user-2", T0.AddMinutes(4)),
                AdminRow("a-other-dispute", "Dispute", "dispute-2", T0.AddMinutes(5)),
            ],
            []);

        var result = await Handle(new GetActionTimeline.Request { UserId = UserId });

        Assert.Equal(new[] { "a-membership", "a-dispute", "a-user" }, result.Value!.Data.Select(e => e.Id));
    }

    [Fact]
    public async Task ByResource_Returns_Every_Row_Naming_The_Order_Including_The_Guest_Act()
    {
        Seed(
            [CustomerRow("c-guest", null, OrderId, T0.AddMinutes(1)), CustomerRow("c-mine", UserId, "order-2", T0.AddMinutes(2))],
            [AdminRow("a-1", "Order", OrderId, T0.AddMinutes(3))],
            [EmployeeRow("e-1", OrderId, T0.AddMinutes(4))]);

        var result = await Handle(new GetActionTimeline.Request { ResourceType = "Order", ResourceId = OrderId });

        Assert.Equal(3, result.Value!.Total);
        Assert.Equal(new[] { "e-1", "a-1", "c-guest" }, result.Value.Data.Select(e => e.Id));
    }

    [Fact]
    public async Task ByUser_Never_Surfaces_A_Guest_Row()
    {
        SeedUserOwns(orderIds: [OrderId]);
        Seed([CustomerRow("c-guest", null, OrderId, T0)], [], []);

        var result = await Handle(new GetActionTimeline.Request { UserId = UserId });

        Assert.Equal(0, result.Value!.Total);
        Assert.Empty(result.Value.Data);
    }

    [Fact]
    public async Task ByResource_Reads_The_Employee_Table_Only_For_An_Order()
    {
        Seed([], [AdminRow("a-1", "Dispute", "dispute-1", T0)], [EmployeeRow("e-1", "dispute-1", T0.AddMinutes(1))]);

        var result = await Handle(new GetActionTimeline.Request { ResourceType = "Dispute", ResourceId = "dispute-1" });

        var only = Assert.Single(result.Value!.Data);
        Assert.Equal("a-1", only.Id);
    }

    [Fact]
    public async Task Paging_Is_Stable_Across_Pages()
    {
        SeedUserOwns(orderIds: [OrderId]);
        Seed(
            Enumerable.Range(0, 3).Select(i => CustomerRow($"c-{i}", UserId, OrderId, T0.AddMinutes(i * 3))).ToArray(),
            Enumerable.Range(0, 3).Select(i => AdminRow($"a-{i}", "Order", OrderId, T0.AddMinutes(i * 3 + 1))).ToArray(),
            Enumerable.Range(0, 3).Select(i => EmployeeRow($"e-{i}", OrderId, T0.AddMinutes(i * 3 + 2))).ToArray());

        var first = await Handle(new GetActionTimeline.Request { UserId = UserId, Offset = 0, Limit = 4 });
        var second = await Handle(new GetActionTimeline.Request { UserId = UserId, Offset = 4, Limit = 4 });
        var third = await Handle(new GetActionTimeline.Request { UserId = UserId, Offset = 8, Limit = 4 });

        var ids = first.Value!.Data.Concat(second.Value!.Data).Concat(third.Value!.Data).Select(e => e.Id).ToList();
        Assert.Equal(9, first.Value.Total);
        Assert.Equal(new[] { "e-2", "a-2", "c-2", "e-1", "a-1", "c-1", "e-0", "a-0", "c-0" }, ids);
        Assert.Equal(2, first.Value.PageNumber + 1);
        Assert.Equal(4, second.Value.PageSize);
    }

    private Task<BusinessResult<PagedData<TimelineEntryDto>>> Handle(GetActionTimeline.Request request)
    {
        var handlerType = typeof(GetActionTimeline).GetNestedType("Handler", BindingFlags.NonPublic)!;
        var handler = Activator.CreateInstance(handlerType,
            _customer.Object, _admin.Object, _employee.Object, _orders.Object, _disputes.Object, _memberships.Object)!;
        var method = handlerType.GetMethod("Handle")!;
        return (Task<BusinessResult<PagedData<TimelineEntryDto>>>)method.Invoke(handler, [request, CancellationToken.None])!;
    }

    private void Seed(CustomerActionAudit[] customer, AdminActionAudit[] admin, EmployeeActionAudit[] employee)
    {
        _customer.Setup(r => r.GetQueryable()).Returns(customer.AsQueryable().BuildMock());
        _admin.Setup(r => r.GetQueryable()).Returns(admin.AsQueryable().BuildMock());
        _employee.Setup(r => r.GetQueryable()).Returns(employee.AsQueryable().BuildMock());
    }

    private void SeedUserOwns(string[]? orderIds = null, string[]? disputeIds = null, string[]? membershipIds = null)
    {
        var orders = (orderIds ?? []).Select(id =>
        {
            var order = OrderMockFactory.Generate(new OrderMockFactory.OrderPartial { Id = id, UserId = UserId });
            return order;
        }).ToArray();
        _orders.Setup(r => r.GetQueryable()).Returns(orders.AsQueryable().BuildMock());

        var disputes = (disputeIds ?? []).Select(id =>
        {
            var dispute = new Dispute(OrderId, UserId, DisputeReason.Other, "d", UserId);
            dispute.Id = id;
            return dispute;
        }).ToArray();
        _disputes.Setup(r => r.GetQueryable()).Returns(disputes.AsQueryable().BuildMock());

        var memberships = (membershipIds ?? []).Select(id =>
        {
            var membership = UserMembership.Create(UserId, "plan-1", "cur-1", "sub_1", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
            membership.Id = id;
            return membership;
        }).ToArray();
        _memberships.Setup(r => r.GetQueryable()).Returns(memberships.AsQueryable().BuildMock());
    }

    private static TimelineEntryDto Entry(TimelineSource source, string id, DateTimeOffset occurredOn) =>
        new(source, id, occurredOn, "actor", "act", "Order", OrderId, true, null);

    private static CustomerActionAudit CustomerRow(string id, string? userId, string orderId, DateTimeOffset occurredOn)
    {
        var row = CustomerActionAudit.Create(
            userId: userId, clientAudience: "cleansia.customer", ipAddress: null, deviceLabel: null, deviceId: null,
            action: "customer.order.cancel", resourceType: "Order", resourceId: orderId, success: true, errorCode: null,
            payloadJson: null, correlationId: null);
        row.Id = id;
        typeof(CustomerActionAudit).GetProperty(nameof(CustomerActionAudit.OccurredOn))!.SetValue(row, occurredOn);
        return row;
    }

    private static AdminActionAudit AdminRow(string id, string resourceType, string resourceId, DateTimeOffset occurredOn) =>
        new()
        {
            Id = id,
            ActorId = "admin-1",
            ActorProfile = UserProfile.Administrator,
            Action = "order.refund.full",
            ResourceType = resourceType,
            ResourceId = resourceId,
            Success = true,
            OccurredOn = occurredOn,
        };

    private static EmployeeActionAudit EmployeeRow(string id, string orderId, DateTimeOffset occurredOn)
    {
        var row = EmployeeActionAudit.Create("employee-1", orderId, EmployeeAuditAction.OrderDropped);
        row.Id = id;
        row.Created("employee-user-1", occurredOn);
        return row;
    }
}
