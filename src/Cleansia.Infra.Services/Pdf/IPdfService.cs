using Cleansia.Infra.Services.Pdf.Models;

namespace Cleansia.Infra.Services.Pdf;

public interface IPdfService
{
    byte[] GenerateReceiptPdf(ReceiptPdfData data, string? countryCode = null);
    byte[] GenerateInvoicePdf(InvoicePdfData data, CountryInvoiceContext? context, string? countryCode = null);

    /// <summary>
    /// The customer incident file. The SHA-256 returned beside the bytes is the one printed on the
    /// last page — the caller records it, so the audit row and the document name the same content.
    /// </summary>
    IncidentFilePdf GenerateIncidentFilePdf(IncidentFilePdfData data);
}

public sealed record IncidentFilePdf(byte[] Bytes, string DataSha256);
