using Cleansia.Core.Domain.EmployeePayroll;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// <see cref="PayCoverage"/> is the one predicate both pay gates read. It answers "can pay be resolved
/// for this catalogue entry and this cleaner", where a null cleaner asks the strictly stronger
/// platform-wide question — the answer that holds for every cleaner, including one who has not
/// registered yet.
/// </summary>
public class PayCoverageTests
{
    private const string CurrencyId = "czk";
    private const string ServiceA = "svc-A";
    private const string PackageA = "pkg-A";

    private static EmployeePayConfig ServiceConfig(string serviceId, string? employeeId = null) =>
        EmployeePayConfig.CreateForService(serviceId, basePay: 100m, currencyId: CurrencyId, employeeId: employeeId);

    private static EmployeePayConfig PackageConfig(string packageId, string? employeeId = null) =>
        EmployeePayConfig.CreateForPackage(packageId, basePay: 100m, currencyId: CurrencyId, employeeId: employeeId);

    private static PayCoverageTarget Service(string id) =>
        new(PayCoverageTargetKind.Service, id, $"Service {id}");

    private static PayCoverageTarget Package(string id) =>
        new(PayCoverageTargetKind.Package, id, $"Package {id}");

    [Fact]
    public void Platform_Wide_Config_Covers_Every_Employee()
    {
        var configs = new[] { ServiceConfig(ServiceA) };

        Assert.Empty(PayCoverage.FindGaps([Service(ServiceA)], configs, employeeId: null, currencyId: CurrencyId));
        Assert.Empty(PayCoverage.FindGaps([Service(ServiceA)], configs, employeeId: "emp-1", currencyId: CurrencyId));
        Assert.Empty(PayCoverage.FindGaps([Service(ServiceA)], configs, employeeId: "emp-2", currencyId: CurrencyId));
    }

    [Fact]
    public void An_Override_Covers_Its_Own_Employee_Only()
    {
        var configs = new[] { ServiceConfig(ServiceA, employeeId: "emp-1") };

        Assert.Empty(PayCoverage.FindGaps([Service(ServiceA)], configs, employeeId: "emp-1", currencyId: CurrencyId));

        var otherEmployee = Assert.Single(PayCoverage.FindGaps([Service(ServiceA)], configs, employeeId: "emp-2", currencyId: CurrencyId));
        Assert.Equal(ServiceA, otherEmployee.Id);
    }

    /// <summary>
    /// The gap the ticket exists to close: a catalogue entry that only ever got a per-employee row is
    /// unquotable for everybody else, so it must fail the platform-wide question.
    /// </summary>
    [Fact]
    public void An_Override_Does_Not_Answer_The_Platform_Wide_Question()
    {
        var configs = new[] { ServiceConfig(ServiceA, employeeId: "emp-1") };

        var gap = Assert.Single(PayCoverage.FindGaps([Service(ServiceA)], configs, employeeId: null, currencyId: CurrencyId));
        Assert.Equal(ServiceA, gap.Id);
        Assert.Equal(PayCoverageTargetKind.Service, gap.Kind);
    }

    /// <summary>
    /// Service ids and package ids live in separate id spaces but the same column family, so a
    /// coverage check that compared ids without the kind would silently cross-cover.
    /// </summary>
    [Fact]
    public void A_Service_Config_Never_Covers_A_Package_With_The_Same_Id()
    {
        const string sharedId = "shared-id";

        var gap = Assert.Single(
            PayCoverage.FindGaps([Package(sharedId)], [ServiceConfig(sharedId)], employeeId: null, currencyId: CurrencyId));

        Assert.Equal(PayCoverageTargetKind.Package, gap.Kind);
    }

    [Fact]
    public void A_Package_Config_Never_Covers_A_Service_With_The_Same_Id()
    {
        const string sharedId = "shared-id";

        var gap = Assert.Single(
            PayCoverage.FindGaps([Service(sharedId)], [PackageConfig(sharedId)], employeeId: null, currencyId: CurrencyId));

        Assert.Equal(PayCoverageTargetKind.Service, gap.Kind);
    }

