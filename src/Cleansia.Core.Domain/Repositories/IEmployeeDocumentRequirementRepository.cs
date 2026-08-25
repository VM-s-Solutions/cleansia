using Cleansia.Core.Domain.Documents;

namespace Cleansia.Core.Domain.Repositories;

public interface IEmployeeDocumentRequirementRepository
    : IRepository<EmployeeDocumentRequirement, string>
{
    /// <summary>
    /// Every requirement configured for a country, ordered the way the cleaner should be asked for
    /// them. Includes the optional ones: the upload screen lists both, and only the caller knows
    /// whether it is drawing a checklist or deciding an approval.
    /// </summary>
    Task<IReadOnlyList<EmployeeDocumentRequirement>> GetForCountryAsync(
        string countryId,
        CancellationToken cancellationToken);
}
