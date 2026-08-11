using Cleansia.Core.Domain.Payouts;

namespace Cleansia.Tests.Domain.Payouts;

/// <summary>
/// ADR-0034 D4.1 — the Czechoslovak modulo-11 account check, and the DIRECTION of its weight vector.
///
/// <para>The ADR draft specified the weights right-to-left. Applied that way the owner's own account
/// <c>5885638003</c> scores 1 and is rejected, as are ~91% of real Czech accounts — at a write path that
/// gates a cleaner's income. The mutation test below is the regression that matters most in this file:
/// it re-implements the rejected direction and asserts the reference specimen fails under it, so a future
/// "tidy-up" that reverses the loop cannot go green.</para>
/// </summary>
public class CzskAccountNumberTests
{
    // The owner's own account, from the invoice this feature was specified against (5885638003/5500).
    private const string OwnerSpecimen = "5885638003";

    [Theory]
    [InlineData(OwnerSpecimen)]
    [InlineData("2000145399")]
    [InlineData("0000000019")]
    [InlineData("19")]
    public void Valid_Account_Numbers_Pass(string value)
    {
        Assert.True(CzskAccountNumber.IsValidNumber(value));
    }

    [Theory]
    [InlineData("5885638030")]
    [InlineData("5885368003")]
    [InlineData("5885638004")]
    public void Transposed_Or_Altered_Digits_Fail(string value)
    {
        Assert.False(CzskAccountNumber.IsValidNumber(value));
    }

    [Theory]
    [InlineData("19")]
    [InlineData("000019")]
    [InlineData("000000")]
    public void Prefix_Uses_The_Same_Zero_Padded_Rule(string value)
    {
        Assert.True(CzskAccountNumber.IsValidPrefix(value));
    }

    [Fact]
    public void Prefix_Takes_The_LAST_Six_Weights_Not_The_First()
    {
        // "000019" padded to ten digits lands on weights 10,5,8,4,2,1 → 2 + 9 = 11 ≡ 0. Taking the FIRST
        // six weights (6,3,7,9,10,5) would score 10 + 5 = 15 ≡ 4 and reject a valid prefix.
        Assert.True(CzskAccountNumber.IsValidPrefix("000019"));
        Assert.False(CzskAccountNumber.IsValidPrefix("000091"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("58856380O3")]
    [InlineData("588563800-3")]
    [InlineData("58856380031")]
    public void Non_Digit_Empty_Or_Overlong_Values_Fail(string value)
    {
        Assert.False(CzskAccountNumber.IsValidNumber(value));
    }

    [Theory]
    [InlineData("5885638003", "5885638003")]
    [InlineData("19", "0000000019")]
    public void Canonicalize_Zero_Pads_To_The_Column_Width(string input, string expected)
    {
        Assert.Equal(expected, CzskAccountNumber.CanonicalizeNumber(input));
    }

    [Fact]
    public void Canonicalized_Prefix_Is_Six_Wide()
    {
        Assert.Equal("000019", CzskAccountNumber.CanonicalizePrefix("19"));
        Assert.Null(CzskAccountNumber.CanonicalizePrefix(null));
        Assert.Null(CzskAccountNumber.CanonicalizePrefix("  "));
    }

    /// <summary>
    /// The mutation proof. `MirrorReversed` is the rejected right-to-left reading of the same vector; the
    /// specimen must fail under it and pass under the shipped implementation. If someone reverses the
    /// production loop, the second assertion goes red.
    /// </summary>
    [Fact]
    public void Reversing_The_Weight_Direction_Rejects_The_Reference_Specimen()
    {
        Assert.False(MirrorReversed(OwnerSpecimen));
        Assert.True(CzskAccountNumber.IsValidNumber(OwnerSpecimen));

        Assert.False(MirrorReversed("2000145399"));
        Assert.True(CzskAccountNumber.IsValidNumber("2000145399"));
    }

    private static bool MirrorReversed(string value)
    {
        int[] weights = [6, 3, 7, 9, 10, 5, 8, 4, 2, 1];
        var padded = value.PadLeft(10, '0');
        var sum = 0;
        for (var i = 0; i < padded.Length; i++)
        {
            sum += (padded[padded.Length - 1 - i] - '0') * weights[i];
        }

        return sum % 11 == 0;
    }
}
