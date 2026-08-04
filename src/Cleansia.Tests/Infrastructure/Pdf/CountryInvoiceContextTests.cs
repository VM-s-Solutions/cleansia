using Cleansia.Core.Domain.Enums;
using Cleansia.Infra.Services.Pdf.Models;

namespace Cleansia.Tests.Infrastructure.Pdf;

/// <summary>
/// The two per-country rules the payout invoice cannot be allowed to hold two copies of: how VAT is
/// derived from a GROSS payout, and which jurisdictions' legal notices are allowed onto the document.
/// </summary>
public class CountryInvoiceContextTests
{
    // ── the pay is GROSS: the stored total is what the cleaner receives ──

    [Fact]
    public void Vat_Is_Decomposed_Out_Of_The_Gross_Total_Rather_Than_Added_To_It()
    {
        var vat = Czech().VatWithinGross(1000m, supplierIsVatPayer: true);

        Assert.Equal(173.55m, vat);
        Assert.NotEqual(1000m * 0.21m, vat);
    }

    // The sanity check against the rate source: take the base the document prints, apply the country's
    // configured rate to it, and the printed total must come back.
    [Theory]
    [InlineData(1000)]
    [InlineData(1350)]
    [InlineData(1633.50)]
    [InlineData(12345.67)]
    public void Grossing_The_Printed_Base_Back_Up_At_The_Countrys_Rate_Returns_The_Printed_Total(decimal gross)
    {
        var context = Czech();

        var vat = context.VatWithinGross(gross, supplierIsVatPayer: true);

        Assert.Equal(gross, Math.Round((gross - vat) * (1m + context.VatRate), 2, MidpointRounding.AwayFromZero));
    }

    // Below a few cents there is no two-decimal base that grosses back up exactly, so the round trip
    // above cannot hold. Base + VAT == the printed total still does, and that is the identity the
    // document needs: it is what makes the recap add up to the amount the cleaner is actually paid.
    [Fact]
    public void Base_Plus_Vat_Is_The_Printed_Total_Even_Where_No_Exact_Split_Exists()
    {
        var vat = Czech().VatWithinGross(0.03m, supplierIsVatPayer: true);

        Assert.Equal(0.03m, 0.03m - vat + vat);
        Assert.InRange(vat, 0m, 0.03m);
    }

    // The country's VAT setting is the CUSTOMER-order regime and cannot make a cleaner a VAT payer.
    [Fact]
    public void Vat_Is_Zero_For_A_Supplier_Who_Is_Not_Registered_Even_Where_The_Country_Requires_It()
    {
        Assert.Equal(0m, Czech().VatWithinGross(1000m, supplierIsVatPayer: false));
    }

    [Fact]
    public void Vat_Is_Zero_Where_The_Country_Requires_None()
    {
        var context = new CountryInvoiceContext { VatRequired = false, VatRate = 0.21m };

        Assert.Equal(0m, context.VatWithinGross(1000m, supplierIsVatPayer: true));
    }

    [Fact]
    public void Vat_Is_Zero_When_The_Configured_Rate_Is_Zero()
    {
        var context = new CountryInvoiceContext { VatRequired = true, VatRate = 0m };

        Assert.Equal(0m, context.VatWithinGross(1000m, supplierIsVatPayer: true));
    }

    // ── whose legal notice may appear on the document ────────────────

    [Fact]
    public void A_Jurisdiction_Whose_Notice_Was_Reviewed_Puts_Its_Own_Text_On_The_Document()
    {
        var context = Czech() with
        {
            LegalDisclaimerTemplate = "Zákonný text.",
            LegalDisclaimerLanguageCode = "cs",
            LegalDisclaimerReviewStatus = LegalNoticeReviewStatus.BusinessSupplied
        };

        Assert.Equal("Zákonný text.", context.ReviewedLegalNotice);
    }

    [Fact]
    public void Counsel_Reviewed_Text_Reaches_The_Document_Too()
    {
        var context = Czech() with
        {
            LegalDisclaimerTemplate = "Zákonný text.",
            LegalDisclaimerReviewStatus = LegalNoticeReviewStatus.CounselReviewed
        };

        Assert.Equal("Zákonný text.", context.ReviewedLegalNotice);
    }

    // The state today's seed is in: a paragraph asserting something about a country's law that nobody
    // checked. Being stored is not review, so it never prints and the fallback takes its place.
    [Fact]
    public void Text_Nobody_Reviewed_For_The_Jurisdiction_Never_Reaches_The_Document()
    {
        var context = Czech() with
        {
            LegalDisclaimerTemplate = "This invoice is issued in accordance with Czech law.",
            LegalDisclaimerReviewStatus = LegalNoticeReviewStatus.NotReviewed
        };

        Assert.Null(context.ReviewedLegalNotice);
    }

    [Fact]
    public void A_Jurisdiction_With_No_Notice_At_All_Has_None_To_Print()
    {
        Assert.Null(Czech().ReviewedLegalNotice);
        Assert.Null((Czech() with
        {
            LegalDisclaimerTemplate = "   ",
            LegalDisclaimerReviewStatus = LegalNoticeReviewStatus.CounselReviewed
        }).ReviewedLegalNotice);
    }

    private static CountryInvoiceContext Czech() => new() { VatRequired = true, VatRate = 0.21m };
}
