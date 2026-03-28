namespace Cleansia.Infra.Services.Pdf.Layouts;

public class LayoutBuilderFactory(
    IEnumerable<IReceiptLayoutBuilder> receiptBuilders,
    IEnumerable<IInvoiceLayoutBuilder> invoiceBuilders)
{
    public IReceiptLayoutBuilder GetReceiptBuilder(string? countryCode)
    {
        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            var match = receiptBuilders.FirstOrDefault(b =>
                string.Equals(b.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }

        return receiptBuilders.First(b =>
            string.Equals(b.CountryCode, "default", StringComparison.OrdinalIgnoreCase));
    }

    public IInvoiceLayoutBuilder GetInvoiceBuilder(string? countryCode)
    {
        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            var match = invoiceBuilders.FirstOrDefault(b =>
                string.Equals(b.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }

        return invoiceBuilders.First(b =>
            string.Equals(b.CountryCode, "default", StringComparison.OrdinalIgnoreCase));
    }
}
