using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.EmployeeDocuments.DTOs;

/// <summary>
/// One document type a country expects, as the admin screen and the cleaner's checklist both read it.
/// </summary>
/// <param name="IsRequired">
/// Whether approval is BLOCKED without it. A row that is not required still appears on the cleaner's
/// upload checklist — that is the difference between "we would like this" and "you cannot start
/// without this", and both are worth telling somebody.
/// </param>
public record DocumentRequirementDto(
    string Id,
    string CountryId,
    DocumentType DocumentType,
    bool IsRequired,
    int SortOrder);

/// <summary>
/// What the cleaner still owes, resolved against what they have already uploaded.
///
/// <para>This is the placeholder the partner apps show BEFORE anything is uploaded — the whole point
/// being that a cleaner learns what is wanted from the app rather than by contacting support on their
/// first day.</para>
/// </summary>
/// <param name="Status">
/// Null when nothing of this type has been uploaded. Otherwise the status of the newest one, so the
/// checklist can distinguish "not started" from "waiting on us" from "rejected, try again".
/// </param>
public record MyDocumentRequirementDto(
    DocumentType DocumentType,
    bool IsRequired,
    int SortOrder,
    DocumentStatus? Status,
    string? DocumentId);

/// <summary>A cleaner's open or answered request to have a document removed, for the admin queue.</summary>
public record DocumentDeletionRequestDto(
    string Id,
    string DocumentId,
    string EmployeeId,
    string EmployeeName,
    string DocumentFileName,
    DocumentType DocumentType,
    string Reason,
    DocumentDeletionRequestStatus Status,
    string? ReviewNotes,
    DateTimeOffset CreatedOn,
    DateTimeOffset? ReviewedAt);
