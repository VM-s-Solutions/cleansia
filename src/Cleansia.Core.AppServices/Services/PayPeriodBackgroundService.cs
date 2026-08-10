using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Services;

public class PayPeriodBackgroundService : IPayPeriodBackgroundService
{
    private const string EmptyRenderMessage = "PDF generation returned empty result";
    private const string NoCompanyInfoMessage = "No active company info is configured to issue this invoice against";

    private readonly IPayPeriodRepository _payPeriodRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PayPeriodBackgroundService> _logger;
    private readonly ICurrencyRepository _currencyRepository;
    private readonly IEmployeeInvoiceRepository _employeeInvoiceRepository;
    private readonly IEmployeePayoutDetailsRepository _employeePayoutDetailsRepository;
    private readonly IOrderEmployeePayRepository _orderEmployeePayRepository;
    private readonly ICompanyInfoRepository _companyInfoRepository;
    private readonly ILanguageRepository _languageRepository;
    private readonly ICountryInvoiceConfigRepository _countryInvoiceConfigRepository;
    private readonly ICountryConfigurationRepository _countryConfigurationRepository;
    private readonly IPdfService _pdfService;
    private readonly IBlobContainerClientFactory _blobContainerClientFactory;
    private readonly ITenantProvider _tenantProvider;
    private readonly IPayoutReferenceAllocator _payoutReferenceAllocator;

    public PayPeriodBackgroundService(
        IPayPeriodRepository payPeriodRepository,
        IEmployeeRepository employeeRepository,
        IEmailService emailService,
        IUnitOfWork unitOfWork,
        ILogger<PayPeriodBackgroundService> logger,
        ICurrencyRepository currencyRepository,
        IEmployeeInvoiceRepository employeeInvoiceRepository,
        IEmployeePayoutDetailsRepository employeePayoutDetailsRepository,
        IOrderEmployeePayRepository orderEmployeePayRepository,
        ICompanyInfoRepository companyInfoRepository,
        ILanguageRepository languageRepository,
        ICountryInvoiceConfigRepository countryInvoiceConfigRepository,
        ICountryConfigurationRepository countryConfigurationRepository,
        IPdfService pdfService,
        IBlobContainerClientFactory blobContainerClientFactory,
        ITenantProvider tenantProvider,
        IPayoutReferenceAllocator payoutReferenceAllocator)
    {
        _payPeriodRepository = payPeriodRepository;
        _employeeRepository = employeeRepository;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _currencyRepository = currencyRepository;
        _employeeInvoiceRepository = employeeInvoiceRepository;
        _employeePayoutDetailsRepository = employeePayoutDetailsRepository;
        _orderEmployeePayRepository = orderEmployeePayRepository;
        _companyInfoRepository = companyInfoRepository;
        _languageRepository = languageRepository;
        _countryInvoiceConfigRepository = countryInvoiceConfigRepository;
        _countryConfigurationRepository = countryConfigurationRepository;
        _pdfService = pdfService;
        _blobContainerClientFactory = blobContainerClientFactory;
        _tenantProvider = tenantProvider;
        _payoutReferenceAllocator = payoutReferenceAllocator;
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
                    .ThenInclude(a => a!.Country)
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

        var currency = await _currencyRepository.GetByCodeAsync(employee.PreferredCurrencyCode ?? string.Empty, cancellationToken) ??
                       await _currencyRepository.GetDefaultAsync(cancellationToken);

        var variableSymbol = await _payoutReferenceAllocator.AllocateAsync(cancellationToken);
        if (variableSymbol.IsFailure)
        {
            _logger.LogError(
                "Could not allocate a payout reference for employee {EmployeeId} / period {PeriodId} ({Error}); skipping this employee's invoice",
                employee.Id,
                period.Id,
                variableSymbol.Error?.Message);
            return null;
        }

        var invoice = EmployeeInvoice.CreateFromOrderPays(
            employee.Id,
            period.Id,
            orderPays,
            currency!.Id,
            variableSymbol.Value!);

        _employeeInvoiceRepository.Add(invoice);

        foreach (var orderPay in orderPays)
        {
            orderPay.AssignToInvoice(invoice.Id);
        }

        // C1 — make the reference durable BEFORE any document carrying it is rendered, uploaded or
        // emailed. Today the whole group commits at the end, after every cleaner has already been
        // emailed their PDF, so one bad row means everyone has an invoice in their inbox and no
        // invoice row exists for any of them.
        try
        {
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbConstraintViolation.IsUniqueViolation(ex))
        {
            // Rollback() is context-global — it sets EVERY tracked entry to Unchanged, including
            // period.Close() if no earlier employee in this period has already committed. So on the
            // FIRST invoicing employee of a period this also reverts the close: the period stays Open,
            // is re-selected on the next tick, and its period-closed emails go out a second time. No
            // duplicate invoice results (the already-has-one guard above skips it) and no money moves.
            _unitOfWork.Rollback();

            _logger.LogError(
                ex,
                "Duplicate payout reference {VariableSymbol} for employee {EmployeeId} / period {PeriodId}; skipping this employee's invoice",
                variableSymbol.Value,
                employee.Id,
                period.Id);
            return null;
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
                throw new InvalidOperationException(EmptyRenderMessage);
            }

            var pdfBlobUrl = await UploadInvoicePdfAsync(invoice, employee, pdfBytes, cancellationToken);
            invoice.SetPdfBlobUrl(pdfBlobUrl);
            invoice.ClearPdfGenerationError();

            // C2 — without it these mutations ride the NEXT employee's commit and are lost with
            // whatever fails next.
            await _unitOfWork.CommitAsync(cancellationToken);

            var fileName = $"{invoice.InvoiceNumber}.pdf";
            return (pdfBytes, fileName);
        }
        // A cancelled run is not a failed render: nothing was attempted to completion, and the recording
        // commit would be cancelled with it — leaving the stamp dirty in the tracker to ride the NEXT
        // employee's commit. It propagates instead.
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to generate PDF for invoice {InvoiceId}", invoice.Id);

