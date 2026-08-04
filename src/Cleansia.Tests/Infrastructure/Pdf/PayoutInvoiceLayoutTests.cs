using System.Globalization;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Layouts;
using Cleansia.Infra.Services.Pdf.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cleansia.Tests.Infrastructure.Pdf;

/// <summary>
/// The rendered payout invoice, asserted on the label/value pairs each block composes. The direction
/// is the point: the supplier block must be the cleaner and the customer block must be Cleansia.
/// Swap the two block builders back and <c>Supplier_Block_Is_The_Cleaner_Not_Cleansia</c> fails.
/// </summary>
public class PayoutInvoiceLayoutTests
{
    private static readonly ProbeLayout Default = new();
    private static readonly CzechProbeLayout Czech = new();

    [Fact]
    public void Supplier_Block_Is_The_Cleaner_Not_Cleansia()
    {
        var fields = Default.Supplier(Data());

        Assert.Contains(fields, f => f.Value == "Jan Novák");
        Assert.Contains(fields, f => f.Value == "12345678");
        Assert.DoesNotContain(fields, f => f.Value == "Cleansia s.r.o.");
        Assert.DoesNotContain(fields, f => f.Value == "87654321");
    }

    [Fact]
    public void Customer_Block_Is_Cleansia_With_Ic_And_Dic()
    {
        var fields = Default.Customer(Data());

        Assert.Contains(fields, f => f.Value == "Cleansia s.r.o.");
        Assert.Contains(fields, f => f.Value == "87654321");
        Assert.Contains(fields, f => f.Value == "CZ87654321");
        Assert.DoesNotContain(fields, f => f.Value == "Jan Novák");
    }

    [Fact]
    public void Czech_Layout_Labels_The_Two_Parties_Dodavatel_And_Odberatel()
    {
        Assert.Equal("Dodavatel", Czech.Labels.Supplier);
        Assert.Equal("Odběratel", Czech.Labels.Customer);
        Assert.Equal("Kontaktní údaje", Czech.Labels.ContactDetails);
        Assert.Equal("IČ", Czech.Labels.RegistrationNumber);
        Assert.Equal("DIČ", Czech.Labels.VatNumber);
        Assert.Equal("Datum vystavení", Czech.Labels.IssueDate);
        Assert.Equal("Datum splatnosti", Czech.Labels.DueDate);
        Assert.Equal("Variabilní symbol", Czech.Labels.VariableSymbol);
        Assert.Equal("Konstantní symbol", Czech.Labels.ConstantSymbol);
        Assert.Equal("Celkem k úhradě", Czech.Labels.AmountDue);
    }

    [Fact]
    public void Payment_Block_Carries_The_Suppliers_Bank_Details_And_The_Variable_Symbol()
    {
        var fields = Default.Payment(Data());

        Assert.Contains(fields, f => f.Value == "CZ3155000000005885638003");
        Assert.Contains(fields, f => f.Value == "5885638003/5500");
        Assert.Contains(fields, f => f.Value == "RZBCCZPP");
        Assert.Contains(fields, f => f.Value == "0001000001");
        Assert.DoesNotContain(fields, f => f.Value == "CZ1101000000001234567890");
    }

    [Fact]
    public void Payment_Block_Shows_The_Bank_Fields_As_Missing_Rather_Than_Hiding_Them()
    {
        var fields = Default.Payment(Data() with { Supplier = Supplier() with { Iban = null, BankAccountNumber = null, Swift = null } });

        Assert.Contains(fields, f => f.Label == Default.Labels.Iban && f.Value == null);
        Assert.Contains(fields, f => f.Label == Default.Labels.BankAccount && f.Value == null);
        Assert.Contains(fields, f => f.Label == Default.Labels.Swift && f.Value == null);
    }

    [Fact]
    public void Constant_Symbol_Is_Omitted_While_No_Value_Is_Configured()
    {
        var fields = Default.Payment(Data() with { ConstantSymbol = null });

        Assert.DoesNotContain(fields, f => f.Label == Default.Labels.ConstantSymbol);
    }

    // ── the VAT branch, both variants ────────────────────────────────

