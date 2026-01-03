using Cleansia.Core.AppServices.Features.EmployeeDocuments.DTOs;
using Cleansia.Core.Domain.Documents;

namespace Cleansia.Core.AppServices.Mappers;

public static class EmployeeDocumentMappers
{
    public static EmployeeDocumentItem MapToDto(this EmployeeDocument document)
    {
        return new EmployeeDocumentItem(
            Id: document.Id,
            FileName: document.FileName,
            FilePath: document.FilePath,
            ContentType: document.ContentType,
            FileSizeBytes: document.FileSizeBytes,
            DocumentType: document.DocumentType,
            Description: document.Description,
            Version: document.Version,
            PreviousVersionId: document.PreviousVersionId,
            EmployeeId: document.EmployeeId,
            Status: document.Status,
            ReviewNotes: document.ReviewNotes,
            ReviewedByUserId: document.ReviewedByUserId,
            ReviewedAt: document.ReviewedAt,
            IsActive: document.IsActive,
            CreatedOn: document.CreatedOn,
            CreatedBy: document.CreatedBy,
            UpdatedOn: document.UpdatedOn
        );
    }
}
