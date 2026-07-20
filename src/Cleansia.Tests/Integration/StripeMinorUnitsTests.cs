using System.Globalization;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Infra.Clients.Stripe;

namespace Cleansia.Tests.Integration;

/// <summary>
/// The Stripe adapter's decimal→minor-units seam. The old <c>(long)(amount * 100)</c> cast
/// truncates toward zero, so any amount still carrying fractional cents lost one against the
/// ledger's numeric(18,2) away-from-zero rounding — the 1-cent Refunded/PartiallyRefunded drift
/// class. These pin that <see cref="StripeClient.ToMinorUnits"/> rounds the same way the ledger
/// does, so a persisted amount and the amount sent to Stripe are always cent-identical.
/// </summary>
public class StripeMinorUnitsTests
{
    [Theory]
    [InlineData("10.10", 1010L)]
    [InlineData("1234.56", 123456L)]
    [InlineData("0.01", 1L)]
    [InlineData("2499.00", 249900L)]
    public void ExactTwoDecimalAmount_MapsToExactCents(string amount, long expectedCents)
    {
        Assert.Equal(expectedCents, StripeClient.ToMinorUnits(decimal.Parse(amount, CultureInfo.InvariantCulture)));
    }

    [Theory]
    [InlineData("33.335", 3334L)] // bare (long) cast truncates to 3333
    [InlineData("50.015", 5002L)] // bare (long) cast truncates to 5001
    [InlineData("0.005", 1L)]     // bare (long) cast truncates to 0
    public void FractionalCentAmount_RoundsAwayFromZero_InsteadOfTruncating(string amount, long expectedCents)
    {
        Assert.Equal(expectedCents, StripeClient.ToMinorUnits(decimal.Parse(amount, CultureInfo.InvariantCulture)));
    }

    // The full chain for a fractional-fee cancellation: 100.03 at the 50% last-minute tier
    // yields a raw 50.015. CancelOrder rounds it to the ledger's 50.02 (2 dp away-from-zero) and the
    // Stripe seam must forward exactly those 5002 cents — the same count the seam would produce from
    // the raw amount, so ledger and Stripe can never disagree by a cent.
    [Fact]
    public void FractionalFeeRefund_RoundTripsToTheExactCentCount()
    {
        var raw = 100.03m * (1m - BookingPolicy.LastMinuteCancellationFeeRate);
        Assert.Equal(50.015m, raw);

        var ledgerAmount = Math.Round(raw, 2, MidpointRounding.AwayFromZero);
        Assert.Equal(50.02m, ledgerAmount);

        Assert.Equal(5002L, StripeClient.ToMinorUnits(ledgerAmount));
        Assert.Equal(StripeClient.ToMinorUnits(raw), StripeClient.ToMinorUnits(ledgerAmount));
    }

    // Mechanical tripwire (the SendPushNotificationSeamTripwireTests idiom): the helper's semantics
    // are pinned above, but nothing else stops a merge-conflict resolution from quietly reverting a
    // call site to the bare truncating cast — the suite would stay green while the 1-cent drift
    // reopens on that one path. So pin the source: no `(long)(... * 100)` cast may reappear in
    // StripeClient.cs, and the helper must still be called at every amount site.
    [Fact]
    public void StripeClient_HasNoBareTruncatingCast_AndRoutesEveryAmountThroughTheSeam()
    {
        var source = File.ReadAllText(StripeClientSourcePath());

        Assert.DoesNotContain("(long)(", source, StringComparison.Ordinal);

        // 5 amount sites: checkout unit amount, two refund paths, PaymentIntent amount,
        // PaymentIntent idempotency-key cents. Guard against the pin hollowing out.
        var seamCalls = CountOccurrences(source, "ToMinorUnits(");
        Assert.True(seamCalls >= 6, // 5 call sites + the method's own declaration
            $"Expected the ToMinorUnits seam at all 5 Stripe amount sites (+ its declaration), found {seamCalls} " +
            "occurrences — a call site was removed or bypassed. Route every decimal→cents conversion through it.");
    }

    private static string StripeClientSourcePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Cleansia.Infra.Clients", "Stripe", "StripeClient.cs");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        Assert.Fail("Could not locate StripeClient.cs from the test base directory.");
        return string.Empty;
    }

    private static int CountOccurrences(string text, string token)
    {
        var count = 0;
        for (var i = text.IndexOf(token, StringComparison.Ordinal); i >= 0;
             i = text.IndexOf(token, i + token.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
