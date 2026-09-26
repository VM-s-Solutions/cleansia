using Cleansia.Core.Domain.Enums;
using Cleansia.Infra.Services.Pdf.Layouts;
using Cleansia.Infra.Services.Pdf.Models;

namespace Cleansia.Tests.Infrastructure.Pdf;

/// <summary>
/// A receipt states ONE VAT posture, not two.
///
/// <para>It used to state both. The company block printed a <c>DIČ</c> line whenever the company row
/// carried a VAT number, and the money block printed „Nejsme plátci DPH" whenever the sale had no VAT
/// — and the shipped configuration (a non-payer row with a VAT number filled in) produces exactly that
/// pair, so every receipt claimed a VAT registration and disclaimed one on the same page.</para>
///
/// <para>Asserted on what the layout composes for the page — the issuer's fields and the notice — so
/// a regression in either block is caught, not only in a helper the blocks might stop calling. The
/// discriminator is the ORDER's frozen posture, not the live company row, for the reason
/// <c>ReceiptService.VatApplied</c> gives.</para>
/// </summary>
public class ReceiptVatPostureTests
{
    private const string VatNumber = "CZ12345678";

    private static readonly ProbeLayout Layout = new();

    [Theory]
    [InlineData("cs", "DIČ")]
    [InlineData("en", "VAT No.")]
    public void A_Sale_That_Charged_No_Vat_Prints_The_Notice_And_No_Vat_Number(string languageCode, string vatLabel)
    {
        var data = Receipt(vatApplied: false) with { LanguageCode = languageCode };

        Assert.DoesNotContain(Layout.Company(data), field => field.Label == vatLabel);
        Assert.DoesNotContain(Layout.Company(data), field => field.Value == VatNumber);
        Assert.Equal(ReceiptLabels.For(languageCode).NotVatRegistered, Layout.Notice(data));
    }

    [Fact]
    public void A_Sale_That_Charged_Vat_Prints_The_Number_And_No_Notice()
    {
        var data = Receipt(vatApplied: true) with { LanguageCode = "cs" };

        Assert.Contains(("DIČ", (string?)VatNumber), Layout.Company(data));
        Assert.Null(Layout.Notice(data));
    }

    /// <summary>
    /// The shipped seed's shape — a non-payer row carrying a VAT number — is the case that matters, and
    /// the notice is then the only VAT statement on the page, in the document's language.
    /// </summary>
    [Fact]
    public void The_Notice_Is_Written_In_The_Documents_Language()
    {
        Assert.Equal("Nejsme plátci DPH", Layout.Notice(Receipt(vatApplied: false) with { LanguageCode = "cs" }));
        Assert.Equal("We are not registered for VAT", Layout.Notice(Receipt(vatApplied: false)));
    }

    /// <summary>
    /// A VAT sale whose issuer row holds no number prints neither — the one case where the pair is not
    /// exactly one. Pinned so the gap stays visible rather than papered over with a placeholder.
    /// </summary>
    [Fact]
    public void A_Vat_Sale_Whose_Issuer_Has_No_Number_Prints_Neither()
    {
        var data = Receipt(vatApplied: true) with { Company = CompanyRow(vatNumber: null) };

        Assert.DoesNotContain(Layout.Company(data), field => field.Label == "VAT No.");
        Assert.Null(Layout.Notice(data));
    }

    private static ReceiptPdfData Receipt(bool vatApplied) =>
        new()
        {
            ReceiptNumber = "2026-000001",
            OrderNumber = "ORD-1",
            IssuedDate = "22.09.2026",
            CustomerName = "Jan Novák",
            Services = [],
            Packages = [],
            Total = 1000m,
            Currency = "Kč",
            PaymentStatus = PaymentStatus.Paid,
            PaymentType = PaymentType.Cash,
            IsVatPayer = vatApplied,
            NetAmount = vatApplied ? 826.45m : null,
            VatAmount = vatApplied ? 173.55m : null,
            VatRate = vatApplied ? 0.21m : null,
            Company = CompanyRow(VatNumber),
        };

    private static CompanyInfoData CompanyRow(string? vatNumber) =>
        new()
        {
            LegalName = "Cleansia s.r.o.",
            TradingName = "Cleansia",
            RegistrationNumber = "12345678",
            VatNumber = vatNumber,
            Street = "Hlavní 1",
            City = "Praha",
            ZipCode = "11000",
            Address = "Hlavní 1, Praha, 11000",
            ContactInfo = "info@cleansia.cz",
        };

    private sealed class ProbeLayout : DefaultReceiptLayoutBuilder
    {
        public IReadOnlyList<(string Label, string? Value)> Company(ReceiptPdfData data) => CompanyFields(data);

        public string? Notice(ReceiptPdfData data) => VatNotice(data);
    }
}
