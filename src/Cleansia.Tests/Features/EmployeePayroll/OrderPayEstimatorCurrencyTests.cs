using System.Reflection;
using Cleansia.Core.Domain.EmployeePayroll;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// A pay estimate is denominated, and the estimator is the only thing that knows in which currency.
///
/// <para><b>Why the narrowing lives here rather than only in the query.</b> The repository reads are
/// scoped to a SET of currencies, because the paged orders list and the partner dashboard batch a whole
/// page of orders into one read and a page can span currencies — narrowing at the query would mean one
/// round trip per currency and would undo the batching that replaced an N+1. So the read returns rows
/// in every currency the page touches, and this is the one place that knows which of the page's orders
/// each estimate is for.</para>
///
/// <para><b>What it prevents.</b> Without the narrowing, a cleaner holding a CZK and a EUR rate for the
/// same service produces one group with two rows, and the selector takes
/// <c>FirstOrDefault(override) ?? First()</c> with no ORDER BY — so the number a cleaner is shown on the
/// job card would depend on Postgres row order. That was unreachable while the unique index forbade a
/// second rate; the same commit that made per-currency pay storable made it reachable, which is why the
/// two move together.</para>
/// </summary>
public class OrderPayEstimatorCurrencyTests
{
    private const string Czk = "cur-czk";
    private const string Eur = "cur-eur";
    private const string ServiceId = "svc-1";
    private const string EmployeeId = "emp-1";

    private static readonly MethodInfo EstimateCore =
        typeof(Cleansia.Core.AppServices.Features.Orders.OrderFactory)
            .Assembly
            .GetType("Cleansia.Core.AppServices.Features.Orders.OrderPayEstimator")!
            .GetMethod("Estimate", BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Two rooms and one bathroom, no travel — so the estimate is base + 3 x perUnit.</summary>
    private static decimal? Estimate(string orderCurrencyId, params EmployeePayConfig[] configs) =>
        (decimal?)EstimateCore.Invoke(null,
        [
            new HashSet<string> { ServiceId },
            new HashSet<string>(),
            2,
            1,
            (decimal?)null,
            orderCurrencyId,
            EmployeeId,
            (IReadOnlyList<EmployeePayConfig>)configs.ToList(),
            (IReadOnlyList<EmployeePayConfig>)new List<EmployeePayConfig>()
        ]);

    private static EmployeePayConfig Config(string currencyId, decimal basePay) =>
        EmployeePayConfig.CreateForService(ServiceId, basePay, currencyId, employeeId: null);

    /// <summary>
    /// THE CASE. Both rates are in hand, as the batched read returns them; the CZK order takes the CZK
    /// rate. The two amounts are far apart so a wrong pick cannot round into a right answer, and neither
    /// is a multiple of the other.
    /// </summary>
    [Fact]
    public void An_Order_Is_Estimated_From_The_Rate_In_Its_Own_Currency()
    {
        var czk = Config(Czk, basePay: 500m);
        var eur = Config(Eur, basePay: 21m);

        Assert.Equal(500m, Estimate(Czk, czk, eur));
        Assert.Equal(21m, Estimate(Eur, czk, eur));
    }

    /// <summary>
    /// ...and the answer does not depend on the order the rows arrive in. This is the property the
    /// group-by's <c>First()</c> cannot provide on its own, and the reason a currency-blind estimator
    /// was a latent coin flip rather than a wrong constant.
    /// </summary>
    [Fact]
    public void The_Estimate_Does_Not_Depend_On_Row_Order()
    {
        var czk = Config(Czk, basePay: 500m);
        var eur = Config(Eur, basePay: 21m);

        Assert.Equal(Estimate(Czk, czk, eur), Estimate(Czk, eur, czk));
    }

    /// <summary>
    /// A cleaner with no rate in the order's currency cannot be quoted. Null is "we cannot quote pay,
    /// hide the chip" — NOT zero, which would read as an offer to work for nothing.
    /// </summary>
    [Fact]
    public void A_Rate_In_Another_Currency_Does_Not_Quote_The_Job()
    {
        Assert.Null(Estimate(Czk, Config(Eur, basePay: 21m)));
    }

    /// <summary>
    /// Anti-vacuity: the same fixture DOES quote when the currency matches, so the null above is the
    /// narrowing rejecting the row rather than the estimator failing for an unrelated reason.
    /// </summary>
    [Fact]
    public void The_Same_Fixture_Quotes_When_The_Currency_Matches()
    {
        Assert.Equal(21m, Estimate(Eur, Config(Eur, basePay: 21m)));
    }
}
