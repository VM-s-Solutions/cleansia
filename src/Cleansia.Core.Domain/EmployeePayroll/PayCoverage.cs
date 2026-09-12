namespace Cleansia.Core.Domain.EmployeePayroll;

public enum PayCoverageTargetKind
{
    Service = 1,
    Package = 2
}

public sealed record PayCoverageTarget(PayCoverageTargetKind Kind, string Id, string Name);

/// <summary>
/// Whether a cleaner's pay can be resolved for a catalogue entry.
///
/// <para>This is the single predicate behind both pay gates — "may this become bookable" and "may this
/// cleaner be approved" — because they are one question asked from two directions. It exists as a
/// predicate rather than as two checks because the two gates compose: <see cref="Applies"/> with a null
/// <c>employeeId</c> is the strictly stronger platform-wide question, and a platform-wide row covers
/// EVERY cleaner, including one who has not registered yet. So guaranteeing the platform-wide answer at
/// the catalogue end is what makes the approval end satisfiable at all.</para>
///
/// <para><b>The disjunction is copied from the estimator's lookup, not paraphrased from it.</b>
/// <c>OrderPayEstimator</c> resolves a config per target as
/// <c>FirstOrDefault(c =&gt; c.EmployeeId == employeeId) ?? First()</c> over a set the repository has
/// already narrowed to <c>EmployeeId == null || EmployeeId == employeeId</c>, so a target is quotable
/// for a cleaner exactly when a row satisfying that disjunction exists. A coverage rule that disagreed
/// with it in either direction would gate on something other than what the cleaner's board shows.
/// <c>IsActive</c> is deliberately NOT a term: the estimator's lookup does not carry one either, and a
/// predicate that filtered rows the estimator still quotes from would report a gap that is not one.</para>
///
/// <para><b>The currency term is copied the same way.</b> The estimator narrows to
/// <c>c.CurrencyId == orderCurrencyId</c> and the pay writer reads only rows in the order's currency,
/// so a rate in another currency is not a rate for this order — however good it is. Without this term
/// every gate admitted a EUR order on the strength of a CZK rate, and the writer then found nothing:
/// an order on every board with no pay on any of them.</para>
/// </summary>
public static class PayCoverage
{
    public static bool Applies(EmployeePayConfig config, string? employeeId, string currencyId) =>
        config.CurrencyId == currencyId
        && (config.EmployeeId is null || (employeeId is not null && config.EmployeeId == employeeId));

    public static IReadOnlyList<PayCoverageTarget> FindGaps(
        IEnumerable<PayCoverageTarget> catalogue,
        IEnumerable<EmployeePayConfig> configs,
        string? employeeId,
        string currencyId)
    {
        var applicable = configs.Where(config => Applies(config, employeeId, currencyId)).ToList();

        var coveredServiceIds = applicable
            .Where(config => config.ServiceId is not null)
            .Select(config => config.ServiceId!)
            .ToHashSet();

        var coveredPackageIds = applicable
            .Where(config => config.PackageId is not null)
            .Select(config => config.PackageId!)
            .ToHashSet();

        return catalogue
            .Where(target => !CoveredIds(target.Kind).Contains(target.Id))
            .ToList();

        HashSet<string> CoveredIds(PayCoverageTargetKind kind) =>
            kind == PayCoverageTargetKind.Service ? coveredServiceIds : coveredPackageIds;
    }
}
