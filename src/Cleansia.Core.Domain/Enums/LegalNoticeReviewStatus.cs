namespace Cleansia.Core.Domain.Enums;

/// <summary>
/// How much assurance stands behind a jurisdiction's legal notice on an invoice. It is the gate, not a
/// caption: only a notice above <see cref="NotReviewed"/> is printed, so a paragraph that asserts
/// something about a country's law and was never checked stays inert in the row instead of on the
/// document, and the platform's generic fallback prints in its place.
/// </summary>
public enum LegalNoticeReviewStatus
{
    /// <summary>
    /// Nobody has produced a notice for this jurisdiction. Any text stored alongside is not printed.
    /// </summary>
    NotReviewed = 0,

    /// <summary>
    /// The wording is taken verbatim from a document the business itself issues in this jurisdiction.
    /// Real provenance, but it is not counsel's opinion — the distinction from
    /// <see cref="CounselReviewed"/> is what tells the owner which jurisdictions still need a lawyer.
    /// </summary>
    BusinessSupplied = 1,

    /// <summary>
    /// A lawyer reviewed this exact wording for this jurisdiction.
    /// </summary>
    CounselReviewed = 2
}
