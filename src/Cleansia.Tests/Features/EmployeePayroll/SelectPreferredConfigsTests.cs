using System.Reflection;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.Domain.EmployeePayroll;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// T-0125 (TC-PAY) / IMP-3 — the per-employee override precedence inside
/// <see cref="CalculateOrderPay.Handler"/>.<c>SelectPreferredConfigs</c> (surface #4, AC5).
///
/// The contract: configs are grouped by their target id (ServiceId or PackageId); for each group the
/// per-employee override (<c>EmployeeId != null</c>) WINS over the per-service/per-package default
/// (<c>EmployeeId == null</c>); when only the default exists, it is the fallback. Groups with a null
/// key are dropped.
///
/// <c>SelectPreferredConfigs</c> is <c>private static</c>, so it is invoked by reflection — the test
/// pins the selection LOGIC (which config object survives), not an accessibility detail. This mirrors
/// the existing handler-test idiom that reaches non-public members via reflection.
/// </summary>
public class SelectPreferredConfigsTests
{
    private const string CurrencyId = "czk";
    private const string ServiceA = "svc-A";
    private const string ServiceB = "svc-B";

    private static EmployeePayConfig ServiceConfig(string serviceId, decimal basePay, string? employeeId) =>
        EmployeePayConfig.CreateForService(
            serviceId: serviceId,
            basePay: basePay,
            currencyId: CurrencyId,
            employeeId: employeeId);

    /// <summary>Invokes the private static Handler.SelectPreferredConfigs via reflection.</summary>
    private static List<EmployeePayConfig> SelectByServiceId(IEnumerable<EmployeePayConfig> configs)
    {
        var method = typeof(CalculateOrderPay.Handler)
            .GetMethod("SelectPreferredConfigs", BindingFlags.NonPublic | BindingFlags.Static)!;

        Func<EmployeePayConfig, string?> selector = c => c.ServiceId;

        var result = (IEnumerable<EmployeePayConfig>)method.Invoke(null, [configs, selector])!;
        return result.ToList();
    }

    [Fact]
    public void Override_With_EmployeeId_Wins_Over_Default_For_Same_Service()
    {
        var defaultConfig = ServiceConfig(ServiceA, basePay: 100m, employeeId: null);
        var overrideConfig = ServiceConfig(ServiceA, basePay: 175m, employeeId: "emp-1");

        var selected = SelectByServiceId([defaultConfig, overrideConfig]);

        var single = Assert.Single(selected);
        Assert.Same(overrideConfig, single);
        Assert.Equal("emp-1", single.EmployeeId);
        Assert.Equal(175m, single.BasePay);
    }

    [Fact]
    public void Override_Wins_Regardless_Of_Order_When_Default_Listed_First()
    {
        var defaultConfig = ServiceConfig(ServiceA, basePay: 100m, employeeId: null);
        var overrideConfig = ServiceConfig(ServiceA, basePay: 175m, employeeId: "emp-1");

        // Default appears BEFORE the override in the input sequence — the override must still win
        // (the selector explicitly prefers EmployeeId != null, not "first in list").
        var selected = SelectByServiceId([defaultConfig, overrideConfig]);

        Assert.Same(overrideConfig, Assert.Single(selected));
    }

    [Fact]
    public void Falls_Back_To_Default_When_No_Override_Exists()
    {
        var defaultConfig = ServiceConfig(ServiceA, basePay: 100m, employeeId: null);

        var selected = SelectByServiceId([defaultConfig]);

        var single = Assert.Single(selected);
        Assert.Same(defaultConfig, single);
        Assert.Null(single.EmployeeId);
    }

    [Fact]
    public void Per_Service_Independent_Override_And_Default_Coexist()
    {
        // Service A has an override; Service B only has a default. Each group resolves independently.
        var aDefault = ServiceConfig(ServiceA, basePay: 100m, employeeId: null);
        var aOverride = ServiceConfig(ServiceA, basePay: 175m, employeeId: "emp-1");
        var bDefault = ServiceConfig(ServiceB, basePay: 90m, employeeId: null);

        var selected = SelectByServiceId([aDefault, aOverride, bDefault]);

        Assert.Equal(2, selected.Count);
        Assert.Contains(aOverride, selected);
        Assert.Contains(bDefault, selected);
        Assert.DoesNotContain(aDefault, selected);
    }

    [Fact]
    public void Single_Override_Only_Is_Selected()
    {
        // Only a per-employee config exists (no default) — it is chosen as the sole group member.
        var overrideConfig = ServiceConfig(ServiceA, basePay: 175m, employeeId: "emp-1");

        var selected = SelectByServiceId([overrideConfig]);

        Assert.Same(overrideConfig, Assert.Single(selected));
    }

    [Fact]
    public void Empty_Input_Yields_No_Configs()
    {
        var selected = SelectByServiceId([]);

        Assert.Empty(selected);
    }
}
