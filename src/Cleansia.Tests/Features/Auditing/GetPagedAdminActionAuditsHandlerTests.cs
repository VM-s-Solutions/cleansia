using System.Linq.Expressions;
using System.Reflection;
using Cleansia.Core.AppServices.Features.Auditing;
using Cleansia.Core.AppServices.Features.Auditing.DTOs;
using Cleansia.Core.AppServices.Features.Auditing.Filters;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Sorting.Common;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// TC-AUDIT-QUERY (handler slice): pins the canonical paged shape of the admin audit-log read
/// (row projection, page metadata) and that each filter reaches the specification predicate. The
/// tenant-scoping + cross-tenant invisibility leg of TC-AUDIT-QUERY is a real-Postgres integration
/// test (the EF global query filter is not exercised by a mocked repo).
/// </summary>
public class GetPagedAdminActionAuditsHandlerTests
{
    private readonly Mock<IAdminActionAuditRepository> _repository = new();

    private Task<PagedData<AdminActionAuditDto>> Handle(GetPagedAdminActionAudits.Request request)
    {
        var handlerType = typeof(GetPagedAdminActionAudits).GetNestedType("Handler", BindingFlags.NonPublic)!;
        var handler = Activator.CreateInstance(handlerType, _repository.Object)!;
        var method = handlerType.GetMethod("Handle")!;
        return (Task<PagedData<AdminActionAuditDto>>)method.Invoke(handler, [request, CancellationToken.None])!;
    }

    private static AdminActionAudit Row(
        string id = "aud-1",
        string actorId = "admin-1",
        string? actorEmail = "admin@cleansia.test",
        UserProfile profile = UserProfile.Administrator,
        string action = "IssuePartialRefund",
        string? resourceType = "Order",
        string? resourceId = "order-1",
        bool success = true,
        string? errorCode = null,
        DateTimeOffset? occurredOn = null)
    {
        return new AdminActionAudit
        {
            Id = id,
            ActorId = actorId,
            ActorEmail = actorEmail,
            ActorProfile = profile,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Success = success,
            ErrorCode = errorCode,
            OccurredOn = occurredOn ?? DateTimeOffset.UtcNow,
        };
    }

    private void SetupRepo(IEnumerable<AdminActionAudit> rows, int total,
        Action<Expression<Func<AdminActionAudit, bool>>?>? captureFilter = null)
    {
        _repository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<AdminActionAudit, bool>>>(), It.IsAny<CancellationToken>()))
            .Callback<Expression<Func<AdminActionAudit, bool>>?, CancellationToken>((f, _) => captureFilter?.Invoke(f))
            .ReturnsAsync(total);
        _repository
            .Setup(r => r.GetPagedSort<AdminActionAuditSort>(
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Expression<Func<AdminActionAudit, bool>>>(),
                It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(rows.AsQueryable().BuildMock());
    }

    [Fact]
    public async Task Projects_Row_And_PageMetadata()
    {
        SetupRepo(new[] { Row() }, total: 31);

        var result = await Handle(new GetPagedAdminActionAudits.Request { Offset = 20, Limit = 10 });

        Assert.Equal(31, result.Total);
        Assert.Equal(3, result.PageNumber);
        Assert.Equal(10, result.PageSize);

        var row = Assert.Single(result.Data);
        Assert.Equal("aud-1", row.Id);
        Assert.Equal("admin-1", row.ActorId);
        Assert.Equal("admin@cleansia.test", row.ActorEmail);
        Assert.Equal(UserProfile.Administrator, row.ActorProfile);
        Assert.Equal("IssuePartialRefund", row.Action);
        Assert.Equal("Order", row.ResourceType);
        Assert.Equal("order-1", row.ResourceId);
        Assert.True(row.Success);
    }

    [Fact]
    public async Task ActorId_Filter_Reaches_Specification()
    {
        Expression<Func<AdminActionAudit, bool>>? captured = null;
        SetupRepo(Array.Empty<AdminActionAudit>(), total: 0, f => captured = f);

        await Handle(new GetPagedAdminActionAudits.Request
        {
            Filter = new AdminActionAuditFilter("admin-1", null, null, null, null, null, null, null)
        });

        Assert.NotNull(captured);
        var predicate = captured!.Compile();
        Assert.True(predicate(Row(actorId: "admin-1")));
        Assert.False(predicate(Row(actorId: "admin-2")));
    }

    [Fact]
    public async Task Action_And_Resource_Filters_Reach_Specification()
    {
        Expression<Func<AdminActionAudit, bool>>? captured = null;
        SetupRepo(Array.Empty<AdminActionAudit>(), total: 0, f => captured = f);

        await Handle(new GetPagedAdminActionAudits.Request
        {
            Filter = new AdminActionAuditFilter(null, null, "IssuePartialRefund", "Order", "order-1", null, null, null)
        });

        var predicate = captured!.Compile();
        Assert.True(predicate(Row(action: "IssuePartialRefund", resourceType: "Order", resourceId: "order-1")));
        Assert.False(predicate(Row(action: "AdminOverrideOrderStatus", resourceType: "Order", resourceId: "order-1")));
        Assert.False(predicate(Row(action: "IssuePartialRefund", resourceType: "Order", resourceId: "order-2")));
    }

    [Fact]
    public async Task Outcome_Filter_Reaches_Specification()
    {
        Expression<Func<AdminActionAudit, bool>>? captured = null;
        SetupRepo(Array.Empty<AdminActionAudit>(), total: 0, f => captured = f);

        await Handle(new GetPagedAdminActionAudits.Request
        {
            Filter = new AdminActionAuditFilter(null, null, null, null, null, null, null, Success: false)
        });

        var predicate = captured!.Compile();
        Assert.True(predicate(Row(success: false, errorCode: "refund.exceeds_remaining")));
        Assert.False(predicate(Row(success: true)));
    }

    [Fact]
    public async Task DateRange_Filter_Reaches_Specification()
    {
        Expression<Func<AdminActionAudit, bool>>? captured = null;
        SetupRepo(Array.Empty<AdminActionAudit>(), total: 0, f => captured = f);

        var from = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);
        await Handle(new GetPagedAdminActionAudits.Request
        {
            Filter = new AdminActionAuditFilter(null, null, null, null, null, from, to, null)
        });

        var predicate = captured!.Compile();
        var inRange = Row(occurredOn: new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero));
        var outOfRange = Row(occurredOn: new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero));

        Assert.True(predicate(inRange));
        Assert.False(predicate(outOfRange));
    }
}
