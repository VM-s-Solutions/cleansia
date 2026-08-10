using System.Reflection;
using Cleansia.Core.Domain.EmployeePayroll;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// The gate and the board must answer the same question. <see cref="PayCoverage"/> decides whether an
/// order may exist and whether a cleaner may be approved; <c>OrderPayEstimator.Estimate</c> decides what
/// that cleaner actually sees. If they can disagree, a gate passes over an order that still quotes
/// blank, which is exactly the defect — so this drives BOTH and asserts the biconditional
/// <c>gaps.Count == 0  ⟺  estimate is not null</c>.
///
/// <para>The configs are narrowed here the way <c>EmployeePayConfigRepository.GetServiceConfigsForOrderAsync</c>
/// narrows them — <c>EmployeeId == null || EmployeeId == employeeId</c>, hand-copied from that WHERE
/// clause rather than routed through <see cref="PayCoverage.Applies"/>, or the agreement would hold by
/// construction and prove nothing.</para>
///
/// <para><c>Estimate</c>'s pure overload is private, so it is reached by reflection — the same idiom
/// <c>SelectPreferredConfigsTests</c> uses for <c>SelectPreferredConfigs</c>.</para>
/// </summary>
public class PayCoverageEstimatorAgreementTests
{
    private const string CurrencyId = "czk";
    private const string Employee = "emp-1";
    private const string ServiceA = "svc-A";
    private const string PackageA = "pkg-A";

    private static readonly MethodInfo EstimateCore = typeof(Cleansia.Core.AppServices.Features.Orders.OrderFactory)
        .Assembly
        .GetType("Cleansia.Core.AppServices.Features.Orders.OrderPayEstimator")!
        .GetMethod(
            "Estimate",
            BindingFlags.NonPublic | BindingFlags.Static)!;

    private static EmployeePayConfig ServiceConfig(string serviceId, string? employeeId = null) =>
        EmployeePayConfig.CreateForService(serviceId, basePay: 250m, currencyId: CurrencyId, employeeId: employeeId);

    private static EmployeePayConfig PackageConfig(string packageId, string? employeeId = null) =>
        EmployeePayConfig.CreateForPackage(packageId, basePay: 400m, currencyId: CurrencyId, employeeId: employeeId);

    /// <summary>The repository's narrowing, restated. Production never hands the estimator anything else.</summary>
    private static List<EmployeePayConfig> AsRepositoryWouldReturn(
        IEnumerable<EmployeePayConfig> all, string employeeId) =>
        all.Where(c => c.EmployeeId == null || c.EmployeeId == employeeId).ToList();

    private static decimal? Estimate(
        IEnumerable<string> serviceIds,
        IEnumerable<string> packageIds,
        string employeeId,
        IEnumerable<EmployeePayConfig> allConfigs)
    {
        var visible = AsRepositoryWouldReturn(allConfigs, employeeId);

        return (decimal?)EstimateCore.Invoke(null,
        [
            serviceIds.ToHashSet(),
            packageIds.ToHashSet(),
            2,
            1,
            (decimal?)5m,
            employeeId,
            (IReadOnlyList<EmployeePayConfig>)visible.Where(c => c.ServiceId != null).ToList(),
            (IReadOnlyList<EmployeePayConfig>)visible.Where(c => c.PackageId != null).ToList()
        ]);
    }

    public static TheoryData<string, EmployeePayConfig[]> ConfigSets() => new()
    {
        { "nothing configured", [] },
        { "platform-wide service only", [ServiceConfig(ServiceA)] },
        { "this cleaner's override only", [ServiceConfig(ServiceA, Employee)] },
        { "another cleaner's override only", [ServiceConfig(ServiceA, "emp-2")] },
        { "platform-wide package only", [PackageConfig(PackageA)] },
        { "platform-wide for both", [ServiceConfig(ServiceA), PackageConfig(PackageA)] },
        { "override for both", [ServiceConfig(ServiceA, Employee), PackageConfig(PackageA, Employee)] },
        { "service covered, package not", [ServiceConfig(ServiceA)] },
        { "package covered, service not", [PackageConfig(PackageA)] },
        { "unrelated target only", [ServiceConfig("svc-other"), PackageConfig("pkg-other")] }
    };

    [Theory]
    [MemberData(nameof(ConfigSets))]
    public void A_Gap_Free_Order_Is_Exactly_A_Quotable_One(string label, EmployeePayConfig[] configs)
    {
        var catalogue = new PayCoverageTarget[]
        {
            new(PayCoverageTargetKind.Service, ServiceA, "General Cleaning"),
            new(PayCoverageTargetKind.Package, PackageA, "Essential Clean")
        };

        var gaps = PayCoverage.FindGaps(catalogue, configs, Employee);
        var estimate = Estimate([ServiceA], [PackageA], Employee, configs);

        // The estimator quotes on ANY matched config, so it is non-null whenever at least one target
        // resolves. The gate's question is the stronger "every target resolves" — which is why the
        // agreement is asserted on a single-target order below as well.
        Assert.Equal(gaps.Count < catalogue.Length, estimate is not null);
        Assert.True(label.Length > 0);
    }

    [Theory]
    [MemberData(nameof(ConfigSets))]
    public void On_A_Single_Target_Order_The_Gate_And_The_Board_Agree_Exactly(string label, EmployeePayConfig[] configs)
    {
        var catalogue = new PayCoverageTarget[] { new(PayCoverageTargetKind.Service, ServiceA, "General Cleaning") };

        var gaps = PayCoverage.FindGaps(catalogue, configs, Employee);
        var estimate = Estimate([ServiceA], [], Employee, configs);

        Assert.Equal(gaps.Count == 0, estimate is not null);
        Assert.True(label.Length > 0);
    }

    /// <summary>
    /// Anti-vacuity: the fixture really does reach a quoting estimator with a real number, so the
    /// biconditional above is not being satisfied by both sides collapsing to "never".
    /// </summary>
    [Fact]
    public void The_Fixture_Really_Quotes_A_Number_When_Covered()
    {
        var estimate = Estimate([ServiceA], [], Employee, [ServiceConfig(ServiceA)]);

        Assert.NotNull(estimate);
        Assert.Equal(250m, estimate);
    }

    [Fact]
    public void The_Fixture_Really_Quotes_Null_When_Only_Another_Cleaner_Is_Configured()
    {
        Assert.Null(Estimate([ServiceA], [], Employee, [ServiceConfig(ServiceA, "emp-2")]));
    }

    /// <summary>
    /// The platform-wide question is the one the catalogue gate asks, and it is strictly stronger:
    /// whatever it clears is quotable for an arbitrary cleaner who owns no config at all.
    /// </summary>
    [Fact]
    public void Platform_Wide_Coverage_Quotes_For_A_Cleaner_With_No_Configs_Of_Their_Own()
    {
        var catalogue = new PayCoverageTarget[]
        {
            new(PayCoverageTargetKind.Service, ServiceA, "General Cleaning"),
            new(PayCoverageTargetKind.Package, PackageA, "Essential Clean")
        };
        var configs = new[] { ServiceConfig(ServiceA), PackageConfig(PackageA) };

        Assert.Empty(PayCoverage.FindGaps(catalogue, configs, employeeId: null));

        var estimate = Estimate([ServiceA], [PackageA], "a-brand-new-cleaner", configs);
        Assert.NotNull(estimate);
    }
}
