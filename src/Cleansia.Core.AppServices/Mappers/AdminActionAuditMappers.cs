using Cleansia.Core.AppServices.Features.Auditing.DTOs;
using Cleansia.Core.AppServices.Features.Auditing.Filters;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Specifications;

namespace Cleansia.Core.AppServices.Mappers;

public static class AdminActionAuditMappers
{
    public static AdminActionAuditDto MapToDto(this AdminActionAudit audit)
    {
        return new AdminActionAuditDto(
            Id: audit.Id,
            ActorId: audit.ActorId,
            ActorEmail: audit.ActorEmail,
            ActorProfile: audit.ActorProfile,
            Action: audit.Action,
            ResourceType: audit.ResourceType,
            ResourceId: audit.ResourceId,
            Success: audit.Success,
            ErrorCode: audit.ErrorCode,
            OccurredOn: audit.OccurredOn,
            Reason: audit.Reason,
            CorrelationId: audit.CorrelationId);
    }

    public static AdminActionAuditDetailDto MapToDetailDto(this AdminActionAudit audit)
    {
        return new AdminActionAuditDetailDto(
            Id: audit.Id,
            ActorId: audit.ActorId,
            ActorEmail: audit.ActorEmail,
            ActorProfile: audit.ActorProfile,
            Action: audit.Action,
            ResourceType: audit.ResourceType,
            ResourceId: audit.ResourceId,
            Success: audit.Success,
            ErrorCode: audit.ErrorCode,
            OccurredOn: audit.OccurredOn,
            Reason: audit.Reason,
            CorrelationId: audit.CorrelationId,
            BeforeJson: audit.BeforeJson,
            AfterJson: audit.AfterJson);
    }

    public static AdminActionAuditSpecification MapToDomain(this AdminActionAuditFilter? filter)
    {
        return new AdminActionAuditSpecification
        {
            ActorId = filter?.ActorId,
            ActorEmail = filter?.ActorEmail,
            Action = filter?.Action,
            ResourceType = filter?.ResourceType,
            ResourceId = filter?.ResourceId,
            OccurredFrom = filter?.OccurredFrom,
            OccurredTo = filter?.OccurredTo,
            Success = filter?.Success
        };
    }
}