            // The row is the admin's only durable signal that this invoice needs a re-render (ADR-0046
            // Erratum E5), so it must carry the CAUSE and not a placeholder standing in for it.
            invoice.SetPdfGenerationError(ex.Message);

            // C2 on the failure arm too: the PDF-error state for the employee whose generation just
            // failed is exactly what is lost if it rides a later commit that also fails.
            await _unitOfWork.CommitAsync(cancellationToken);

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
        var countryId = employee.Address?.CountryId;

        // Try to get company info by employee's country, fallback to any active
        var companyInfo = countryId != null
            ? await _companyInfoRepository.GetActiveByCountryAsync(countryId, cancellationToken)
            : null;
        companyInfo ??= await _companyInfoRepository.GetActiveCompanyInfoAsync(cancellationToken);

        if (companyInfo == null)
        {
            throw new InvalidOperationException(NoCompanyInfoMessage);
        }

        var countryContext = await GetCountryInvoiceContextAsync(countryId, cancellationToken);

        var dateFormat = "dd.MM.yyyy";
        if (!string.IsNullOrEmpty(countryId))
        {
            var countryConfig = await _countryConfigurationRepository.GetByCountryIdAsync(countryId, cancellationToken);
            if (!string.IsNullOrEmpty(countryConfig?.DateFormat))
                dateFormat = countryConfig.DateFormat;
        }

        var payoutDetails = await _employeePayoutDetailsRepository
            .GetByEmployeeIdAsync(invoice.EmployeeId, cancellationToken);

        var pdfData = invoice.CreatePdfData(employee, currency, orderPays, countryContext, companyInfo, payoutDetails, dateFormat);

        var countryCode = employee.Address?.Country?.IsoCode;

        return _pdfService.GenerateInvoicePdf(pdfData, countryContext, countryCode);
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
            LegalDisclaimerTemplate = config.LegalDisclaimerTemplate,
            LegalDisclaimerLanguageCode = config.LegalDisclaimerLanguageCode,
            LegalDisclaimerReviewStatus = config.LegalDisclaimerReviewStatus,
            ConstantSymbol = config.ConstantSymbol
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
