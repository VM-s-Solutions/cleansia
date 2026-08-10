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

        Assert.Empty(PayCoverage.FindGaps([Service(ServiceA)], configs, employeeId: null));
        Assert.Empty(PayCoverage.FindGaps([Service(ServiceA)], configs, employeeId: "emp-1"));
        Assert.Empty(PayCoverage.FindGaps([Service(ServiceA)], configs, employeeId: "emp-2"));
    }

    [Fact]
    public void An_Override_Covers_Its_Own_Employee_Only()
    {
        var configs = new[] { ServiceConfig(ServiceA, employeeId: "emp-1") };

        Assert.Empty(PayCoverage.FindGaps([Service(ServiceA)], configs, employeeId: "emp-1"));

        var otherEmployee = Assert.Single(PayCoverage.FindGaps([Service(ServiceA)], configs, employeeId: "emp-2"));
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

        var gap = Assert.Single(PayCoverage.FindGaps([Service(ServiceA)], configs, employeeId: null));
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
            PayCoverage.FindGaps([Package(sharedId)], [ServiceConfig(sharedId)], employeeId: null));

        Assert.Equal(PayCoverageTargetKind.Package, gap.Kind);
    }

    [Fact]
    public void A_Package_Config_Never_Covers_A_Service_With_The_Same_Id()
    {
        const string sharedId = "shared-id";

        var gap = Assert.Single(
            PayCoverage.FindGaps([Service(sharedId)], [PackageConfig(sharedId)], employeeId: null));

        Assert.Equal(PayCoverageTargetKind.Service, gap.Kind);
    }

    [Fact]
    public void Both_Kinds_Are_Reported_And_Covered_Entries_Are_Not()
    {
        var catalogue = new[] { Service(ServiceA), Service("svc-B"), Package(PackageA), Package("pkg-B") };
        var configs = new[] { ServiceConfig(ServiceA), PackageConfig(PackageA) };

        var gaps = PayCoverage.FindGaps(catalogue, configs, employeeId: null);

        Assert.Equal(2, gaps.Count);
        Assert.Contains(gaps, g => g is { Kind: PayCoverageTargetKind.Service, Id: "svc-B" });
        Assert.Contains(gaps, g => g is { Kind: PayCoverageTargetKind.Package, Id: "pkg-B" });
    }

    [Fact]
    public void An_Empty_Catalogue_Has_No_Gaps_Even_With_No_Configs()
    {
        Assert.Empty(PayCoverage.FindGaps([], [], employeeId: null));
        Assert.Empty(PayCoverage.FindGaps([], [], employeeId: "emp-1"));
    }

    [Fact]
    public void Everything_Is_A_Gap_When_Nothing_Is_Configured()
    {
        var catalogue = new[] { Service(ServiceA), Package(PackageA) };

        Assert.Equal(2, PayCoverage.FindGaps(catalogue, [], employeeId: null).Count);
        Assert.Equal(2, PayCoverage.FindGaps(catalogue, [], employeeId: "emp-1").Count);
    }

    [Fact]
    public void A_Config_For_Another_Target_Covers_Nothing()
    {
        var gap = Assert.Single(
            PayCoverage.FindGaps([Service(ServiceA)], [ServiceConfig("svc-other")], employeeId: null));

        Assert.Equal(ServiceA, gap.Id);
    }

    [Fact]
    public void Applies_Is_The_Estimator_Lookup_Disjunction()
    {
        var platformWide = ServiceConfig(ServiceA);
        var mine = ServiceConfig(ServiceA, employeeId: "emp-1");
        var somebodyElses = ServiceConfig(ServiceA, employeeId: "emp-2");

        Assert.True(PayCoverage.Applies(platformWide, "emp-1"));
        Assert.True(PayCoverage.Applies(platformWide, employeeId: null));
        Assert.True(PayCoverage.Applies(mine, "emp-1"));
        Assert.False(PayCoverage.Applies(mine, "emp-2"));
        Assert.False(PayCoverage.Applies(mine, employeeId: null));
        Assert.False(PayCoverage.Applies(somebodyElses, "emp-1"));
    }
}
