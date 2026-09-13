using System.Linq.Expressions;
using System.Reflection;
using Cleansia.Core.AppServices.Features.Auditing;
using Cleansia.Core.AppServices.Features.Auditing.DTOs;
using Cleansia.Core.AppServices.Features.Auditing.Filters;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Sorting.Common;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// ADR-0062 D6 (handler slice): the customer audit list has the canonical paged shape of its admin
/// sibling — row projection without the payload or request metadata, page metadata — and each filter
/// reaches the specification predicate. The tenant-scoping leg is a real-Postgres integration test.
/// </summary>
public class GetPagedCustomerActionAuditsHandlerTests
{
    private readonly Mock<ICustomerActionAuditRepository> _repository = new();

    private Task<PagedData<CustomerActionAuditDto>> Handle(GetPagedCustomerActionAudits.Request request)
    {
        var handlerType = typeof(GetPagedCustomerActionAudits).GetNestedType("Handler", BindingFlags.NonPublic)!;
        var handler = Activator.CreateInstance(handlerType, _repository.Object)!;
        var method = handlerType.GetMethod("Handle")!;
        return (Task<PagedData<CustomerActionAuditDto>>)method.Invoke(handler, [request, CancellationToken.None])!;
    }

    private static CustomerActionAudit Row(
        string id = "aud-1",
        string? userId = "user-1",
        string clientAudience = "cleansia.customer",
        string action = "customer.order.cancel",
        string? resourceType = "Order",
        string? resourceId = "order-1",
        bool success = true,
        string? errorCode = null,
        DateTimeOffset? occurredOn = null)
    {
        var row = CustomerActionAudit.Create(
            userId: userId, clientAudience: clientAudience, ipAddress: "203.0.113.9", deviceLabel: "iPhone 15",
            deviceId: "device-1", action: action, resourceType: resourceType, resourceId: resourceId,
            success: success, errorCode: errorCode, payloadJson: "{\"feeRate\":0.5}", correlationId: "corr-1");
        row.Id = id;
        typeof(CustomerActionAudit).GetProperty(nameof(CustomerActionAudit.OccurredOn))!
            .SetValue(row, occurredOn ?? DateTimeOffset.UtcNow);
        return row;
    }

    private void SetupRepo(IEnumerable<CustomerActionAudit> rows, int total,
        Action<Expression<Func<CustomerActionAudit, bool>>?>? captureFilter = null)
    {
        _repository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<CustomerActionAudit, bool>>>(), It.IsAny<CancellationToken>()))
            .Callback<Expression<Func<CustomerActionAudit, bool>>?, CancellationToken>((f, _) => captureFilter?.Invoke(f))
            .ReturnsAsync(total);
        _repository
            .Setup(r => r.GetPagedSort<CustomerActionAuditSort>(
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Expression<Func<CustomerActionAudit, bool>>>(),
                It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(rows.AsQueryable().BuildMock());
    }

    private static Func<CustomerActionAudit, bool> Predicate(CustomerActionAuditFilter filter, GetPagedCustomerActionAuditsHandlerTests self)
    {
        Expression<Func<CustomerActionAudit, bool>>? captured = null;
        self.SetupRepo(Array.Empty<CustomerActionAudit>(), total: 0, f => captured = f);
        self.Handle(new GetPagedCustomerActionAudits.Request { Filter = filter }).GetAwaiter().GetResult();
        Assert.NotNull(captured);
        return captured!.Compile();
    }

    [Fact]
    public async Task Projects_Row_And_PageMetadata()
    {
        SetupRepo(new[] { Row() }, total: 31);

        var result = await Handle(new GetPagedCustomerActionAudits.Request { Offset = 20, Limit = 10 });

        Assert.Equal(31, result.Total);
        Assert.Equal(3, result.PageNumber);
        Assert.Equal(10, result.PageSize);

        var row = Assert.Single(result.Data);
        Assert.Equal("aud-1", row.Id);
        Assert.Equal("user-1", row.UserId);
        Assert.Equal("cleansia.customer", row.ClientAudience);
        Assert.Equal("customer.order.cancel", row.Action);
        Assert.Equal("Order", row.ResourceType);
        Assert.Equal("order-1", row.ResourceId);
        Assert.True(row.Success);
    }

    [Fact]
    public void User_Filter_Reaches_Specification()
    {
        var predicate = Predicate(new CustomerActionAuditFilter("user-1", null, null, null, null, null, null, null, null), this);

        Assert.True(predicate(Row(userId: "user-1")));
        Assert.False(predicate(Row(userId: "user-2")));
        Assert.False(predicate(Row(userId: null)));
    }

    [Fact]
    public void Action_And_Resource_Filters_Reach_Specification()
    {
        var predicate = Predicate(new CustomerActionAuditFilter(null, "customer.order.cancel", "Order", "order-1", null, null, null, null, null), this);

        Assert.True(predicate(Row(action: "customer.order.cancel", resourceType: "Order", resourceId: "order-1")));
        Assert.False(predicate(Row(action: "customer.order.create", resourceType: "Order", resourceId: "order-1")));
        Assert.False(predicate(Row(action: "customer.order.cancel", resourceType: "Order", resourceId: "order-2")));
    }

    [Fact]
    public void Outcome_Filter_Reaches_Specification()
    {
        var predicate = Predicate(new CustomerActionAuditFilter(null, null, null, null, null, null, Success: false, null, null), this);

        Assert.True(predicate(Row(success: false, errorCode: "order.in_progress_cannot_cancel")));
        Assert.False(predicate(Row(success: true)));
    }

    [Fact]
    public void DateRange_Filter_Reaches_Specification()
    {
        var from = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);
        var predicate = Predicate(new CustomerActionAuditFilter(null, null, null, null, from, to, null, null, null), this);

        Assert.True(predicate(Row(occurredOn: new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero))));
        Assert.False(predicate(Row(occurredOn: new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero))));
    }

    [Fact]
    public void ErrorCode_Filter_Is_A_Text_Match_On_The_Key()
    {
        var predicate = Predicate(new CustomerActionAuditFilter(null, null, null, null, null, null, null, ErrorCode: "in_progress", null), this);

        Assert.True(predicate(Row(success: false, errorCode: "order.in_progress_cannot_cancel")));
        Assert.False(predicate(Row(success: false, errorCode: "order.not_found")));
        Assert.False(predicate(Row(success: true, errorCode: null)));
    }

    [Fact]
    public void ClientAudience_Filter_Reaches_Specification()
    {
        var predicate = Predicate(new CustomerActionAuditFilter(null, null, null, null, null, null, null, null, ClientAudience: "cleansia.mobile.customer"), this);

        Assert.True(predicate(Row(clientAudience: "cleansia.mobile.customer")));
        Assert.False(predicate(Row(clientAudience: "cleansia.customer")));
    }
}
