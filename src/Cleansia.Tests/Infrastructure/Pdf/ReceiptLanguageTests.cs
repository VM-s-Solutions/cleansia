using System.Globalization;
using System.Reflection;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Infra.Services.Pdf.Layouts;
using Cleansia.Infra.Services.Pdf.Models;

namespace Cleansia.Tests.Infrastructure.Pdf;

/// <summary>
/// The receipt is written in the customer's language, the way their e-mails are.
///
/// <para>Every label on it was a hardcoded English literal and the payment method was the enum's own
/// name, so a Czech customer who booked in Czech, paid in Czech koruna and was e-mailed in Czech
/// received a document reading "Payment Status / Pending / Payment Method / Cash".</para>
///
/// <para>The seam is the e-mails' own: an in-code catalogue per locale, resolved to English for
/// anything outside the five the platform renders. It is NOT the invoice's, which keys on the
/// jurisdiction — <see cref="ReceiptLabels"/> says why the two differ.</para>
/// </summary>
public class ReceiptLanguageTests
{
    private static readonly ProbeLayout Layout = new();

    /// <summary>
    /// What the payment block prints, asserted where the layout composes it — a word chosen from the
    /// document's language, where the page used to print the enum's own name.
    /// </summary>
    [Fact]
    public void A_Czech_Receipt_States_Its_Payment_In_Czech()
    {
        var lines = Layout.Payment(CzechReceipt());

        Assert.Contains(("Stav platby", "Čeká na úhradu"), lines);
        Assert.Contains(("Způsob platby", "Hotově"), lines);
    }

    [Fact]
    public void A_Card_Sale_Is_Named_In_The_Documents_Language()
    {
        var lines = Layout.Payment(CzechReceipt() with { PaymentType = PaymentType.Card, PaymentStatus = PaymentStatus.Paid });

        Assert.Contains(("Způsob platby", "Kartou"), lines);
        Assert.Contains(("Stav platby", "Zaplaceno"), lines);
    }

    /// <summary>A language nobody has reviewed a receipt in gets the English page, every word of it.</summary>
    [Fact]
    public void An_Unknown_Language_Falls_Back_To_English_On_The_Page()
    {
        var lines = Layout.Payment(CzechReceipt() with { LanguageCode = "de" });

        Assert.Contains(("Payment method", "Cash"), lines);
        Assert.Contains(("Payment status", "Awaiting payment"), lines);
    }

    [Theory]
    [InlineData("cs", 150, "2 h 30 min")]
    [InlineData("uk", 150, "2 год 30 хв")]
    [InlineData("en", 45, "45 min")]
    public void The_Estimated_Duration_Is_Written_In_The_Documents_Units(string languageCode, int minutes, string expected)
    {
        Assert.Equal(expected, Layout.Duration(minutes, languageCode));
    }

    /// <summary>
    /// The enum's own name must never reach the page. Asserted as an absence across every locale and
    /// every member, because the failure mode is one member falling back rather than the lookup
    /// breaking.
    /// </summary>
    [Fact]
    public void No_Locale_Prints_An_Enum_Name_For_A_Payment_Method_Or_Status()
    {
        foreach (var languageCode in ReceiptLabels.SupportedLanguages.Where(c => c != "en"))
        {
            var labels = ReceiptLabels.For(languageCode);

            foreach (var status in Enum.GetValues<PaymentStatus>())
            {
                Assert.NotEqual(status.ToString(), labels.PaymentStatuses[status]);
            }

            foreach (var type in Enum.GetValues<PaymentType>())
            {
                Assert.NotEqual(type.ToString(), labels.PaymentTypes[type]);
            }
        }
    }

    /// <summary>
    /// Every locale carries a word for every member. A missing key would throw at render time — on the
    /// document, in production, for one customer — so the gap is found here instead.
    /// </summary>
    [Fact]
    public void Every_Locale_Has_A_Word_For_Every_Payment_Status_And_Method()
    {
        foreach (var languageCode in ReceiptLabels.SupportedLanguages)
        {
            var labels = ReceiptLabels.For(languageCode);

            foreach (var status in Enum.GetValues<PaymentStatus>())
            {
                Assert.False(string.IsNullOrWhiteSpace(labels.PaymentStatuses.GetValueOrDefault(status)));
            }

            foreach (var type in Enum.GetValues<PaymentType>())
            {
                Assert.False(string.IsNullOrWhiteSpace(labels.PaymentTypes.GetValueOrDefault(type)));
            }
        }
    }

    /// <summary>
    /// Every label differs from its English original in every non-English locale, save the handful that
    /// are the same word in that language (IBAN, E-mail). A half-translated set is the shape this
    /// catches: it reads as a rendering bug rather than a missing translation, because the page still
    /// renders.
    /// </summary>
    [Fact]
    public void Every_Locale_Translates_Every_Label()
    {
        var english = ReceiptLabels.English;
        // The same word in every locale, or — the two units — in every Latin-script one.
        var untranslatable = new[]
        {
            nameof(ReceiptLabels.Iban), nameof(ReceiptLabels.Email),
            nameof(ReceiptLabels.HoursUnit), nameof(ReceiptLabels.MinutesUnit),
        };

        foreach (var languageCode in ReceiptLabels.SupportedLanguages.Where(c => c != "en"))
        {
            var labels = ReceiptLabels.For(languageCode);

            foreach (var property in TextLabels().Where(p => !untranslatable.Contains(p.Name)))
            {
                var translated = (string)property.GetValue(labels)!;

                Assert.False(string.IsNullOrWhiteSpace(translated));
                Assert.NotEqual((string)property.GetValue(english)!, translated);
            }
        }
    }

