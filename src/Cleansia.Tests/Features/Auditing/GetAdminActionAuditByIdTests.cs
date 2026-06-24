using System.Reflection;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auditing;
using Cleansia.Core.AppServices.Features.Auditing.DTOs;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Moq;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// AC1/AC2/AC3 (handler + validator slice): the single-row admin-audit read returns ONE row by id
/// INCLUDING its pre-redacted BeforeJson/AfterJson snapshot (the projection the paged cut omits),
/// fails not-found when the id resolves to nothing (the tenant-filtered repo yields null for a
/// cross-tenant id — same code path as a missing id; the real-Postgres leg lives in IntegrationTests),
/// and the detail DTO carries exactly what the entity persisted and nothing the write-side did not
/// already minimize.
/// </summary>
public class GetAdminActionAuditByIdTests
{
    private const string AuditId = "aud-1";

    private readonly Mock<IAdminActionAuditRepository> _repository = new();

    private static AdminActionAudit Row(
        string id = AuditId,
        string actorId = "admin-1",
        string? actorEmail = "admin@cleansia.test",
        string action = "IssuePartialRefund",
        string? resourceType = "Order",
        string? resourceId = "order-1",
        bool success = true,
        string? beforeJson = "{\"orderId\":\"order-1\",\"consumedRefund\":0}",
        string? afterJson = "{\"orderId\":\"order-1\",\"consumedRefund\":500}")
    {
        return new AdminActionAudit
        {
            Id = id,
            ActorId = actorId,
            ActorEmail = actorEmail,
            ActorProfile = UserProfile.Administrator,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Success = success,
            OccurredOn = DateTimeOffset.UtcNow,
            BeforeJson = beforeJson,
            AfterJson = afterJson,
        };
    }

    private async Task<BusinessResult<AdminActionAuditDetailDto>> InvokeHandler(string auditId)
    {
        var handlerType = typeof(GetAdminActionAuditById).GetNestedType("Handler", BindingFlags.NonPublic)!;
        var handler = Activator.CreateInstance(handlerType, _repository.Object)!;
        var method = handlerType.GetMethod("Handle")!;
        var task = (Task<BusinessResult<AdminActionAuditDetailDto>>)method.Invoke(
            handler, [new GetAdminActionAuditById.Query(auditId), CancellationToken.None])!;
        return await task;
    }

    [Fact]
    public async Task Returns_The_Row_With_Its_Before_And_After_Snapshots()
    {
        _repository
            .Setup(r => r.GetByIdAsync(AuditId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Row());

        var result = await InvokeHandler(AuditId);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal(AuditId, dto.Id);
        Assert.Equal("admin-1", dto.ActorId);
        Assert.Equal("IssuePartialRefund", dto.Action);
        Assert.Equal("Order", dto.ResourceType);
        Assert.Equal("order-1", dto.ResourceId);
        Assert.Equal("{\"orderId\":\"order-1\",\"consumedRefund\":0}", dto.BeforeJson);
        Assert.Equal("{\"orderId\":\"order-1\",\"consumedRefund\":500}", dto.AfterJson);
    }

    [Fact]
    public async Task Missing_Or_CrossTenant_Id_Returns_NotFound()
    {
        // The tenant-scoped repository yields null for a cross-tenant id exactly as for a non-existent
        // one (S3: the response never reveals the row exists in another tenant).
        _repository
            .Setup(r => r.GetByIdAsync(AuditId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AdminActionAudit?)null);

        var result = await InvokeHandler(AuditId);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.AuditNotFound, result.Error!.Message);
    }

    [Fact]
    public void Detail_Dto_Carries_Only_Persisted_Snapshot_Fields_No_Extra_Pii_Surface()
    {
        // AC3 — the detail DTO exposes exactly the columns T-0284 persisted (ids + changed fields, the
        // pre-redacted snapshot blobs), nothing the write-side did not already minimize. Pinning the
        // shape catches any future drift that would widen the read surface beyond the stored row.
        var expected = new[]
        {
            "Id", "ActorId", "ActorEmail", "ActorProfile", "Action", "ResourceType", "ResourceId",
            "Success", "ErrorCode", "OccurredOn", "Reason", "CorrelationId", "BeforeJson", "AfterJson",
        };

        var actual = typeof(AdminActionAuditDetailDto)
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n);

        Assert.Equal(expected.OrderBy(n => n), actual);
    }

    [Fact]
    public async Task Validator_Rejects_Empty_Id()
    {
        var validator = new GetAdminActionAuditById.Validator(_repository.Object);

        var result = await validator.ValidateAsync(new GetAdminActionAuditById.Query(string.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.Required);
    }

    [Fact]
    public async Task Validator_Rejects_Unknown_Or_CrossTenant_Id_As_NotFound()
    {
        _repository
            .Setup(r => r.ExistsAsync(AuditId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var validator = new GetAdminActionAuditById.Validator(_repository.Object);

        var result = await validator.ValidateAsync(new GetAdminActionAuditById.Query(AuditId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.AuditNotFound);
    }

    [Fact]
    public async Task Validator_Accepts_An_Existing_Id()
    {
        _repository
            .Setup(r => r.ExistsAsync(AuditId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var validator = new GetAdminActionAuditById.Validator(_repository.Object);

        var result = await validator.ValidateAsync(new GetAdminActionAuditById.Query(AuditId));

        Assert.True(result.IsValid);
    }
}
