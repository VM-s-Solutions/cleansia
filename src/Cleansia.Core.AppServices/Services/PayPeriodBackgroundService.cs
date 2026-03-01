using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.Infra.Services.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Services;

public class PayPeriodBackgroundService : IPayPeriodBackgroundService
{
    private readonly IPayPeriodRepository _payPeriodRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PayPeriodBackgroundService> _logger;
    private readonly ICurrencyRepository _currencyRepository;
    private readonly IEmployeeInvoiceRepository _employeeInvoiceRepository;
    private readonly IOrderEmployeePayRepository _orderEmployeePayRepository;
    private readonly ICompanyInfoRepository _companyInfoRepository;
    private readonly ILanguageRepository _languageRepository;
    private readonly IInvoiceTemplateRepository _invoiceTemplateRepository;
    private readonly ICountryInvoiceConfigRepository _countryInvoiceConfigRepository;
    private readonly ICountryConfigurationRepository _countryConfigurationRepository;
    private readonly IPdfService _pdfService;
    private readonly ITemplateEngine _templateEngine;
    private readonly IBlobContainerClientFactory _blobContainerClientFactory;

    public PayPeriodBackgroundService(
        IPayPeriodRepository payPeriodRepository,
        IEmployeeRepository employeeRepository,
        IEmailService emailService,
        IUnitOfWork unitOfWork,
        ILogger<PayPeriodBackgroundService> logger,
        ICurrencyRepository currencyRepository,
        IEmployeeInvoiceRepository employeeInvoiceRepository,
        IOrderEmployeePayRepository orderEmployeePayRepository,
        ICompanyInfoRepository companyInfoRepository,
        ILanguageRepository languageRepository,
        IInvoiceTemplateRepository invoiceTemplateRepository,
        ICountryInvoiceConfigRepository countryInvoiceConfigRepository,
        ICountryConfigurationRepository countryConfigurationRepository,
        IPdfService pdfService,
        ITemplateEngine templateEngine,
        IBlobContainerClientFactory blobContainerClientFactory)
    {
        _payPeriodRepository = payPeriodRepository;
        _employeeRepository = employeeRepository;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _currencyRepository = currencyRepository;
        _employeeInvoiceRepository = employeeInvoiceRepository;
        _orderEmployeePayRepository = orderEmployeePayRepository;
        _companyInfoRepository = companyInfoRepository;
        _languageRepository = languageRepository;
        _invoiceTemplateRepository = invoiceTemplateRepository;
        _countryInvoiceConfigRepository = countryInvoiceConfigRepository;
        _countryConfigurationRepository = countryConfigurationRepository;
        _pdfService = pdfService;
        _templateEngine = templateEngine;
        _blobContainerClientFactory = blobContainerClientFactory;
    }

    public async Task CloseExpiredPeriodsAndOpenNewAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting pay period auto-close job at {Time}", DateTime.UtcNow);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // Find all open pay periods that have ended
            var expiredPeriods = await _payPeriodRepository
                .GetQueryable()
                .Where(p => p.Status == PayPeriodStatus.Open && p.EndDate < today)
                .ToListAsync(cancellationToken);

            if (!expiredPeriods.Any())
            {
                _logger.LogInformation("No expired pay periods found");
                return;
            }

            _logger.LogInformation("Found {Count} expired pay periods to close", expiredPeriods.Count);

            foreach (var period in expiredPeriods)
            {
                try
                {
                    // Close the expired period (auto-closed by system)
                    period.Close("System", "Automatically closed by background job");
                    _logger.LogInformation(
                        "Closed pay period {PeriodId} ({StartDate} - {EndDate})",
                        period.Id,
                        period.StartDate.ToString("yyyy-MM-dd"),
                        period.EndDate.ToString("yyyy-MM-dd"));

                    // Send email notifications to all active employees
                    await SendPeriodClosedEmailsAsync(period, cancellationToken);

                    // Check if we need to create a new period
                    var hasActivePeriod = await _payPeriodRepository
                        .GetQueryable()
                        .AnyAsync(p => p.Status == PayPeriodStatus.Open, cancellationToken);

                    if (!hasActivePeriod)
                    {
                        // Create new period starting the day after the closed period ended
                        var newStartDate = period.EndDate.AddDays(1);
                        var newEndDate = newStartDate.AddMonths(1).AddDays(-1); // One month period

                        var newPeriod = PayPeriod.Create(newStartDate, newEndDate);
                        _payPeriodRepository.Add(newPeriod);

                        _logger.LogInformation(
                            "Created new pay period {PeriodId} ({StartDate} - {EndDate})",
                            newPeriod.Id,
                            newPeriod.StartDate.ToString("yyyy-MM-dd"),
                            newPeriod.EndDate.ToString("yyyy-MM-dd"));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error processing pay period {PeriodId}",
                        period.Id);
                }
            }

