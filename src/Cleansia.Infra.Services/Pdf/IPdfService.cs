using Cleansia.Infra.Services.Pdf.Models;

namespace Cleansia.Infra.Services.Pdf;

public interface IPdfService
{
    byte[] GenerateReceiptPdf(ReceiptPdfData data, string? countryCode = null);
    byte[] GenerateInvoicePdf(InvoicePdfData data, CountryInvoiceContext? context, string? countryCode = null);
}
