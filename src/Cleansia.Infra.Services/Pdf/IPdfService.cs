using Cleansia.Infra.Services.Pdf.Models;

namespace Cleansia.Infra.Services.Pdf;

public interface IPdfService
{
    Task<byte[]> GenerateInvoicePdfAsync(InvoicePdfData invoiceData, string templateHtml, CountryInvoiceContext? countryContext, CancellationToken cancellationToken);
    Task<byte[]> GenerateReceiptPdfAsync(string templateHtml, CancellationToken cancellationToken);
}
