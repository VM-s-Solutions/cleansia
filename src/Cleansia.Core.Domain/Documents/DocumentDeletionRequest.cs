using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Domain.Documents;

/// <summary>
/// A cleaner asking for one of their documents to be removed, and an admin's answer.
///
/// <para><b>Why asking replaced doing.</b> The delete button removed the document immediately and
/// soft-deleted it, which flipped <c>AreDocumentsUploaded</c> to false, which re-engaged the
/// registration lock — so one tap, with no dialog on either platform, cost a cleaner their access to
/// work. Worse, we need some of those documents as the employer: the person least able to judge
/// whether a document can go was the only one who could remove it.</para>
///
/// <para><b>The document stays ACTIVE until the request is approved.</b> That is the whole design. A
/// pending request changes nothing the cleaner can work with — it is a message, not a state change —
/// so a request that is never answered leaves them exactly as they were rather than locked out.</para>
///
/// <para><b>Replacement is the other door and is deliberately not this.</b> Replacing supersedes a
/// document with a newer version and needs no permission, because the slot never empties;
/// <c>EmployeeDocument.CreateNewVersion</c> already existed for it. Deletion is for the case where
/// nothing should be there at all, which is the case an employer has to agree with.</para>
/// </summary>
public class DocumentDeletionRequest : Auditable, ITenantEntity
{
    public string DocumentId { get; private set; } = default!;

    public EmployeeDocument? Document { get; private set; }

    /// <summary>The employee the document belongs to, denormalised so the admin queue can filter
    /// without walking the document.</summary>
    public string EmployeeId { get; private set; } = default!;

    /// <summary>
    /// Why the cleaner wants it gone, in their words. Required — the reason IS the request; without
    /// one an admin is being asked to rule on nothing.
    /// </summary>
    [MaxLength(1000)]
    public string Reason { get; private set; } = default!;

    public DocumentDeletionRequestStatus Status { get; private set; } = DocumentDeletionRequestStatus.Pending;

    /// <summary>The admin's answer, shown to the cleaner. Optional on approval, where the outcome
    /// speaks for itself; a refusal without one tells them nothing.</summary>
    [MaxLength(1000)]
    public string? ReviewNotes { get; private set; }

    public string? ReviewedByUserId { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    private DocumentDeletionRequest() { }

    public static DocumentDeletionRequest Create(
        string documentId,
        string employeeId,
        string reason,
        string createdBy)
    {
        var request = new DocumentDeletionRequest
        {
            DocumentId = documentId,
            EmployeeId = employeeId,
            Reason = reason,
            Status = DocumentDeletionRequestStatus.Pending,
        };

        request.Created(createdBy, DateTimeOffset.UtcNow);
        return request;
    }

    public DocumentDeletionRequest Approve(string reviewedByUserId, string? notes)
    {
        Status = DocumentDeletionRequestStatus.Approved;
        ReviewedByUserId = reviewedByUserId;
        ReviewedAt = DateTimeOffset.UtcNow;
        ReviewNotes = notes;
        Updated(reviewedByUserId, DateTimeOffset.UtcNow);
        return this;
    }

    public DocumentDeletionRequest Reject(string reviewedByUserId, string? notes)
    {
        Status = DocumentDeletionRequestStatus.Rejected;
        ReviewedByUserId = reviewedByUserId;
        ReviewedAt = DateTimeOffset.UtcNow;
        ReviewNotes = notes;
        Updated(reviewedByUserId, DateTimeOffset.UtcNow);
        return this;
    }

    /// <summary>
    /// Still awaiting an answer. The one predicate the whole flow turns on: it is what stops a second
    /// request stacking on the first, and what the cleaner's screen reads to show "requested".
    /// </summary>
    public bool IsOpen => Status == DocumentDeletionRequestStatus.Pending;
}
