using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Legal;

/// <summary>
/// Who a legal text is written for. A customer and an employee accept different documents
/// (ADR-0041), so the audience is part of a document's identity, never inferred from its type.
/// </summary>
[SwaggerEnumAsInt]
public enum LegalDocumentAudience
{
    Customer = 0,
    Employee = 1
}
