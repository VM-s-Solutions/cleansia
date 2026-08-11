using System.Globalization;
using Cleansia.Infra.Services.Pdf.Models;

namespace Cleansia.Infra.Services.Pdf.Layouts;

public class CzechInvoiceLayoutBuilder : DefaultInvoiceLayoutBuilder
{
    public override string CountryCode => "CZ";

    public override IReadOnlyCollection<string> CountryCodes => ["CZ", "CZE"];

    protected override InvoiceLabels Labels { get; } = InvoiceLabels.Czech;

    protected override CultureInfo NumberCulture { get; } = CultureInfo.GetCultureInfo("cs-CZ");

    protected override string FormatMoney(decimal amount, InvoicePdfData data) =>
        $"{amount.ToString("N2", NumberCulture)} {data.CurrencySymbol}";
}
