using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Domain.Users;

public class GdprRequest : Auditable, ITenantEntity
{
    [Required]
    public string UserId { get; private set; }

    public User? User { get; private set; }

    [Required]
    [MaxLength(20)]
    public string RequestType { get; private set; }

    public GdprRequestStatus Status { get; private set; }

    [MaxLength(255)]
    public string? ProcessedBy { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    [MaxLength(1000)]
    public string? Notes { get; private set; }

    public static GdprRequest Create(string userId, string requestType)
        => new()
        {
            UserId = userId,
            RequestType = requestType,
            Status = GdprRequestStatus.Pending
        };

    public GdprRequest MarkProcessing()
    {
        Status = GdprRequestStatus.Processing;
        return this;
    }

    public GdprRequest MarkCompleted(string? processedBy = null, string? notes = null)
    {
        Status = GdprRequestStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
        ProcessedBy = processedBy;
        Notes = notes;
        return this;
    }

    public GdprRequest MarkFailed(string? notes = null)
    {
        Status = GdprRequestStatus.Failed;
        CompletedAt = DateTimeOffset.UtcNow;
        Notes = notes;
        return this;
    }
}
