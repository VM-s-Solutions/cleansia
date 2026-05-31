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
    private readonly ICountryInvoiceConfigRepository _countryInvoiceConfigRepository;
    private readonly ICountryConfigurationRepository _countryConfigurationRepository;
    private readonly IPdfService _pdfService;
    private readonly IBlobContainerClientFactory _blobContainerClientFactory;
    private readonly ITenantProvider _tenantProvider;

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
        ICountryInvoiceConfigRepository countryInvoiceConfigRepository,
        ICountryConfigurationRepository countryConfigurationRepository,
        IPdfService pdfService,
        IBlobContainerClientFactory blobContainerClientFactory,
        ITenantProvider tenantProvider)
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
        _countryInvoiceConfigRepository = countryInvoiceConfigRepository;
        _countryConfigurationRepository = countryConfigurationRepository;
        _pdfService = pdfService;
        _blobContainerClientFactory = blobContainerClientFactory;
        _tenantProvider = tenantProvider;
    }

    public async Task EnsureOpenPeriodAsync(CancellationToken cancellationToken = default)
    {
        // Cross-tenant scan: pay-calc on a tenant-scoped order should bootstrap
        // a tenant-scoped PayPeriod for that same tenant. Today the system is
        // single-tenant in practice (TenantId null), so the simple "any open
        // period for the active tenant context" check is sufficient. The
        // multi-tenant flow already loops per tenant in
        // CloseExpiredPeriodsAndOpenNewAsync; bootstrap inherits the active
        // tenant override from the caller (queue consumer sets none → null).
        var hasOpen = await _payPeriodRepository
            .GetQueryable()
            .AnyAsync(p => p.Status == PayPeriodStatus.Open, cancellationToken);

        if (hasOpen) return;

        // Pick a monthly window anchored on today. Matches the cadence the
        // close-and-rollover job uses (newStartDate = previousEndDate + 1,
        // newEndDate = +1 month -1 day), so once timer-driven rollover kicks
        // in the seam is invisible.
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var period = PayPeriod.Create(startDate, endDate);
        _payPeriodRepository.Add(period);
        await _unitOfWork.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Auto-seeded Open pay period {PeriodId} ({StartDate} - {EndDate}) " +
            "because no Open period existed",
            period.Id,
            period.StartDate.ToString("yyyy-MM-dd"),
            period.EndDate.ToString("yyyy-MM-dd"));
    }

    public async Task CloseExpiredPeriodsAndOpenNewAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting pay period auto-close job at {Time}", DateTime.UtcNow);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // System job — no JWT context. Use IgnoreQueryFilters to see rows
            // across all tenants; group by tenant and set the override per
            // tenant before mutating so new PayPeriod / EmployeeInvoice rows
            // are stamped with the correct TenantId.
            var expiredPeriods = await _payPeriodRepository
                .GetQueryableIgnoringTenant()
                .Where(p => p.Status == PayPeriodStatus.Open && p.EndDate < today)
                .ToListAsync(cancellationToken);

            
            if (!expiredPeriods.Any())
            {
                _logger.LogInformation("No expired pay periods found");
                return;
            }

            _logger.LogInformation("Found {Count} expired pay periods to close", expiredPeriods.Count);

            foreach (var tenantGroup in expiredPeriods.GroupBy(p => p.TenantId ?? string.Empty))
            {
                // Reset before each iteration so a non-empty override from the
                // previous group doesn't leak into a single-tenant (empty key)
                // group that follows it.
                _tenantProvider.ClearTenantOverride();
                if (!string.IsNullOrEmpty(tenantGroup.Key))
                {
                    _tenantProvider.SetTenantOverride(tenantGroup.Key);
                }

                foreach (var period in tenantGroup)
                {
                    try
                    {
                        period.Close("System", "Automatically closed by background job");
                        _logger.LogInformation(
                            "Closed pay period {PeriodId} ({StartDate} - {EndDate})",
                            period.Id,
                            period.StartDate.ToString("yyyy-MM-dd"),
                            period.EndDate.ToString("yyyy-MM-dd"));

                        await SendPeriodClosedEmailsAsync(period, cancellationToken);

                        // Within the current tenant — check if any open period exists.
                        var hasActivePeriod = await _payPeriodRepository
                            .GetQueryable()
                            .AnyAsync(p => p.Status == PayPeriodStatus.Open, cancellationToken);

                        if (!hasActivePeriod)
                        {
                            var newStartDate = period.EndDate.AddDays(1);
                            var newEndDate = newStartDate.AddMonths(1).AddDays(-1);

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

                // Commit per-tenant so new rows inherit the right TenantId.
                await _unitOfWork.CommitAsync(cancellationToken);
            }

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
                    var languageCode = employee.User.PreferredLanguageCode ?? Constants.Language.English;

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
        var orderPays = await _orderEmployeePayRepository.GetUnassignedForEmployeePeriodAsync(
            employee.Id, period.Id, cancellationToken);

        if (!orderPays.Any())
        {
            return null;
        }

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

        var subTotal = orderPays.Sum(p => p.BasePay + p.ExtrasPay + p.ExpensesPay);
        var bonusAmount = orderPays.Sum(p => p.BonusPay);
        var deductionAmount = orderPays.Sum(p => p.DeductionPay);

        var currency = await _currencyRepository.GetByCodeAsync(employee.PreferredCurrencyCode ?? string.Empty, cancellationToken) ??
                       await _currencyRepository.GetDefaultAsync(cancellationToken);

        var invoice = EmployeeInvoice.Create(
            employee.Id,
            period.Id,
            orderPays.Count,
            subTotal,
            currency!.Id,
            bonusAmount,
            deductionAmount);

        _employeeInvoiceRepository.Add(invoice);

        foreach (var orderPay in orderPays)
        {
            orderPay.AssignToInvoice(invoice.Id);
        }

        var language = await _languageRepository.GetByCodeAsync(languageCode, cancellationToken) ??
                       await _languageRepository.GetByCodeAsync(Constants.Language.English, cancellationToken);

        if (language == null)
        {
            _logger.LogError("No language found for code {LanguageCode} or fallback 'en'", languageCode);
            return null;
        }

        try
        {
            var pdfBytes = await GenerateInvoicePdfAsync(invoice, employee, currency, orderPays, language.Code, cancellationToken);

            if (pdfBytes == null || pdfBytes.Length == 0)
            {
                throw new InvalidOperationException("PDF generation returned empty result");
            }

            var pdfBlobUrl = await UploadInvoicePdfAsync(invoice, employee, pdfBytes, cancellationToken);
            invoice.SetPdfBlobUrl(pdfBlobUrl);
            invoice.ClearPdfGenerationError();

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
        IReadOnlyList<OrderEmployeePay> orderPays,
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

            var dateFormat = "dd.MM.yyyy";
            if (!string.IsNullOrEmpty(countryId))
            {
                var countryConfig = await _countryConfigurationRepository.GetByCountryIdAsync(countryId, cancellationToken);
                if (!string.IsNullOrEmpty(countryConfig?.DateFormat))
                    dateFormat = countryConfig.DateFormat;
            }

            var pdfData = invoice.CreatePdfData(employee, currency, orderPays, countryContext, companyInfo, dateFormat);

            var countryCode = employee.Address?.Country?.IsoCode;
            var pdfBytes = _pdfService.GenerateInvoicePdf(pdfData, countryContext, countryCode);

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
