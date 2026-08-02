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

        Assert.Contains(fields, f => f.Value == "CZ6555000000005885638003");
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

    // ── end-to-end: the document actually renders ────────────────────

    [Fact]
    public void Factory_Selects_The_Czech_Layout_For_Cz_And_The_Default_Otherwise()
    {
        var factory = new LayoutBuilderFactory(
            [new DefaultReceiptLayoutBuilder()],
            [new DefaultInvoiceLayoutBuilder(), new CzechInvoiceLayoutBuilder()]);

        Assert.IsType<CzechInvoiceLayoutBuilder>(factory.GetInvoiceBuilder("CZ"));
        Assert.IsType<CzechInvoiceLayoutBuilder>(factory.GetInvoiceBuilder("cz"));
        Assert.IsType<DefaultInvoiceLayoutBuilder>(factory.GetInvoiceBuilder("PL"));
        Assert.IsType<DefaultInvoiceLayoutBuilder>(factory.GetInvoiceBuilder(null));
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
        var data = Data() with { Supplier = Supplier() with { IsVatPayer = true, VatNumber = "CZ12345678" }, VatAmount = 210m };

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
    public void Country_Vat_Is_Applied_To_A_Supplier_Who_Is_Vat_Registered()
    {
        var data = Data() with { Supplier = Supplier() with { IsVatPayer = true, VatNumber = "CZ12345678" } };
        var context = new CountryInvoiceContext { VatRequired = true, VatRate = 0.21m };

        var enriched = QuestPdfService.ApplyCountryLogic(data, context);

        Assert.Equal(210m, enriched.VatAmount);
        Assert.Equal(1210m, enriched.TotalAmount);
    }

    // ── arrangement ──────────────────────────────────────────────────

    private static QuestPdfService PdfService() => new(
        new LayoutBuilderFactory(
            [new DefaultReceiptLayoutBuilder()],
            [new DefaultInvoiceLayoutBuilder(), new CzechInvoiceLayoutBuilder()]),
        NullLogger<QuestPdfService>.Instance);

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
        Iban = "CZ6555000000005885638003",
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
        LegalDisclaimer = "V případě prodlení s úhradou bude účtován zákonný úrok z prodlení.",
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
    }

    private sealed class CzechProbeLayout : CzechInvoiceLayoutBuilder
    {
        public new InvoiceLabels Labels => base.Labels;
        public IReadOnlyList<InvoiceField> Supplier(InvoicePdfData data) => SupplierFields(data);
        public IReadOnlyList<InvoiceField> Customer(InvoicePdfData data) => CustomerFields(data);
        public IReadOnlyList<InvoiceField> Payment(InvoicePdfData data) => PaymentFields(data);
    }
}
