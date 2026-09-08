using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Configuration;

namespace Cleansia.Tests.Services;

/// <summary>
/// <see cref="VatCalculator"/> — the gross-inclusive VAT split that every receipt, every accounting
/// export and every credit note is derived from.
///
/// <para><b>This class had no tests at all until 2026-09-08, and that is why it was wrong.</b> Every
/// one of the nine references to <c>IVatCalculator</c> in the suite was a <c>Mock</c>, so the real
/// implementation was never executed by anything. It divided by <c>(100 + rate)</c> — the PERCENT form
/// — while <c>CountryConfiguration.StandardVatRate</c> is <c>numeric(5,4)</c> and holds a FRACTION.
/// On a 2 000 Kč order it returned 4.19 Kč of VAT instead of 347.11: a 98.8% under-declaration on
/// every sale.</para>
///
/// <para><b>Why nobody noticed.</b> The seeded company is flagged not a VAT payer, so the guard returns
/// before the arithmetic. The bug was dormant, waiting on a single flag flip.</para>
///
/// <para><b>Every expected value below is hand-derived, never the production expression re-run.</b>
/// A test that recomputes the formula it is testing would have passed against the broken version —
/// which is exactly what the partial-refund suite did, by building its fixtures with a rate of
/// <c>21m</c>, a value the column physically cannot hold.</para>
/// </summary>
public class VatCalculatorTests
{
    private const string CountryId = "cze";

    private static readonly VatCalculator Sut = new();

    private static CompanyInfo VatPayer(bool isVatPayer = true) =>
        CompanyInfo
            .Create(
                legalName: "Cleansia s.r.o.",
                tradingName: "Cleansia",
                registrationNumber: "12345678",
                street: "Street 1",
                city: "Prague",
                zipCode: "11000",
                countryId: CountryId)
            .SetVatPayerStatus(isVatPayer);

    /// <summary>
    /// The rate is a FRACTION. <c>StandardVatRate</c> is <c>numeric(5,4)</c> — maximum 9.9999 — so a
    /// percent literal cannot be stored, and Postgres rejects the insert. The seed agrees: CZE 0.21,
    /// SVK 0.20, POL 0.23, DEU 0.19.
    /// </summary>
    private static CountryConfiguration Config(decimal standardVatRateAsFraction) =>
        CountryConfiguration.Create(CountryId, "CZK", "cs", standardVatRateAsFraction);

    /// <summary>
    /// The headline case, hand-derived: 2 000 × 0.21 / 1.21 = 347.107438… → 347.11 away from zero.
    /// The broken percent form gave 2 000 × 0.21 / 100.21 = 4.1911… → 4.19.
    /// </summary>
    [Fact]
    public void TwoThousandAtCzechRate_SplitsTo347_11()
    {
        var result = Sut.Calculate(2000m, VatPayer(), Config(0.21m));

        Assert.True(result.IsApplicable);
        Assert.Equal(347.11m, result.VatAmount);
        Assert.Equal(1652.89m, result.NetAmount);
        Assert.Equal(0.21m, result.AppliedRate);
    }

    /// <summary>
    /// The example in the method's own docstring, which the implementation did not satisfy:
    /// 500 × 0.21 / 1.21 = 86.7768… → 86.78, net 413.22.
    /// </summary>
    [Fact]
    public void TheDocstringExample_Holds()
    {
        var result = Sut.Calculate(500m, VatPayer(), Config(0.21m));

        Assert.Equal(86.78m, result.VatAmount);
        Assert.Equal(413.22m, result.NetAmount);
    }

    /// <summary>
    /// Each seeded rate, hand-derived on a 1 210 gross so the Czech case lands on a whole number and a
    /// wrong divisor is obvious by eye.
    /// </summary>
    [Theory]
    [InlineData(0.21, 210.00, 1000.00)]   // 1210 × 0.21 / 1.21 = 210 exactly
    [InlineData(0.20, 201.67, 1008.33)]   // 1210 × 0.20 / 1.20 = 201.666… → 201.67
    [InlineData(0.23, 226.26, 983.74)]    // 1210 × 0.23 / 1.23 = 226.260… → 226.26
    [InlineData(0.19, 193.19, 1016.81)]   // 1210 × 0.19 / 1.19 = 193.193… → 193.19
    public void EachSeededRate_SplitsAsHandDerived(decimal rate, decimal expectedVat, decimal expectedNet)
    {
        var result = Sut.Calculate(1210m, VatPayer(), Config(rate));

        Assert.Equal(expectedVat, result.VatAmount);
        Assert.Equal(expectedNet, result.NetAmount);
    }

