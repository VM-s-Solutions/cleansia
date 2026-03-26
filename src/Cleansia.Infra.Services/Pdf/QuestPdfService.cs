using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.Infra.Services.Templates;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace Cleansia.Infra.Services.Pdf;

public class QuestPdfService : IPdfService
{
    private readonly ITemplateEngine _templateEngine;
    private readonly ILogger<QuestPdfService> _logger;
    private readonly string? _chromiumExecutablePath;
    private static bool _chromiumDownloaded;

    public QuestPdfService(ITemplateEngine templateEngine, ILogger<QuestPdfService> logger)
    {
        _templateEngine = templateEngine;
        _logger = logger;
        _chromiumExecutablePath = Environment.GetEnvironmentVariable("PUPPETEER_EXECUTABLE_PATH");

        _logger.LogInformation("QuestPdfService initialized. PUPPETEER_EXECUTABLE_PATH={Path}", _chromiumExecutablePath ?? "(not set)");

        if (string.IsNullOrEmpty(_chromiumExecutablePath) && !_chromiumDownloaded)
        {
            _logger.LogInformation("Downloading Chromium via BrowserFetcher...");
            new BrowserFetcher().DownloadAsync().GetAwaiter().GetResult();
            _chromiumDownloaded = true;
            _logger.LogInformation("Chromium downloaded successfully");
        }
    }

    public async Task<byte[]> GenerateInvoicePdfAsync(InvoicePdfData invoiceData, string templateHtml, CountryInvoiceContext? countryContext, CancellationToken cancellationToken)
    {
        var enrichedData = ApplyCountryLogic(invoiceData, countryContext);
        var mergedHtml = await _templateEngine.CompileAsync(templateHtml, enrichedData, cancellationToken);
        return await ConvertHtmlToPdfBytesAsync(mergedHtml, cancellationToken);
    }

    public async Task<byte[]> GenerateReceiptPdfAsync(string templateHtml, CancellationToken cancellationToken)
    {
        return await ConvertHtmlToPdfBytesAsync(templateHtml, cancellationToken);
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
        var executablePath = _chromiumExecutablePath;
        _logger.LogInformation("Launching Chromium for PDF generation. ExecutablePath={Path}", executablePath ?? "(auto-detect)");

        if (!string.IsNullOrEmpty(executablePath) && !File.Exists(executablePath))
        {
            _logger.LogError("Chromium executable not found at {Path}", executablePath);
            throw new FileNotFoundException($"Chromium executable not found at '{executablePath}'", executablePath);
        }

        var launchOptions = new LaunchOptions
        {
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-setuid-sandbox", "--disable-gpu", "--disable-dev-shm-usage" },
            ExecutablePath = executablePath
        };

        using var browser = await Puppeteer.LaunchAsync(launchOptions);
        _logger.LogInformation("Chromium launched successfully");

        await using var page = await browser.NewPageAsync();
        await page.SetContentAsync(html, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Networkidle0 } });

        var pdfOptions = new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            PreferCSSPageSize = true
        };

        var pdfBytes = await page.PdfDataAsync(pdfOptions);
        _logger.LogInformation("PDF generated successfully ({Size} bytes)", pdfBytes.Length);

        return pdfBytes;
    }
}