    /// <summary>
    /// The five are the locales the e-mails already render, and the fallback is theirs too: a language
    /// nobody has reviewed a receipt in gets English rather than a half-written document.
    /// </summary>
    [Fact]
    public void The_Locales_Are_The_Ones_The_Emails_Render_And_Anything_Else_Is_English()
    {
        Assert.Equal(
            EmailLocale.Supported.OrderBy(c => c, StringComparer.Ordinal),
            ReceiptLabels.SupportedLanguages.OrderBy(c => c, StringComparer.Ordinal));

        Assert.Same(ReceiptLabels.English, ReceiptLabels.For("de"));
        Assert.Same(ReceiptLabels.English, ReceiptLabels.For(null));
    }

    /// <summary>
    /// The document's language reaches the money block, not just the chrome — the labels a customer
    /// holds against a bank statement are the ones most worth reading.
    /// </summary>
    [Fact]
    public void The_Money_Block_Is_Written_In_The_Documents_Language()
    {
        var lines = Layout.Summary(CzechReceipt());

        Assert.Contains(lines, l => l.Label == "Celkem");
        Assert.Contains(lines, l => l.Label == "Uhrazeno kreditem");
        Assert.Contains(lines, l => l.Label == "Uhrazeno kartou");
        Assert.Contains(lines, l => l.Label.StartsWith("DPH", StringComparison.Ordinal));
    }

    /// <summary>
    /// Amounts are written the way the document's language writes numbers, and never the way the host
    /// happens to: a Czech reader takes the comma in "Kč2,000.00" for the decimal point and reads two
    /// crowns. Run under two host cultures that disagree with each other and with Czech.
    /// </summary>
    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    public void Amounts_Are_Written_In_The_Documents_Number_Format_Whatever_The_Hosts(string hostCulture)
    {
        var host = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(hostCulture);
        try
        {
            var receipt = CzechReceipt() with { Total = 2000m, CreditApplied = 500m, AmountDueOnCard = 1500m };
            var czech = Layout.Summary(receipt);
            var english = Layout.Summary(receipt with { LanguageCode = "en" });

            Assert.Contains(czech, l => l is { Label: "Celkem", Value: "2\u00A0000,00 Kč" });
            Assert.Contains(czech, l => l is { Label: "Uhrazeno kreditem", Value: "-500,00 Kč" });
            Assert.Contains(english, l => l is { Label: "Total", Value: "Kč2,000.00" });
            Assert.Contains(english, l => l is { Label: "Paid with credit", Value: "-Kč500.00" });
        }
        finally
        {
            CultureInfo.CurrentCulture = host;
        }
    }

    [Fact]
    public void The_Item_Lines_Are_Written_In_The_Documents_Language()
    {
        var lines = Layout.Items(CzechReceipt());

        Assert.Contains(lines, l => l.Description == "Expresní příplatek");
        Assert.Contains(lines, l => l.Description == "Sleva na kód");
        Assert.Contains(lines, l => l.Description == "Stěhovací balíček (balíček)");
    }

    private static ReceiptPdfData CzechReceipt() =>
        new()
        {
            ReceiptNumber = "2026-000001",
            OrderNumber = "ORD-1",
            IssuedDate = "22.09.2026",
            CustomerName = "Jan Novák",
            LanguageCode = "cs",
            Services = [],
            Packages = [new ReceiptLineItem("Stěhovací balíček", 1000m)],
            Extras = [],
            ExpressSurcharge = 200m,
            PromoDiscount = 100m,
            Total = 1100m,
            Currency = "Kč",
            CreditApplied = 100m,
            AmountDueOnCard = 1000m,
            PaymentStatus = PaymentStatus.Pending,
            PaymentType = PaymentType.Cash,
            IsVatPayer = true,
            NetAmount = 909.09m,
            VatAmount = 190.91m,
            VatRate = 0.21m,
        };

    private static IEnumerable<PropertyInfo> TextLabels() =>
        typeof(ReceiptLabels)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string));

    private sealed class ProbeLayout : DefaultReceiptLayoutBuilder
    {
        public IReadOnlyList<(string Label, string Value, bool IsBold)> Summary(ReceiptPdfData data) =>
            SummaryLines(data);

        public IReadOnlyList<(string Description, decimal Amount)> Items(ReceiptPdfData data) =>
            ItemLines(data);

        public IReadOnlyList<(string Label, string Value)> Payment(ReceiptPdfData data) =>
            PaymentLines(data);

        public string Duration(int minutes, string languageCode) =>
            Duration(minutes, ReceiptLabels.For(languageCode));
    }
}