    /// <summary>
    /// The invariant a tax document lives or dies by: the split must reconstitute the gross exactly.
    /// Net is derived by subtraction rather than a second rounding, so this holds for every input —
    /// which is worth pinning, because computing net independently would break it on half-cent cases.
    /// </summary>
    [Theory]
    [InlineData(0.01)]
    [InlineData(9.99)]
    [InlineData(100.05)]
    [InlineData(1234.56)]
    [InlineData(999999.99)]
    public void NetPlusVat_ExactlyReconstitutesTheGross(decimal gross)
    {
        var result = Sut.Calculate(gross, VatPayer(), Config(0.21m));

        Assert.Equal(gross, result.NetAmount + result.VatAmount);
    }

    /// <summary>
    /// Half-cent rounds AWAY FROM ZERO, not banker's. 6.90 × 0.20 / 1.20 = 1.15 exactly, so it is not
    /// the case; 8.55 × 0.21 / 1.21 = 1.4838… is. Picked so a HalfEven implementation disagrees.
    /// </summary>
    [Fact]
    public void HalfCent_RoundsAwayFromZero()
    {
        // 0.21 / 1.21 of 20.15 = 3.49752… → 3.50 (a truncating implementation gives 3.49).
        var result = Sut.Calculate(20.15m, VatPayer(), Config(0.21m));

        Assert.Equal(3.50m, result.VatAmount);
        Assert.Equal(16.65m, result.NetAmount);
    }

    /// <summary>
    /// A rate of 21 is the percent form and cannot reach this method from the database — but if a
    /// caller ever hands one over, the result must be visibly absurd rather than plausibly wrong.
    /// 1 000 × 21 / 22 = 954.55 of "VAT" on a 1 000 order, i.e. 95%. Pinned so that a future reader
    /// who reintroduces the percent convention sees a screaming number rather than a quiet one.
    /// </summary>
    [Fact]
    public void APercentShapedRate_ProducesAnAbsurdResult_NotAPlausibleOne()
    {
        var result = Sut.Calculate(1000m, VatPayer(), Config(21m));

        Assert.Equal(954.55m, result.VatAmount);
        Assert.True(result.VatAmount > result.NetAmount * 20m,
            "a percent-shaped rate must not produce a result that could pass for correct");
    }

    [Fact]
    public void NotAVatPayer_YieldsNotApplicable_AndTheWholeGrossAsNet()
    {
        var result = Sut.Calculate(1210m, VatPayer(isVatPayer: false), Config(0.21m));

        Assert.False(result.IsApplicable);
        Assert.Equal(0m, result.VatAmount);
        Assert.Equal(1210m, result.NetAmount);
        Assert.Null(result.AppliedRate);
    }

    /// <summary>
    /// Pins the CURRENT behaviour, which is fail-open: no country configuration means no VAT, silently.
    /// That is safe while Czechia is the only market and its configuration is seeded, and it is a
    /// silent under-declaration the moment it is not. Recorded as a finding rather than changed here —
    /// making it throw is a behaviour change that belongs with the multi-jurisdiction work.
    /// </summary>
    [Fact]
    public void NullCountryConfiguration_CurrentlyYieldsZeroVat_Silently()
    {
        var result = Sut.Calculate(1210m, VatPayer(), countryConfig: null);

        Assert.False(result.IsApplicable);
        Assert.Equal(0m, result.VatAmount);
        Assert.Equal(1210m, result.NetAmount);
    }

    [Fact]
    public void ZeroRate_YieldsZeroVat_ButStaysApplicable()
    {
        var result = Sut.Calculate(1210m, VatPayer(), Config(0m));

        Assert.True(result.IsApplicable);
        Assert.Equal(0m, result.VatAmount);
        Assert.Equal(1210m, result.NetAmount);
        Assert.Equal(0m, result.AppliedRate);
    }
}
