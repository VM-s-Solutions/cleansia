using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.EmployeeDocuments.Filters;

public class EmployeeDocumentFilter
{
    public bool? IsActive { get; init; }
    public string? EmployeeId { get; init; }
    public DocumentType? DocumentType { get; init; }
    public DocumentStatus? Status { get; init; }
    public bool? LatestVersionOnly { get; init; }
}