    [Fact]
    public void Both_Kinds_Are_Reported_And_Covered_Entries_Are_Not()
    {
        var catalogue = new[] { Service(ServiceA), Service("svc-B"), Package(PackageA), Package("pkg-B") };
        var configs = new[] { ServiceConfig(ServiceA), PackageConfig(PackageA) };

        var gaps = PayCoverage.FindGaps(catalogue, configs, employeeId: null, currencyId: CurrencyId);

        Assert.Equal(2, gaps.Count);
        Assert.Contains(gaps, g => g is { Kind: PayCoverageTargetKind.Service, Id: "svc-B" });
        Assert.Contains(gaps, g => g is { Kind: PayCoverageTargetKind.Package, Id: "pkg-B" });
    }

    [Fact]
    public void An_Empty_Catalogue_Has_No_Gaps_Even_With_No_Configs()
    {
        Assert.Empty(PayCoverage.FindGaps([], [], employeeId: null, currencyId: CurrencyId));
        Assert.Empty(PayCoverage.FindGaps([], [], employeeId: "emp-1", currencyId: CurrencyId));
    }

    [Fact]
    public void Everything_Is_A_Gap_When_Nothing_Is_Configured()
    {
        var catalogue = new[] { Service(ServiceA), Package(PackageA) };

        Assert.Equal(2, PayCoverage.FindGaps(catalogue, [], employeeId: null, currencyId: CurrencyId).Count);
        Assert.Equal(2, PayCoverage.FindGaps(catalogue, [], employeeId: "emp-1", currencyId: CurrencyId).Count);
    }

    [Fact]
    public void A_Config_For_Another_Target_Covers_Nothing()
    {
        var gap = Assert.Single(
            PayCoverage.FindGaps([Service(ServiceA)], [ServiceConfig("svc-other")], employeeId: null, currencyId: CurrencyId));

        Assert.Equal(ServiceA, gap.Id);
    }

    [Fact]
    public void Applies_Is_The_Estimator_Lookup_Disjunction()
    {
        var platformWide = ServiceConfig(ServiceA);
        var mine = ServiceConfig(ServiceA, employeeId: "emp-1");
        var somebodyElses = ServiceConfig(ServiceA, employeeId: "emp-2");

        Assert.True(PayCoverage.Applies(platformWide, "emp-1", CurrencyId));
        Assert.True(PayCoverage.Applies(platformWide, employeeId: null, CurrencyId));
        Assert.True(PayCoverage.Applies(mine, "emp-1", CurrencyId));
        Assert.False(PayCoverage.Applies(mine, "emp-2", CurrencyId));
        Assert.False(PayCoverage.Applies(mine, employeeId: null, CurrencyId));
        Assert.False(PayCoverage.Applies(somebodyElses, "emp-1", CurrencyId));
    }

    // ---------------------------------------------------------------- the currency term

    private static EmployeePayConfig ServiceConfigIn(string currencyId, string serviceId, string? employeeId = null) =>
        EmployeePayConfig.CreateForService(serviceId, basePay: 100m, currencyId: currencyId, employeeId: employeeId);

    /// <summary>
    /// The term the estimator and the writer already carried and the gates did not. A rate in another
    /// currency is not a rate for this order, however good it is: without this, every gate admitted a
    /// EUR order on the strength of a CZK rate and the writer then found nothing.
    /// </summary>
    [Fact]
    public void A_Rate_In_Another_Currency_Does_Not_Cover()
    {
        var eurOnly = new[] { ServiceConfigIn("eur", ServiceA) };

        var gap = Assert.Single(PayCoverage.FindGaps([Service(ServiceA)], eurOnly, employeeId: null, currencyId: CurrencyId));
        Assert.Equal(ServiceA, gap.Id);
        Assert.False(PayCoverage.Applies(eurOnly[0], employeeId: null, CurrencyId));
    }

    [Fact]
    public void A_Rate_In_The_Asked_Currency_Covers_Beside_One_In_Another()
    {
        var both = new[] { ServiceConfigIn("eur", ServiceA), ServiceConfig(ServiceA) };

        Assert.Empty(PayCoverage.FindGaps([Service(ServiceA)], both, employeeId: null, currencyId: CurrencyId));
        Assert.Empty(PayCoverage.FindGaps([Service(ServiceA)], both, employeeId: null, currencyId: "eur"));
    }
}
