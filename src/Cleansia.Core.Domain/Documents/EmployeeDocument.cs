using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Documents;

public class EmployeeDocument : Auditable
{
    [MaxLength(255)]
    public string FileName { get; private set; } = default!;

    [MaxLength(500)]
    public string FilePath { get; private set; } = default!;

    [MaxLength(100)]
    public string ContentType { get; private set; } = default!;

    public long FileSizeBytes { get; private set; }

    public DocumentType DocumentType { get; private set; }

    [MaxLength(500)]
    public string? Description { get; private set; }

    public int Version { get; private set; } = 1;

    public string? PreviousVersionId { get; private set; }
    public EmployeeDocument? PreviousVersion { get; private set; }

    public string EmployeeId { get; private set; } = default!;
    public Employee? Employee { get; private set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Pending;

    [MaxLength(500)]
    public string? ReviewNotes { get; private set; }

    public string? ReviewedByUserId { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }

    private EmployeeDocument() { }

    public static EmployeeDocument Create(
        string employeeId,
        string fileName,
        string filePath,
        string contentType,
        long fileSizeBytes,
        DocumentType documentType,
        string? description,
        string createdBy)
    {
        var document = new EmployeeDocument
        {
            EmployeeId = employeeId,
            FileName = fileName,
            FilePath = filePath,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            DocumentType = documentType,
            Description = description,
            Version = 1,
            Status = DocumentStatus.Pending
        };

        document.Created(createdBy, DateTimeOffset.UtcNow);

        return document;
    }

    public static EmployeeDocument CreateNewVersion(
        EmployeeDocument previousVersion,
        string fileName,
        string filePath,
        string contentType,
        long fileSizeBytes,
        string? description,
        string createdBy)
    {
        if (previousVersion == null)
            throw new ArgumentNullException(nameof(previousVersion));

        var newVersion = new EmployeeDocument
        {
            EmployeeId = previousVersion.EmployeeId,
            FileName = fileName,
            FilePath = filePath,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            DocumentType = previousVersion.DocumentType,
            Description = description ?? previousVersion.Description,
            Version = previousVersion.Version + 1,
            PreviousVersionId = previousVersion.Id,
            Status = DocumentStatus.Pending
        };

        newVersion.Created(createdBy, DateTimeOffset.UtcNow);

        return newVersion;
    }

    public EmployeeDocument UpdateMetadata(string? description, string updatedBy)
    {
        Description = description;
        Updated(updatedBy, DateTimeOffset.UtcNow);
        return this;
    }

    public EmployeeDocument Approve(string reviewedByUserId, string? notes = null)
    {
        Status = DocumentStatus.Approved;
        ReviewedByUserId = reviewedByUserId;
        ReviewedAt = DateTimeOffset.UtcNow;
        ReviewNotes = notes;
        Updated(reviewedByUserId, DateTimeOffset.UtcNow);
        return this;
    }

    public EmployeeDocument Reject(string reviewedByUserId, string? notes = null)
    {
        Status = DocumentStatus.Rejected;
        ReviewedByUserId = reviewedByUserId;
        ReviewedAt = DateTimeOffset.UtcNow;
        ReviewNotes = notes;
        Updated(reviewedByUserId, DateTimeOffset.UtcNow);
        return this;
    }

    public EmployeeDocument SoftDelete(string deletedBy)
    {
        Deactivated(deletedBy, DateTimeOffset.UtcNow);
        return this;
    }

    public bool IsLatestVersion() => string.IsNullOrEmpty(PreviousVersionId) || Version > (PreviousVersion?.Version ?? 0);
}
