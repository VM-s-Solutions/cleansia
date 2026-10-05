using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Services.BusinessRegistry;

namespace Cleansia.Core.AppServices.Features.Employees;

/// <summary>
/// The public register's answer for a cleaner's registration number, asked of the register of the
/// country it is judged against. A blank number or a country the platform does not know consults
/// nothing, so the rule that owns the field is the one that refuses it.
/// </summary>
public static class CleanerBusinessRegister
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
    /// The country whose register judges a cleaner's number on their own save: the one they are approved to
    /// work in, which is the register approval asked, else the country of their address. Never a country the
    /// client names for the check alone, so an approved cleaner cannot name one no register is wired for.
    /// </summary>
    public static string? RegisterCountryId(Employee employee, string? addressCountryId)
        => employee.WorkCountryId ?? addressCountryId;

    /// <summary>
    /// The refusal a register answer earns. Approval grade, which approval itself and an approved cleaner's
    /// changed number are held to, refuses an ended business, a business with no trade licence in force and
    /// a register that does not answer, because a person is there to try again. Any other save refuses only
    /// a number the register does not hold; approval judges the rest before the cleaner can work.
    /// </summary>
    public static string? Refusal(BusinessRegistryRecord record, bool approvalGrade) => record switch
    {
        { Answer: BusinessRegistryAnswer.NotRegistered } => BusinessErrorMessage.RegistrationNumberNotRegistered,
        _ when !approvalGrade => null,
        { Answer: BusinessRegistryAnswer.Unavailable } => BusinessErrorMessage.EmployeeBusinessRegistryUnavailable,
        { Answer: BusinessRegistryAnswer.Registered, Ceased: true } => BusinessErrorMessage.EmployeeBusinessCeased,
        { Answer: BusinessRegistryAnswer.Registered, TradeLicenceActive: false } => BusinessErrorMessage.EmployeeTradeLicenceInactive,
        _ => null,
    };

    public static bool Changes(Employee employee, string? registrationNumber)
        => !string.Equals(employee.RegistrationNumber?.Trim(), registrationNumber?.Trim(), StringComparison.Ordinal);
}