    [Fact]
    public void Supplier_Who_Is_Not_Vat_Registered_States_So_And_Shows_No_Vat_Number()
    {
        var fields = Czech.Supplier(Data());

        Assert.Contains(fields, f => f.Value == "Nejsme plátci DPH");
        Assert.DoesNotContain(fields, f => f.Label == Czech.Labels.VatNumber);
    }

    [Fact]
    public void Supplier_Who_Is_Vat_Registered_Shows_Dic_Instead_Of_The_Statement()
    {
        var data = Data() with { Supplier = Supplier() with { IsVatPayer = true, VatNumber = "CZ12345678" } };

        var fields = Czech.Supplier(data);

        Assert.Contains(fields, f => f.Label == Czech.Labels.VatNumber && f.Value == "CZ12345678");
        Assert.DoesNotContain(fields, f => f.Value == "Nejsme plátci DPH");
    }

    // ── the legal notice: whose text, and in whose language ──────────

    [Fact]
    public void A_Reviewed_Jurisdiction_Prints_Its_Own_Notice_Verbatim()
    {
        Assert.Equal(CzechReviewedNotice, Czech.LegalNoticeText(Data()));
    }

    [Fact]
    public void A_Jurisdiction_With_No_Reviewed_Notice_Falls_Back_Rather_Than_Printing_Nothing()
    {
        Assert.Equal(InvoiceLabels.UnreviewedJurisdictionNotice, Default.LegalNoticeText(Data() with { LegalDisclaimer = null }));
    }

    // The owner's rule: English functions everywhere as the notice for a jurisdiction nobody has
    // reviewed. Translating it per layout would make "we never checked this country" read exactly like
    // "a lawyer wrote this for your country", which is the one confusion the model exists to prevent.
    [Fact]
    public void The_Fallback_Is_The_Same_English_Sentence_On_Every_Layout()
    {
        var unreviewed = Data() with { LegalDisclaimer = null };

        Assert.Equal(Default.LegalNoticeText(unreviewed), Czech.LegalNoticeText(unreviewed));
        Assert.Contains("due date", Czech.LegalNoticeText(unreviewed)!);
    }

    // The heading still follows the document, so a Czech reader can tell what the box is even when its
    // contents are the English fallback.
    [Fact]
    public void The_Notice_Heading_Follows_The_Documents_Language_Even_Where_The_Text_Does_Not()
    {
        Assert.Equal("Právní upozornění", Czech.Labels.LegalNotice);
        Assert.Equal("Legal notice", Default.Labels.LegalNotice);
    }

    // Nothing can be overdue without a stated splatnost, and the fallback names one — so with no due
    // date it is an unenforceable threat rather than a disclosure.
    [Fact]
    public void The_Fallback_Is_Omitted_When_The_Invoice_States_No_Due_Date()
    {
        Assert.Null(Czech.LegalNoticeText(Data() with { LegalDisclaimer = null, DueDate = null }));
    }

    [Fact]
    public void A_Jurisdictions_Own_Notice_Is_Printed_As_Supplied_And_Not_Second_Guessed()
    {
        Assert.Equal(CzechReviewedNotice, Czech.LegalNoticeText(Data() with { DueDate = null }));
    }

    // ── the summary decomposes a gross total, it does not add to it ──

    [Fact]
    public void Summary_Of_A_Non_Vat_Payer_Ends_At_The_Stored_Total_With_No_Vat_Line()
    {
        var lines = Czech.Summary(Data());

        Assert.DoesNotContain(lines, l => l.Label == Czech.Labels.Vat);
        Assert.DoesNotContain(lines, l => l.Label == Czech.Labels.VatBase);
        Assert.Equal(("Celkem k úhradě", Czk(1000m), true), lines[^1]);
    }

    [Fact]
    public void Summary_Of_A_Vat_Payer_Shows_The_Base_And_The_Vat_Adding_Up_To_The_Unchanged_Total()
    {
        var data = Data() with
        {
            Supplier = Supplier() with { IsVatPayer = true, VatNumber = "CZ12345678" },
            VatAmount = 173.55m
        };

        var lines = Czech.Summary(data);

        Assert.Contains(lines, l => l.Label == Czech.Labels.VatBase && l.Value == Czk(826.45m));
        Assert.Contains(lines, l => l.Label == Czech.Labels.Vat && l.Value == Czk(173.55m));
        Assert.Equal(("Celkem k úhradě", Czk(1000m), true), lines[^1]);
    }