            // Commit all changes to database
            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Pay period auto-close job completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in pay period auto-close job");
            throw;
        }
    }

    private async Task SendPeriodClosedEmailsAsync(PayPeriod period, CancellationToken cancellationToken)
    {
        try
        {
            var employees = await _employeeRepository
                .GetQueryable()
                .Include(e => e.User)
                .Include(e => e.Address)
                .Where(e => e.IsActive)
                .ToListAsync(cancellationToken);

            if (!employees.Any())
            {
                _logger.LogInformation("No active employees found to notify about period closure");
                return;
            }

            _logger.LogInformation("Sending period closed emails with invoices to {Count} employees", employees.Count);

            foreach (var employee in employees)
            {
                try
                {
                    if (employee.User == null || string.IsNullOrWhiteSpace(employee.User.Email))
                    {
                        _logger.LogWarning("Employee {EmployeeId} has no user or email, skipping notification", employee.Id);
                        continue;
                    }

                    var employeeName = $"{employee.User.FirstName} {employee.User.LastName}";
                    var languageCode = employee.User.PreferredLanguageCode ?? "en";

                    // Generate invoice and PDF for employee
                    byte[]? invoicePdfBytes = null;
                    string? invoiceFileName = null;

                    try
                    {
                        var invoiceResult = await GenerateInvoiceForEmployeeAsync(employee, period, languageCode, cancellationToken);
                        if (invoiceResult != null)
                        {
                            invoicePdfBytes = invoiceResult.Value.PdfBytes;
                            invoiceFileName = invoiceResult.Value.FileName;
                            _logger.LogInformation(
                                "Generated invoice {FileName} for employee {EmployeeId}",
                                invoiceFileName,
                                employee.Id);
                        }
                        else
                        {
                            _logger.LogInformation(
                                "No unpaid orders found for employee {EmployeeId}, skipping invoice generation",
                                employee.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Failed to generate invoice for employee {EmployeeId}, will send email without invoice",
                            employee.Id);
                        // Continue to send email without invoice
                    }

                    await _emailService.SendPeriodClosedEmailAsync(
                        employee.User.Email,
                        employeeName,
                        period.StartDate,
                        period.EndDate,
                        period.ClosedAt ?? DateTime.UtcNow,
                        period.GetPeriodLabel(),
                        languageCode,
                        invoicePdfBytes,
                        invoiceFileName,
                        cancellationToken);

                    _logger.LogInformation(
                        "Sent period closed email to {Email} for period {PeriodId}",
                        employee.User.Email,
                        period.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to send period closed email to employee {EmployeeId} ({Email})",
                        employee.Id,
                        employee.User?.Email ?? "unknown");
                    // Continue processing other employees even if one fails
                }
            }

            _logger.LogInformation("Finished sending period closed email notifications");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending period closed email notifications");
            // Don't throw - we don't want email failures to prevent period closure
        }
    }

    private async Task<(byte[] PdfBytes, string FileName)?> GenerateInvoiceForEmployeeAsync(
        Employee employee,
        PayPeriod period,
        string languageCode,
        CancellationToken cancellationToken)
    {
        // Check if employee has unpaid order pays for this period
        var orderPays = await _orderEmployeePayRepository
            .GetByEmployeeId(employee.Id)
            .Where(p => p.PayPeriodId == period.Id && p.EmployeeInvoiceId == null)
            .Include(p => p.Order)
            .ToListAsync(cancellationToken);

        if (!orderPays.Any())
        {
            return null;
        }

        // Check if invoice already exists for this employee and period
        var existingInvoice = await _employeeInvoiceRepository
            .GetQueryable()
            .FirstOrDefaultAsync(i => i.EmployeeId == employee.Id && i.PayPeriodId == period.Id, cancellationToken);

        if (existingInvoice != null)
        {
            _logger.LogWarning(
                "Invoice already exists for employee {EmployeeId} and period {PeriodId}, skipping generation",
                employee.Id,
                period.Id);
            return null;
        }

        // Calculate invoice totals
        var subTotal = orderPays.Sum(p => p.BasePay + p.ExtrasPay + p.ExpensesPay);
        var bonusAmount = orderPays.Sum(p => p.BonusPay);
        var deductionAmount = orderPays.Sum(p => p.DeductionPay);

        // Get currency
        var currency = await _currencyRepository.GetByCodeAsync(employee.PreferredCurrencyCode ?? string.Empty, cancellationToken) ??
                       await _currencyRepository.GetDefaultAsync(cancellationToken);

        // Create invoice
        var invoice = EmployeeInvoice.Create(
            employee.Id,
            period.Id,
            orderPays.Count,
            subTotal,
            currency!.Id,
            bonusAmount,
            deductionAmount);

        _employeeInvoiceRepository.Add(invoice);

        // Assign order pays to invoice
        foreach (var orderPay in orderPays)
        {
            orderPay.AssignToInvoice(invoice.Id);
        }

        // Get language for template
        var language = await _languageRepository.GetByCodeAsync(languageCode, cancellationToken) ??
                       await _languageRepository.GetByCodeAsync("en", cancellationToken);

        if (language == null)
        {
            _logger.LogError("No language found for code {LanguageCode} or fallback 'en'", languageCode);
            return null;
        }

        // Generate PDF with error handling
        try
        {
            var pdfBytes = await GenerateInvoicePdfAsync(invoice, employee, currency, orderPays, language.Code, cancellationToken);

            if (pdfBytes == null || pdfBytes.Length == 0)
            {
                throw new InvalidOperationException("PDF generation returned empty result");
            }

            // Upload PDF to blob storage
            var pdfBlobUrl = await UploadInvoicePdfAsync(invoice, employee, pdfBytes, cancellationToken);
            invoice.SetPdfBlobUrl(pdfBlobUrl);
            invoice.ClearPdfGenerationError(); // Clear any previous errors

            var fileName = $"{invoice.InvoiceNumber}.pdf";
            return (pdfBytes, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate PDF for invoice {InvoiceId}", invoice.Id);

            // Mark invoice with error but don't fail entire process
            invoice.SetPdfGenerationError(ex.Message);

            // Invoice will be created without PDF
            // Admin can regenerate PDF later via RegenerateInvoicePdf endpoint
            return null;
        }
    }

    private async Task<byte[]?> GenerateInvoicePdfAsync(
        EmployeeInvoice invoice,
        Employee employee,
        Currency? currency,
        List<OrderEmployeePay> orderPays,
        string languageCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var countryId = employee.Address?.CountryId;

            // Try to get company info by employee's country, fallback to any active
            var companyInfo = countryId != null
                ? await _companyInfoRepository.GetActiveByCountryAsync(countryId, cancellationToken)
                : null;
            companyInfo ??= await _companyInfoRepository.GetActiveCompanyInfoAsync(cancellationToken);

            if (companyInfo == null)
            {
                _logger.LogError("No active company info found for country {CountryId}", countryId);
                return null;
            }
            var countryContext = await GetCountryInvoiceContextAsync(countryId, cancellationToken);

            // Get date format from country configuration
            var dateFormat = "dd.MM.yyyy";
            if (!string.IsNullOrEmpty(countryId))
            {
                var countryConfig = await _countryConfigurationRepository.GetByCountryIdAsync(countryId, cancellationToken);
                if (!string.IsNullOrEmpty(countryConfig?.DateFormat))
                    dateFormat = countryConfig.DateFormat;
            }

            var pdfData = invoice.CreatePdfData(employee, currency, orderPays, countryContext, companyInfo, dateFormat);

            var templateHtml = await GetTemplateHtmlAsync(countryId, languageCode, cancellationToken);
            if (templateHtml == null)
            {
                _logger.LogError("Failed to get invoice template for country {CountryId} and language {LanguageCode}", countryId, languageCode);
                return null;
            }

            var mergedHtml = await _templateEngine.CompileAsync(templateHtml, pdfData, cancellationToken);

            var pdfBytes = await _pdfService.GenerateInvoicePdfAsync(
                pdfData,
                mergedHtml,
                countryContext,
                cancellationToken);

            return pdfBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating invoice PDF");
            return null;
        }
    }

    private async Task<CountryInvoiceContext?> GetCountryInvoiceContextAsync(string? countryId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(countryId)) return null;

        var config = await _countryInvoiceConfigRepository.GetByCountryIdAsync(countryId, cancellationToken);
        if (config == null)
        {
            return null;
        }

        return new CountryInvoiceContext
        {
            VatRequired = config.VatRequired,
            VatRate = config.VatRate,
            DigitalSignatureRequired = config.DigitalSignatureRequired,
            EInvoiceFormat = config.EInvoiceFormat,
            LegalDisclaimerTemplate = config.LegalDisclaimerTemplate
        };
    }

    private async Task<string?> GetTemplateHtmlAsync(string? countryId, string languageCode, CancellationToken cancellationToken)
    {
        try
        {
            var template = await _invoiceTemplateRepository.GetActiveByCountryAndLanguageAsync(
                countryId,
                languageCode,
                cancellationToken);

            if (template == null)
            {
                _logger.LogWarning("No invoice template found for country {CountryId} and language {LanguageCode}", countryId, languageCode);
                return null;
            }

            var templateBlobClient = _blobContainerClientFactory.GetBlobContainerClient(Common.Constants.BlobContainers.InvoiceTemplates);
            var templateBlob = await templateBlobClient.DownloadAsync(template.BlobUrl, cancellationToken);
            using var reader = new StreamReader(templateBlob.Content);
            return await reader.ReadToEndAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting invoice template HTML");
            return null;
        }
    }

    private async Task<string> UploadInvoicePdfAsync(
        EmployeeInvoice invoice,
        Employee employee,
        byte[] pdfBytes,
        CancellationToken cancellationToken)
    {
        var employeeName = $"{employee.User?.FirstName}_{employee.User?.LastName}";
        var payPeriodDescription = invoice.PayPeriod!.GetPeriodLabel();
        var invoiceFileName = invoice.InvoiceNumber;

        var blobName = $"{payPeriodDescription}/{employeeName}/{invoiceFileName}.pdf";
        var blobClient = _blobContainerClientFactory.GetBlobContainerClient(Common.Constants.BlobContainers.GeneratedInvoices);

        using var pdfStream = new MemoryStream(pdfBytes);
        await blobClient.UploadAsync(blobName, pdfStream, cancellationToken: cancellationToken);

        return blobClient.GetBlobUri(blobName).ToString();
    }
}
