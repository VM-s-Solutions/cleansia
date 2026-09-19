using System.Globalization;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Tests.Common;

/// <summary>
/// Money as a notification states it, shared by the apology credit and the admin events: the number
/// in invariant culture with no trailing zeros, a space, then the currency's own symbol; the code
/// stands in for a currency with none.
/// </summary>
public sealed class MoneyTextTests
{
    private static readonly Currency Czk = Currency.Create("CZK", "Kč", "Czech koruna");

    [Theory]
    [InlineData("250", "250 Kč")]
    [InlineData("250.00", "250 Kč")]
    [InlineData("10", "10 Kč")]
    [InlineData("9.5", "9.5 Kč")]
    [InlineData("9.50", "9.5 Kč")]
    [InlineData("1250.75", "1250.75 Kč")]
    public void The_Number_Is_Invariant_With_No_Trailing_Zeros_Then_The_Symbol(string amount, string expected)
    {
        Assert.Equal(expected, MoneyText.Format(decimal.Parse(amount, CultureInfo.InvariantCulture), Czk));
    }

    [Fact]
    public void A_Symbolless_Currency_Falls_Back_To_Its_Code()
    {
        var bare = Currency.Create("XXX", "", "Bare");

        Assert.Equal("250 XXX", MoneyText.Format(250m, bare));
    }
}
