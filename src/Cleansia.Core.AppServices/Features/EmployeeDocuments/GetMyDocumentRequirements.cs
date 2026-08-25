using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.EmployeeDocuments.DTOs;
using Cleansia.Core.Domain.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.EmployeeDocuments;

/// <summary>
/// What this cleaner still owes, resolved against what they have already uploaded.
///
/// <para><b>This is the placeholder the partner apps show before anything is uploaded.</b> A cleaner
/// used to open the documents screen and find an empty box: nothing said which papers we wanted, so
/// the first step of onboarding was contacting support to ask. The checklist answers that in the app.</para>
///
/// <para><b>Keyed on the WORK country, falling back to the address country.</b> Work country is the
/// jurisdiction the requirements belong to, but it is only set at APPROVAL — and this screen exists
/// precisely for people who are not approved yet. Falling back to where they live is the best
/// available guess at where they will work, and it is a checklist rather than a gate, so a wrong guess
/// costs a misleading prompt rather than a refusal. The gate itself
/// (<c>ApproveEmployee</c>) only ever reads the work country.</para>
/// </summary>
public class GetMyDocumentRequirements
{
    public record Request : IRequest<IEnumerable<MyDocumentRequirementDto>>;

    public class Handler(
        IEmployeeRepository employeeRepository,
        IEmployeeDocumentRequirementRepository requirementRepository,
        IUserSessionProvider userSessionProvider)
        : IRequestHandler<Request, IEnumerable<MyDocumentRequirementDto>>
    {
        public async Task<IEnumerable<MyDocumentRequirementDto>> Handle(
            Request request, CancellationToken cancellationToken)
        {
            var userEmail = userSessionProvider.GetUserEmail();

            var employee = await employeeRepository
                .GetQueryable()
                .Include(e => e.Documents)
                .Include(e => e.Address)
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.User!.Email == userEmail, cancellationToken);

            if (employee is null)
            {
                return [];
            }

            var countryId = employee.WorkCountryId ?? employee.Address?.CountryId;

            if (string.IsNullOrEmpty(countryId))
            {
                return [];
            }

            var requirements = await requirementRepository.GetForCountryAsync(countryId, cancellationToken);

            return requirements.Select(requirement =>
            {
                // The NEWEST active document of that type answers for it. A replaced document leaves
                // its superseded version behind, and reading the older one would report a stale
                // status — an approved v1 saying the pending v2 is already accepted.
                var held = employee.Documents
                    .Where(d => d.IsActive && d.DocumentType == requirement.DocumentType)
                    .OrderByDescending(d => d.Version)
                    .ThenByDescending(d => d.CreatedOn)
                    .FirstOrDefault();

                return new MyDocumentRequirementDto(
                    DocumentType: requirement.DocumentType,
                    IsRequired: requirement.IsRequired,
                    SortOrder: requirement.SortOrder,
                    Status: held?.Status,
                    DocumentId: held?.Id);
            });
        }
    }
}
