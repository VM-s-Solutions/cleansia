using System.Reflection;
using System.Text.RegularExpressions;
using Cleansia.Core.AppServices.Features.Company.DTOs;
using Cleansia.Core.AppServices.Features.Employees.DTOs;
using Cleansia.Core.AppServices.Features.Gdpr.DTOs;

namespace Cleansia.Tests.Features.Employees;

/// <summary>
/// ADR-0034 D8.6 / verification 9b — "a payout identifier lives on exactly one DTO family and nowhere
/// else" is a rule a test can check, and the child-entity shape does NOT enforce it on its own: the
/// existing employee mappers are hand-written positional constructions that each merely *chose* to omit
/// the account. Without this test the rule is something a future author has to remember.
/// </summary>
public class PayoutDtoSurfaceTests
{
    private static readonly string[] PayoutIdentifierMembers =
    [
        "Iban", "IBAN", "AccountNumber", "AccountPrefix", "BankCode", "Swift", "Bic", "BankAccountNumber",
    ];

    /// <summary>
    /// The named family: the three payout read DTOs (D8.2) and the subject-access export, which carries
    /// the subject's own data by law (AC9). Nothing else may declare one.
    /// </summary>
    private static readonly Type[] AllowedTypes =
    [
        typeof(MyPayoutDetails),
        typeof(MaskedPayoutDetails),
        typeof(RevealedPayoutDetails),
        typeof(GdprExportPayoutDetailsDto),
        typeof(GdprExportEmployeeDto),
        // Cleansia's OWN account, printed on the customer receipt as the payee. A different record with a
        // different audience — not a cleaner's payout destination, and not what ADR-0034 governs.
        typeof(CompanyInfoDetailDto),
    ];

    [Fact]
    public void No_Feature_Dto_Outside_The_Payout_Family_Declares_A_Payout_Identifier()
    {
        var offenders = typeof(MyPayoutDetails).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsPublic: true } && t.Namespace?.Contains(".DTOs") == true)
            .Where(t => !AllowedTypes.Contains(t))
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => PayoutIdentifierMembers.Contains(p.Name))
                .Select(p => $"{t.FullName}.{p.Name}"))
            .ToList();

        Assert.True(offenders.Count == 0,
            "ADR-0034 D8: payout identifiers ride the payout DTO family only. Offending members:\n  " +
            string.Join("\n  ", offenders));
    }

    [Fact]
    public void The_Masked_Admin_Dto_Has_No_Unmasked_Field_At_All()
    {
        var members = typeof(MaskedPayoutDetails)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        Assert.DoesNotContain("Iban", members);
        Assert.DoesNotContain("AccountNumber", members);
        Assert.DoesNotContain("Swift", members);
        Assert.DoesNotContain("HolderName", members);
    }

    /// <summary>
    /// Verification 9b, second half. The gate reads a scalar precisely so no list query ever needs the
    /// payout navigation; adding the include to "fix" the admin grid would materialize the full unmasked
    /// record on the paged path — the exposure the read contract exists to close.
    /// </summary>
    [Fact]
    public void No_Paged_Or_List_Query_Includes_The_Payout_Navigation()
    {
        // Forbidden everywhere, not only on the paged path: the id-keyed repository getter is the way in,
        // so an Include is never needed and one added to "fix the grid" is the exposure itself.
        var appServicesRoot = Path.Combine(SolutionRoot(), "Cleansia.Core.AppServices");
        var offenders = Directory
            .EnumerateFiles(appServicesRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => Regex.IsMatch(File.ReadAllText(path), @"\.(Include|ThenInclude)\([^)]*PayoutDetails"))
            .ToList();

        Assert.True(offenders.Count == 0,
            "ADR-0034 D1.1: .Include(e => e.PayoutDetails) is forbidden — read the row through " +
            "IEmployeePayoutDetailsRepository instead. Files:\n  " + string.Join("\n  ", offenders));
    }

    private static string SolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Cleansia.Api.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Cleansia.Api.sln not found above the test binaries.");
    }
}
