using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.Infra.Services.Templates;
using HTMLQuestPDF.Extensions;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Cleansia.Infra.Services.Pdf;

public class QuestPdfService : IPdfService
{
    private readonly ITemplateEngine _templateEngine;
    private readonly ILogger<QuestPdfService> _logger;

    static QuestPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public QuestPdfService(ITemplateEngine templateEngine, ILogger<QuestPdfService> logger)
    {
        _templateEngine = templateEngine;
        _logger = logger;
    }

    public async Task<byte[]> GenerateInvoicePdfAsync(InvoicePdfData invoiceData, string templateHtml, CountryInvoiceContext? countryContext, CancellationToken cancellationToken)
    {
        var enrichedData = ApplyCountryLogic(invoiceData, countryContext);
        var mergedHtml = await _templateEngine.CompileAsync(templateHtml, enrichedData, cancellationToken);
        return ConvertHtmlToPdfBytes(mergedHtml);
    }

    public Task<byte[]> GenerateReceiptPdfAsync(string templateHtml, CancellationToken cancellationToken)
    {
        var pdfBytes = ConvertHtmlToPdfBytes(templateHtml);
        return Task.FromResult(pdfBytes);
    }

    private InvoicePdfData ApplyCountryLogic(InvoicePdfData data, CountryInvoiceContext? context)
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

    private byte[] ConvertHtmlToPdfBytes(string html)
    {
        _logger.LogInformation("Generating PDF from HTML using QuestPDF");

        try
        {
            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(QuestPDF.Helpers.PageSizes.A4);
                    page.Content().HTML(h => h.SetHtml(html));
                });
            }).GeneratePdf();

            _logger.LogInformation("PDF generated successfully ({Size} bytes)", pdfBytes.Length);
            return pdfBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF generation failed: {Message}", ex.Message);
            throw;
        }
    }
}
