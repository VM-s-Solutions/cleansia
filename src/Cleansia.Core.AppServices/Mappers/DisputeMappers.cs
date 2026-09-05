using Cleansia.Core.AppServices.Features.Disputes.DTOs;
using Cleansia.Core.AppServices.Features.Disputes.Filters;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
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
            Currency: dispute.Order?.Currency?.MapToDetailDto(),
            ResolvedOn: dispute.ResolvedOn,
            Messages: dispute.Messages.Select(m => m.MapToDto()).ToList(),
            Evidence: dispute.Evidence.Select(e => e.MapToDto(evidenceBlobClient)).ToList(),
            CreatedOn: dispute.CreatedOn,
            UpdatedOn: dispute.UpdatedOn,
            // Measured from when the clean ended, or from when it was due to start if it never did —
            // a no-show has no completion time, and that is exactly the case the window covers.
            FiledWithinWindow: dispute.Order is null
                ? null
                : DisputeLimits.IsWithinFilingWindow(
                    dispute.Order.CompletedAt, dispute.Order.CleaningDateTime, dispute.CreatedOn),
            Lines: dispute.Lines.Select(line => line.MapToDto(dispute.Order)).ToList()
        );
    }

    /// <summary>
    /// Names come from the ORDER's own graph, not a second query: the dispute's order already carries
    /// its services and packages, so a line resolves without touching the catalogue again — and
    /// resolves to what the order holds rather than to whatever the catalogue says today.
    /// </summary>
    private static DisputeLineDto MapToDto(this DisputeLine line, Order? order)
    {
        var package = order?.SelectedPackages
            .FirstOrDefault(p => p.PackageId == line.PackageId)?.Package;

        var serviceName = package is null
            ? order?.SelectedServices
                .FirstOrDefault(s => s.ServiceId == line.ServiceId)?.Service?.Name
            : package.IncludedServices
                .FirstOrDefault(s => s.ServiceId == line.ServiceId)?.Service?.Name;

        return new DisputeLineDto(
            ServiceId: line.ServiceId,
            ServiceName: serviceName ?? string.Empty,
            PackageId: line.PackageId,
            PackageName: package?.Name);
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
            // The evidence row records no content type, so the served one is derived from the stored blob
            // PATH through the same closed set — its extension is minted from the payload's bytes, while
            // FileName is the caller's own string. An unrecognised extension serves opaquely.
            blobUrl = blobClient
                .GenerateSasUri(
                    evidence.FilePath,
                    TimeSpan.FromHours(1),
                    ServedContentType.ForFileName(evidence.FilePath))
                .ToString();
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