    private static string Czk(decimal amount) =>
        $"{amount.ToString("N2", CultureInfo.GetCultureInfo("cs-CZ"))} Kč";

    // ── end-to-end: the document actually renders ────────────────────

    [Fact]
    public void Factory_Selects_The_Czech_Layout_For_Cz_And_The_Default_Otherwise()
    {
        var factory = Factory();

        Assert.IsType<CzechInvoiceLayoutBuilder>(factory.GetInvoiceBuilder("CZ"));
        Assert.IsType<CzechInvoiceLayoutBuilder>(factory.GetInvoiceBuilder("cz"));
        Assert.IsType<DefaultInvoiceLayoutBuilder>(factory.GetInvoiceBuilder("PL"));
        Assert.IsType<DefaultInvoiceLayoutBuilder>(factory.GetInvoiceBuilder(null));
    }

    // The invoice path passes Country.IsoCode, which is alpha-3 ("CZE") — the fixture above passes
    // alpha-2, an input production never produces. A layout that answers only to "CZ" is therefore
    // never selected, and every Czech invoice renders in English while the test stays green.
    [Fact]
    public void Factory_Selects_The_Czech_Layout_For_The_Stored_Alpha3_Iso_Code()
    {
        var factory = Factory();

        Assert.IsType<CzechInvoiceLayoutBuilder>(factory.GetInvoiceBuilder("CZE"));
        Assert.IsType<CzechInvoiceLayoutBuilder>(factory.GetInvoiceBuilder("cze"));
        Assert.IsType<DefaultInvoiceLayoutBuilder>(factory.GetInvoiceBuilder("POL"));
    }

