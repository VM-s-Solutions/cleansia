using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.EmployeeDocuments.DTOs;

public record EmployeeDocumentItem(
    string Id,
    string FileName,
    string FilePath,
    string ContentType,
    long FileSizeBytes,
    DocumentType DocumentType,
    string? Description,
    int Version,
    string? PreviousVersionId,
    string EmployeeId,
    DocumentStatus Status,
    string? ReviewNotes,
    string? ReviewedByUserId,
    DateTimeOffset? ReviewedAt,
    bool IsActive,
    DateTimeOffset CreatedOn,
    string CreatedBy,
    DateTimeOffset? UpdatedOn
);
