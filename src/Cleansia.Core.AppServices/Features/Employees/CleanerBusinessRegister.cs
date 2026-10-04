using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Services.BusinessRegistry;

namespace Cleansia.Core.AppServices.Features.Employees;

/// <summary>
/// The public register's answer for a cleaner's registration number, asked of the register of the
/// country it is judged against. A blank number or a country the platform does not know consults
/// nothing, so the rule that owns the field is the one that refuses it.
/// </summary>
internal static class CleanerBusinessRegister
{
    public static async Task<BusinessRegistryRecord> LookupAsync(
        ICountryRepository countryRepository,
        IBusinessRegistry businessRegistry,
        string? countryId,
        string registrationNumber,
        CancellationToken cancellationToken)
    {
        var country = string.IsNullOrEmpty(countryId) || string.IsNullOrWhiteSpace(registrationNumber)
            ? null
            : await countryRepository.GetByIdAsync(countryId, cancellationToken);

        return country is null
            ? BusinessRegistryRecord.NotConsulted
            : await businessRegistry.LookupAsync(country.IsoCode, registrationNumber, cancellationToken);
    }

    /// <summary>
    /// A cleaner's own save refuses only a number the register does not know. A register that does not
    /// answer lets the save through: the check that binds is the one at approval.
    /// </summary>
    public static bool AcceptsOnSave(BusinessRegistryRecord record)
        => record.Answer != BusinessRegistryAnswer.NotRegistered;
}
