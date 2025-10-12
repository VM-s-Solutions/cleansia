using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.Infra.Services.Templates;
using PuppeteerSharp; // Add this
using PuppeteerSharp.Media;

namespace Cleansia.Infra.Services.Pdf;

public class QuestPdfService : IPdfService
{
    private readonly ITemplateEngine _templateEngine;
    private static bool _chromiumDownloaded = false;

    public QuestPdfService(ITemplateEngine templateEngine)
    {
        _templateEngine = templateEngine;

        if (!_chromiumDownloaded)
        {
            new BrowserFetcher().DownloadAsync().GetAwaiter().GetResult();
            _chromiumDownloaded = true;
        }
    }

    public async Task<byte[]> GenerateInvoicePdfAsync(
        InvoicePdfData invoiceData,
        string templateHtml,
        CountryInvoiceContext? countryContext,
        CancellationToken cancellationToken)
    {
        var enrichedData = ApplyCountryLogic(invoiceData, countryContext);

        var mergedHtml = await _templateEngine.CompileAsync(templateHtml, enrichedData, cancellationToken);

        return await ConvertHtmlToPdfBytesAsync(mergedHtml, cancellationToken);
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

    private async Task<byte[]> ConvertHtmlToPdfBytesAsync(string html, CancellationToken cancellationToken)
    {
        var launchOptions = new LaunchOptions
        {
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
        };

        using var browser = await Puppeteer.LaunchAsync(launchOptions);
        await using var page = await browser.NewPageAsync();

        await page.SetContentAsync(html, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Networkidle0 } });

        var pdfOptions = new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            PreferCSSPageSize = true
        };

        return await page.PdfDataAsync(pdfOptions);
    }
}