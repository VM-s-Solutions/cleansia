using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Domain.Users;

public class GdprRequest : Auditable, ITenantEntity
{
    public const int NotesMaxLength = 1000;

    /// <summary>The <c>RequestType</c> of an erasure — filed, retried, recorded and listed under one spelling.</summary>
    public const string DeletionRequestType = "Deletion";

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

    [MaxLength(NotesMaxLength)]
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
        Notes = AppendNote(Notes, notes);
        return this;
    }

    public GdprRequest MarkFailed(string? processedBy = null, string? notes = null)
    {
        Status = GdprRequestStatus.Failed;
        CompletedAt = DateTimeOffset.UtcNow;
        ProcessedBy = processedBy;
        Notes = AppendNote(Notes, notes);
        return this;
    }

    // A retried request keeps every attempt's note; the column is bounded, so the OLDEST text is what
    // goes when it overflows — the newest note is the one an admin acts on.
    private static string? AppendNote(string? existing, string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return existing;
        }

        var combined = string.IsNullOrWhiteSpace(existing) ? note : $"{existing}\n{note}";
        return combined.Length <= NotesMaxLength ? combined : combined[^NotesMaxLength..];
    }
}
