using Cleansia.Core.AppServices.Features.Disputes.DTOs;
using Cleansia.Core.AppServices.Features.Disputes.Filters;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Specifications;

namespace Cleansia.Core.AppServices.Mappers;

public static class DisputeMappers
{
    public static DisputeListItem MapToListItem(this Dispute dispute)
    {
        return new DisputeListItem(
            Id: dispute.Id,
            OrderId: dispute.OrderId,
            DisplayOrderNumber: dispute.Order?.DisplayOrderNumber ?? "",
            CustomerName: dispute.User?.FirstName + " " + dispute.User?.LastName ?? "",
            CustomerEmail: dispute.User?.Email ?? "",
            Reason: dispute.Reason.MapToCode(),
            Status: dispute.Status.MapToCode(),
            CreatedOn: dispute.CreatedOn,
            ResolvedOn: dispute.ResolvedOn,
            RefundAmount: dispute.RefundAmount
        );
    }

    public static DisputeDetails MapToDetails(this Dispute dispute)
    {
        return new DisputeDetails(
            Id: dispute.Id,
            OrderId: dispute.OrderId,
            DisplayOrderNumber: dispute.Order?.DisplayOrderNumber ?? "",
            UserId: dispute.UserId,
            CustomerName: dispute.User?.FirstName + " " + dispute.User?.LastName ?? "",
            CustomerEmail: dispute.User?.Email ?? "",
            Reason: dispute.Reason.MapToCode(),
            Description: dispute.Description,
            Status: dispute.Status.MapToCode(),
            ResolutionNotes: dispute.ResolutionNotes,
            RefundAmount: dispute.RefundAmount,
            ResolvedBy: dispute.ResolvedBy,
            ResolvedOn: dispute.ResolvedOn,
            StripeDisputeId: dispute.StripeDisputeId,
            Messages: dispute.Messages.Select(m => m.MapToDto()),
            Evidence: dispute.Evidence.Select(e => e.MapToDto()),
            CreatedOn: dispute.CreatedOn,
            CreatedBy: dispute.CreatedBy,
            UpdatedOn: dispute.UpdatedOn,
            UpdatedBy: dispute.UpdatedBy
        );
    }

    public static DisputeMessageDto MapToDto(this DisputeMessage message)
    {
        return new DisputeMessageDto(
            Id: message.Id,
            Message: message.Message,
            AuthorId: message.AuthorId,
            AuthorName: "", // Will be populated from user lookup if needed
            IsStaffMessage: message.IsStaffMessage,
            CreatedOn: message.CreatedOn
        );
    }

    public static DisputeEvidenceDto MapToDto(this DisputeEvidence evidence)
    {
        return new DisputeEvidenceDto(
            Id: evidence.Id,
            FileName: evidence.FileName,
            FilePath: evidence.FilePath,
            UploadedBy: evidence.UploadedBy,
            UploadedOn: evidence.UploadedOn
        );
    }

    public static DisputeSpecification MapToDomain(this DisputeFilter? filter)
    {
        return new DisputeSpecification
        {
            OrderId = filter?.OrderId,
            UserId = filter?.UserId,
            CustomerName = filter?.CustomerName,
            CustomerEmail = filter?.CustomerEmail,
            Statuses = filter?.Statuses?.Select(s => (DisputeStatus)s),
            Reasons = filter?.Reasons?.Select(r => (DisputeReason)r),
            CreatedFrom = filter?.CreatedFrom,
            CreatedTo = filter?.CreatedTo,
            ResolvedFrom = filter?.ResolvedFrom,
            ResolvedTo = filter?.ResolvedTo,
            MinRefundAmount = filter?.MinRefundAmount,
            MaxRefundAmount = filter?.MaxRefundAmount
        };
    }
}