    [Theory]
    [InlineData("CZ")]
    [InlineData(null)]
    public void Invoice_Renders_To_A_Non_Empty_Pdf(string? countryCode)
    {
        var bytes = PdfService().GenerateInvoicePdf(Data(), null, countryCode);

        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void Invoice_Renders_For_A_Vat_Registered_Supplier_Too()
    {
        var data = Data() with { Supplier = Supplier() with { IsVatPayer = true, VatNumber = "CZ12345678" }, VatAmount = 173.55m };

        Assert.NotEmpty(PdfService().GenerateInvoicePdf(data, null, "CZ"));
    }

    // The country's VAT setting is the CUSTOMER-order regime; a cleaner who is not registered must
    // not have VAT added to their payout because the country charges it on cleaning services.
    [Fact]
    public void Country_Vat_Is_Not_Applied_To_A_Supplier_Who_Is_Not_Vat_Registered()
    {
        var context = new CountryInvoiceContext { VatRequired = true, VatRate = 0.21m };

        var bytes = PdfService().GenerateInvoicePdf(Data(), context, "CZ");

        Assert.NotEmpty(bytes);
        Assert.Equal(0m, QuestPdfService.ApplyCountryLogic(Data(), context).VatAmount);
    }

    [Fact]
    public void Country_Vat_Is_Carved_Out_Of_The_Total_For_A_Supplier_Who_Is_Vat_Registered()
    {
        var data = Data() with { Supplier = Supplier() with { IsVatPayer = true, VatNumber = "CZ12345678" } };
        var context = new CountryInvoiceContext { VatRequired = true, VatRate = 0.21m };

        var enriched = QuestPdfService.ApplyCountryLogic(data, context);

        Assert.Equal(173.55m, enriched.VatAmount);
        Assert.Equal(data.TotalAmount, enriched.TotalAmount);
    }

    // ── arrangement ──────────────────────────────────────────────────

    // Verbatim from the owner's own Czech invoice, which is the whole reason it may be printed as CZ's
    // jurisdiction notice at all. It is seeded, not hardcoded — this is only the fixture's copy.
    private const string CzechReviewedNotice =
        "Dovolujeme si Vás upozornit, že v případě nedodržení data splatnosti uvedeného na faktuře " +
        "Vám můžeme účtovat zákonný úrok z prodlení.";

    private static LayoutBuilderFactory Factory() => new(
        [new DefaultReceiptLayoutBuilder()],
        [new DefaultInvoiceLayoutBuilder(), new CzechInvoiceLayoutBuilder()]);

    private static QuestPdfService PdfService() => new(Factory(), NullLogger<QuestPdfService>.Instance);

    private static InvoiceSupplierData Supplier() => new()
    {
        Name = "Jan Novák",
        Street = "Dlouhá 12",
        ZipCode = "11000",
        City = "Praha",
        Country = "Česká republika",
        RegistrationNumber = "12345678",
        VatNumber = null,
        IsVatPayer = false,
        Email = "jan.novak@cleansia.test",
        Phone = "+420777123456",
        BankName = "Raiffeisenbank",
        BankAccountNumber = "5885638003/5500",
        Iban = "CZ3155000000005885638003",
        Swift = "RZBCCZPP"
    };

    private static InvoicePdfData Data() => new()
    {
        InvoiceNumber = "INV-202603-A1B2C",
        VariableSymbol = "0001000001",
        ConstantSymbol = "0308",
        PaymentReference = "INV-202603-A1B2C",
        GeneratedAt = new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc),
        DueDate = new DateTime(2026, 3, 16),
        Supplier = Supplier(),
        PayPeriodStart = "01.02.2026",
        PayPeriodEnd = "14.02.2026",
        SubTotal = 1000m,
        BonusAmount = 0m,
        DeductionAmount = 0m,
        VatAmount = 0m,
        TotalAmount = 1000m,
        CurrencyCode = "CZK",
        CurrencySymbol = "Kč",
        LineItems =
        [
            new InvoiceLineItem
            {
                OrderNumber = "ORD-1001",
                PerformedOn = new DateTime(2026, 2, 3),
                Quantity = 1m,
                UnitPrice = 600m,
                LineTotal = 600m
            },
            new InvoiceLineItem
            {
                OrderNumber = "ORD-1002",
                PerformedOn = new DateTime(2026, 2, 9),
                Quantity = 1m,
                UnitPrice = 400m,
                LineTotal = 400m
            }
        ],
        LegalDisclaimer = CzechReviewedNotice,
        Company = new CompanyInfoData
        {
            LegalName = "Cleansia s.r.o.",
            TradingName = "Cleansia",
            Tagline = "Professional cleaning",
            RegistrationNumber = "87654321",
            VatNumber = "CZ87654321",
            Street = "Václavské náměstí 1",
            City = "Praha",
            ZipCode = "11000",
            Address = "Václavské náměstí 1, 11000 Praha",
            Phone = "+420111222333",
            Email = "info@cleansia.cz",
            ContactInfo = "info@cleansia.cz | +420111222333",
            BankName = "Komerční banka",
            BankAccountNumber = "1234567890/0100",
            Iban = "CZ1101000000001234567890",
            Swift = "KOMBCZPP"
        }
    };

    private sealed class ProbeLayout : DefaultInvoiceLayoutBuilder
    {
        public new InvoiceLabels Labels => base.Labels;
        public IReadOnlyList<InvoiceField> Supplier(InvoicePdfData data) => SupplierFields(data);
        public IReadOnlyList<InvoiceField> Customer(InvoicePdfData data) => CustomerFields(data);
        public IReadOnlyList<InvoiceField> Payment(InvoicePdfData data) => PaymentFields(data);
        public string? LegalNoticeText(InvoicePdfData data) => base.LegalNoticeText(data);
        public IReadOnlyList<(string Label, string Value, bool IsBold)> Summary(InvoicePdfData data) => SummaryLines(data);
    }

    private sealed class CzechProbeLayout : CzechInvoiceLayoutBuilder
    {
        public new InvoiceLabels Labels => base.Labels;
        public IReadOnlyList<InvoiceField> Supplier(InvoicePdfData data) => SupplierFields(data);
        public IReadOnlyList<InvoiceField> Customer(InvoicePdfData data) => CustomerFields(data);
        public IReadOnlyList<InvoiceField> Payment(InvoicePdfData data) => PaymentFields(data);
        public string? LegalNoticeText(InvoicePdfData data) => base.LegalNoticeText(data);
        public IReadOnlyList<(string Label, string Value, bool IsBold)> Summary(InvoicePdfData data) => SummaryLines(data);
    }
}
