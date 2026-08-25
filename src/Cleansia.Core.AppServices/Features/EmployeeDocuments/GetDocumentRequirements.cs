using Cleansia.Core.AppServices.Features.EmployeeDocuments.DTOs;
using Cleansia.Core.Domain.Repositories;
using MediatR;

namespace Cleansia.Core.AppServices.Features.EmployeeDocuments;

/// <summary>
/// The document types a country expects, as the admin screen reads them.
///
/// <para><b>Admin-managed rather than a constant</b>, on the owner's ruling: requirements change with
/// the law, and a change that needs a release is a change that waits for one.</para>
///
/// <para>Returns the optional rows too. Only the caller knows whether it is drawing a checklist or
/// deciding an approval, and <see cref="Cleansia.Core.AppServices.Features.Employees.ApproveEmployee"/>
/// filters to the required ones itself.</para>
/// </summary>
public class GetDocumentRequirements
{
    public record Request(string CountryId) : IRequest<IEnumerable<DocumentRequirementDto>>;

    public class Handler(IEmployeeDocumentRequirementRepository repository)
        : IRequestHandler<Request, IEnumerable<DocumentRequirementDto>>
    {
        public async Task<IEnumerable<DocumentRequirementDto>> Handle(
            Request request, CancellationToken cancellationToken)
        {
            var rows = await repository.GetForCountryAsync(request.CountryId, cancellationToken);

            return rows.Select(r => new DocumentRequirementDto(
                r.Id, r.CountryId, r.DocumentType, r.IsRequired, r.SortOrder));
        }
    }
}
