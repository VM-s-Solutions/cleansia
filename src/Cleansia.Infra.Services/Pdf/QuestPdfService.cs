using Cleansia.Infra.Services.Pdf.Layouts;
using Cleansia.Infra.Services.Pdf.Models;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Cleansia.Infra.Services.Pdf;

public class QuestPdfService : IPdfService
{
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
            var pdfBytes = Document.Create(c => builder.Build(c, data)).GeneratePdf();

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
            var pdfBytes = Document.Create(c => builder.Build(c, enrichedData, context)).GeneratePdf();

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

    private static InvoicePdfData ApplyCountryLogic(InvoicePdfData data, CountryInvoiceContext? context)
    {
        if (context?.VatRequired == true && data.VatAmount == 0)
        {
            var vatAmount = data.SubTotal * context.VatRate;
            return data with
            {
                VatAmount = vatAmount,
                TotalAmount = data.SubTotal + data.BonusAmount - data.DeductionAmount + vatAmount
            };
        }

        return data;
    }
}
