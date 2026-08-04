using Cleansia.Core.Domain.Payouts;

namespace Cleansia.Tests.Domain.Payouts;

/// <summary>
/// ADR-0034 D5.2 — the IBAN is DERIVED from the local parts (ISO 7064 mod-97-10 over an ISO 13616
/// structure), not collected. The owner's specimen composes to <c>CZ3155000000005885638003</c>, which is
/// the value the payout-invoice fixtures pin.
/// </summary>
public class IbanCalculatorTests
{
    [Fact]
    public void Owner_Specimen_Composes_To_The_Fixture_Iban()
    {
        var iban = IbanCalculator.ComposeCzsk("CZ", "5500", null, "5885638003");

        Assert.Equal("CZ3155000000005885638003", iban);
        Assert.True(IbanCalculator.IsValid(iban));
    }

    [Fact]
    public void Leading_Zeros_Are_Canonicalization_Not_Identity()
    {
        Assert.Equal(
            IbanCalculator.ComposeCzsk("CZ", "5500", null, "5885638003"),
            IbanCalculator.ComposeCzsk("CZ", "5500", "0", "0005885638003".TrimStart('0')));

        Assert.Equal(
            IbanCalculator.ComposeCzsk("CZ", "0800", "19", "2000145399"),
            IbanCalculator.ComposeCzsk("CZ", "0800", "000019", "0002000145399".TrimStart('0')));
    }

    [Theory]
    [InlineData("CZ3155000000005885638003")]
    [InlineData("CZ6508000000192000145399")]
    [InlineData("CZ 31 5500 0000 0058 8563 8003")]
    [InlineData("DE89370400440532013000")]
    [InlineData("SK3112000000198742637541")]
    public void Valid_Ibans_Pass_Mod97(string value)
    {
        Assert.True(IbanCalculator.IsValid(value));
    }

    [Theory]
    // The fixture value this sprint corrected: it fails its own checksum (remainder 35, not 1).
    [InlineData("CZ6555000000005885638003")]
    [InlineData("totally not an iban!!")]
    [InlineData("CZ3255000000005885638003")]
    [InlineData("DE89370400440532013001")]
    [InlineData("")]
    [InlineData("CZ31")]
    public void Invalid_Ibans_Fail(string value)
    {
        Assert.False(IbanCalculator.IsValid(value));
    }

    [Fact]
    public void A_Right_Length_Wrong_Checksum_Iban_Is_Rejected()
    {
        const string wrongCheckDigits = "CZ9955000000005885638003";

        Assert.Equal(24, wrongCheckDigits.Length);
        Assert.False(IbanCalculator.IsValid(wrongCheckDigits));
    }

    [Theory]
    [InlineData("CZ3155000000005885638003", "CZ")]
    [InlineData("de89370400440532013000", "DE")]
    [InlineData("1234", null)]
    public void Country_Prefix_Is_Read_From_The_Value_Itself(string value, string? expected)
    {
        Assert.Equal(expected, IbanCalculator.CountryPrefixOf(value));
    }

    [Fact]
    public void Registry_Length_Is_Enforced_For_Known_Countries()
    {
        Assert.Equal(24, IbanCalculator.RegistryLengthFor("CZ"));
        Assert.Equal(24, IbanCalculator.RegistryLengthFor("SK"));
        Assert.Equal(22, IbanCalculator.RegistryLengthFor("DE"));
        Assert.Null(IbanCalculator.RegistryLengthFor("ZZ"));
    }

    [Fact]
    public void Normalize_Strips_Separators_And_Uppercases()
    {
        Assert.Equal("CZ3155000000005885638003", IbanCalculator.Normalize(" cz31 5500 0000 0058 8563 8003 "));
    }
}
