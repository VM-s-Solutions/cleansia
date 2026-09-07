using Cleansia.Infra.Services.Pdf.Layouts;
using Cleansia.Infra.Services.Pdf.Models;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Cleansia.Infra.Services.Pdf;

public class QuestPdfService : IPdfService
{
    /// <summary>
    /// One render at a time, process-wide.
    ///
    /// <para><b>QuestPDF 2024.12.1's native Skia is not thread-safe when it builds a subsetted font's
    /// <c>/ToUnicode</c> CMap.</b> With two <c>GeneratePdf</c> calls in flight, ~1-3% of renders emit a
    /// CMap mapping EVERY glyph to U+0000 instead of its real code point. The document looks perfect;
    /// its text layer is dead, so copy, search and extraction return nothing usable. Measured in one
    /// process: 0 corrupt in 300 sequential renders, 39 in 1200 concurrent, 0 in 1200 concurrent behind
    /// this lock. → T-0679</para>
    ///
    /// <para><b>Why a global lock is not the throughput risk it looks like.</b> QuestPDF ALREADY blocks
    /// every render on a static process-wide semaphore —
    /// <c>QuestPDF.Drawing.DocumentGenerator.RenderDocumentSemaphore</c>, verified by reflection at
    /// <c>CurrentCount == 2</c>. The process already parks threads on a global render gate; this moves
    /// it from two to one. One extra parked thread per burst, against a real demand near 0.01
    /// renders/second, and no customer request pays any of it: a receipt download is a blob read, and
    /// the receipt queue is pinned at one message in flight.</para>
    ///
    /// <para>STATIC, not an instance field: <c>IPdfService</c> is registered SCOPED, so every request
    /// gets its own service and an instance lock would gate nothing. What is being protected is
    /// QuestPDF's process-wide typeface cache.</para>
    ///
    /// <para>It wraps ONLY the render call — not the logging, not the country-logic enrichment — so
    /// nothing else in the method is serialised.</para>
    /// </summary>
    private static readonly object RenderGate = new();

    private readonly LayoutBuilderFactory _layoutFactory;
    private readonly ILogger<QuestPdfService> _logger;

    static QuestPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public QuestPdfService(LayoutBuilderFactory layoutFactory, ILogger<QuestPdfService> logger)
    {
        _layoutFactory = layoutFactory;
        _logger = logger;
    }

    public byte[] GenerateReceiptPdf(ReceiptPdfData data, string? countryCode = null)
    {
        _logger.LogInformation("Generating receipt PDF for {ReceiptNumber}, country={Country}",
            data.ReceiptNumber, countryCode ?? "default");

        try
        {
            var builder = _layoutFactory.GetReceiptBuilder(countryCode);
            byte[] pdfBytes;
            lock (RenderGate)
            {
                pdfBytes = Document.Create(c => builder.Build(c, data)).GeneratePdf();
            }

            _logger.LogInformation("Receipt PDF generated successfully ({Size} bytes)", pdfBytes.Length);
            return pdfBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Receipt PDF generation failed for {ReceiptNumber}: {Message}",
                data.ReceiptNumber, ex.Message);
            throw;
        }
    }

    public byte[] GenerateInvoicePdf(InvoicePdfData data, CountryInvoiceContext? context, string? countryCode = null)
    {
        _logger.LogInformation("Generating invoice PDF for {InvoiceNumber}, country={Country}",
            data.InvoiceNumber, countryCode ?? "default");

        try
        {
            var enrichedData = ApplyCountryLogic(data, context);
            var builder = _layoutFactory.GetInvoiceBuilder(countryCode);
            byte[] pdfBytes;
            lock (RenderGate)
            {
                pdfBytes = Document.Create(c => builder.Build(c, enrichedData, context)).GeneratePdf();
            }

            _logger.LogInformation("Invoice PDF generated successfully ({Size} bytes)", pdfBytes.Length);
            return pdfBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invoice PDF generation failed for {InvoiceNumber}: {Message}",
                data.InvoiceNumber, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// The backstop for data built without a country context — the payout mapper already applies
    /// <see cref="CountryInvoiceContext.VatWithinGross"/>, so this only fires for a caller that did not.
    /// The total is left alone: VAT is carved out of a gross payout, never added to it.
    /// </summary>
    public static InvoicePdfData ApplyCountryLogic(InvoicePdfData data, CountryInvoiceContext? context)
    {
        if (context is null || data.VatAmount != 0)
        {
            return data;
        }

        var vatAmount = context.VatWithinGross(data.TotalAmount, data.Supplier.IsVatPayer);
        if (vatAmount == 0)
        {
            return data;
        }

        return data with { VatAmount = vatAmount };
    }
}
