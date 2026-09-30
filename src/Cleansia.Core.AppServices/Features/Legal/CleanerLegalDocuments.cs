using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Legal;

/// <summary>
/// The employee-audience documents in force for a cleaner's market and whether the cleaner holds the
/// current version of each. Nothing in force gates nothing, so approval and the take run unchanged
/// until the texts are seeded.
/// </summary>
public static class CleanerLegalDocuments
{
    /// <summary>
    /// The caller's employee id and the market their documents are read in: the one they are approved
    /// for, and until then the one their address is in — where their required uploads are read too, and
    /// the market they apply to work in. Null for a caller who is not a cleaner.
    /// </summary>
    public static async Task<(string EmployeeId, string? CountryId)?> MarketOfAsync(
        IEmployeeRepository employeeRepository, string? userId, CancellationToken cancellationToken)
    {
        var employee = await employeeRepository
            .GetQueryable()
            .Where(e => e.UserId == userId)
            .Select(e => new { e.Id, CountryId = e.WorkCountryId ?? (e.Address != null ? e.Address.CountryId : null) })
            .FirstOrDefaultAsync(cancellationToken);

        return employee is null ? null : (employee.Id, employee.CountryId);
    }

    public static async Task<IReadOnlyList<LegalDocument>> InForceAsync(
        ILegalDocumentResolver legalDocumentResolver, string? countryId, CancellationToken cancellationToken)
    {
        var documents = new List<LegalDocument>();
        foreach (var type in LegalDocument.CleanerAcceptedTypes)
        {
            var document = await legalDocumentResolver.ResolveInForceAsync(
                LegalDocumentAudience.Employee, type, countryId, cancellationToken);
            if (document is not null)
            {
                documents.Add(document);
            }
        }

        return documents;
    }

    public static UserConsent? AcceptanceOf(LegalDocument document, IEnumerable<UserConsent> consents) =>
        LegalDocument.CleanerConsentTypeFor(document.Type) is { } consentType
            ? consents.FirstOrDefault(c => c.ConsentType == consentType)
            : null;

    public static async Task<bool> AllAcceptedAsync(
        ILegalDocumentResolver legalDocumentResolver,
        IUserConsentRepository userConsentRepository,
        string userId,
        string? countryId,
        CancellationToken cancellationToken)
    {
        var inForce = await InForceAsync(legalDocumentResolver, countryId, cancellationToken);
        if (inForce.Count == 0)
        {
            return true;
        }

        var consents = await userConsentRepository.GetByUserIdNoTrackingAsync(userId, cancellationToken);
        return inForce.All(document => AcceptanceOf(document, consents)?.Covers(document) == true);
    }
}
