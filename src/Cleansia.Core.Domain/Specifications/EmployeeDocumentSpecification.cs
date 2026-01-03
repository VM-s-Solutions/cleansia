using System.Linq.Expressions;
using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.Enums;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class EmployeeDocumentSpecification : BaseSpecification<string?>, ISpecification<EmployeeDocument>
{
    public string? EmployeeId { get; set; }

    public DocumentType? DocumentType { get; set; }

    public DocumentStatus? Status { get; set; }

    public bool? LatestVersionOnly { get; set; }

    public Expression<Func<EmployeeDocument, bool>> SatisfiedBy()
    {
        Specification<EmployeeDocument> specification = new TrueSpecification<EmployeeDocument>();

        if (!string.IsNullOrWhiteSpace(Id))
        {
            specification &= new DirectSpecification<EmployeeDocument>(x => x.Id == Id);
        }

        if (IsActive.HasValue)
        {
            specification &= new DirectSpecification<EmployeeDocument>(x => x.IsActive == IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(EmployeeId))
        {
            specification &= new DirectSpecification<EmployeeDocument>(x => x.EmployeeId == EmployeeId);
        }

        if (DocumentType.HasValue)
        {
            specification &= new DirectSpecification<EmployeeDocument>(x => x.DocumentType == DocumentType.Value);
        }

        if (Status.HasValue)
        {
            specification &= new DirectSpecification<EmployeeDocument>(x => x.Status == Status.Value);
        }

        if (LatestVersionOnly.HasValue && LatestVersionOnly.Value)
        {
            // Only get documents that don't have a newer version
            specification &= new DirectSpecification<EmployeeDocument>(x => string.IsNullOrEmpty(x.PreviousVersionId));
        }

        return specification.SatisfiedBy();
    }

    public static EmployeeDocumentSpecification Create(
        string? id = null,
        bool? isActive = null,
        string? employeeId = null,
        DocumentType? documentType = null,
        DocumentStatus? status = null,
        bool? latestVersionOnly = null) =>
        new()
        {
            Id = id,
            IsActive = isActive,
            EmployeeId = employeeId,
            DocumentType = documentType,
            Status = status,
            LatestVersionOnly = latestVersionOnly
        };
}
