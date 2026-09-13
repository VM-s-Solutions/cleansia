using Cleansia.Core.AppServices.Features.Auditing.DTOs;
using Cleansia.Core.AppServices.Features.Auditing.Filters;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Specifications;

namespace Cleansia.Core.AppServices.Mappers;

public static class CustomerActionAuditMappers
{
    public static CustomerActionAuditDto MapToDto(this CustomerActionAudit audit)
    {
        return new CustomerActionAuditDto(
            Id: audit.Id,
            UserId: audit.UserId,
            ClientAudience: audit.ClientAudience,
            Action: audit.Action,
            ResourceType: audit.ResourceType,
            ResourceId: audit.ResourceId,
            Success: audit.Success,
            ErrorCode: audit.ErrorCode,
            OccurredOn: audit.OccurredOn);
    }

    public static CustomerActionAuditDetailDto MapToDetailDto(this CustomerActionAudit audit, string? currencyCode)
    {
        return new CustomerActionAuditDetailDto(
            Id: audit.Id,
            UserId: audit.UserId,
            ClientAudience: audit.ClientAudience,
            IpAddress: audit.IpAddress,
            DeviceLabel: audit.DeviceLabel,
            DeviceId: audit.DeviceId,
            Action: audit.Action,
            ResourceType: audit.ResourceType,
            ResourceId: audit.ResourceId,
            Success: audit.Success,
            ErrorCode: audit.ErrorCode,
            OccurredOn: audit.OccurredOn,
            PayloadJson: audit.PayloadJson,
            CorrelationId: audit.CorrelationId,
            CurrencyCode: currencyCode);
    }

    public static TimelineEntryDto MapToTimelineEntry(this CustomerActionAudit audit)
    {
        return new TimelineEntryDto(
            Source: TimelineSource.Customer,
            Id: audit.Id,
            OccurredOn: audit.OccurredOn,
            ActorId: audit.UserId,
            Action: audit.Action,
            ResourceType: audit.ResourceType,
            ResourceId: audit.ResourceId,
            Success: audit.Success,
            ErrorCode: audit.ErrorCode);
    }

    public static TimelineEntryDto MapToTimelineEntry(this AdminActionAudit audit)
    {
        return new TimelineEntryDto(
            Source: TimelineSource.Admin,
            Id: audit.Id,
            OccurredOn: audit.OccurredOn,
            ActorId: audit.ActorId,
            Action: audit.Action,
            ResourceType: audit.ResourceType,
            ResourceId: audit.ResourceId,
            Success: audit.Success,
            ErrorCode: audit.ErrorCode);
    }

    public static TimelineEntryDto MapToTimelineEntry(this EmployeeActionAudit audit, string actionLabel)
    {
        return new TimelineEntryDto(
            Source: TimelineSource.Employee,
            Id: audit.Id,
            OccurredOn: audit.CreatedOn,
            ActorId: audit.EmployeeId,
            Action: actionLabel,
            ResourceType: "Order",
            ResourceId: audit.OrderId,
            Success: true,
            ErrorCode: null);
    }

    public static CustomerActionAuditSpecification MapToDomain(this CustomerActionAuditFilter? filter)
    {
        return new CustomerActionAuditSpecification
        {
            UserId = filter?.UserId,
            Action = filter?.Action,
            ResourceType = filter?.ResourceType,
            ResourceId = filter?.ResourceId,
            OccurredFrom = filter?.OccurredFrom,
            OccurredTo = filter?.OccurredTo,
            Success = filter?.Success,
            ErrorCode = filter?.ErrorCode,
            ClientAudience = filter?.ClientAudience
        };
    }
}
