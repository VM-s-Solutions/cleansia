using Cleansia.Core.AppServices.Features.Disputes.DTOs;
using Cleansia.Core.AppServices.Features.Disputes.Filters;
using Cleansia.Core.Blobs.Abstractions;
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

    public static DisputeDetails MapToDetails(this Dispute dispute, IBlobContainerClient evidenceBlobClient)
    {
        return new DisputeDetails(
            Id: dispute.Id,
            OrderId: dispute.OrderId,
            DisplayOrderNumber: dispute.Order?.DisplayOrderNumber ?? "",
            CustomerName: dispute.User?.FirstName + " " + dispute.User?.LastName ?? "",
            CustomerEmail: dispute.User?.Email ?? "",
            Reason: dispute.Reason.MapToCode(),
            Description: dispute.Description,
            Status: dispute.Status.MapToCode(),
            ResolutionNotes: dispute.ResolutionNotes,
            RefundAmount: dispute.RefundAmount,
            ResolvedOn: dispute.ResolvedOn,
            Messages: dispute.Messages.Select(m => m.MapToDto()).ToList(),
            Evidence: dispute.Evidence.Select(e => e.MapToDto(evidenceBlobClient)).ToList(),
            CreatedOn: dispute.CreatedOn,
            UpdatedOn: dispute.UpdatedOn
        );
    }

    public static DisputeMessageDto MapToDto(this DisputeMessage message)
    {
        var authorName = message.Author != null
            ? $"{message.Author.FirstName} {message.Author.LastName}".Trim()
            : string.Empty;

        return new DisputeMessageDto(
            Id: message.Id,
            Message: message.Message,
            AuthorId: message.AuthorId,
            AuthorName: authorName,
            IsStaffMessage: message.IsStaffMessage,
            CreatedOn: message.CreatedOn
        );
    }

    public static DisputeEvidenceDto MapToDto(this DisputeEvidence evidence, IBlobContainerClient blobClient)
    {
        string? blobUrl = null;
        try
        {
            blobUrl = blobClient.GenerateSasUri(evidence.FilePath, TimeSpan.FromHours(1)).ToString();
        }
        catch
        {
            // Swallow — UI handles null gracefully.
        }

        return new DisputeEvidenceDto(
            Id: evidence.Id,
            FileName: evidence.FileName,
            FilePath: evidence.FilePath,
            BlobUrl: blobUrl,
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
