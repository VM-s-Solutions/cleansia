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
using Cleansia.Core.Domain.Payments;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

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
    private readonly ITenantRepository _tenantRepository;
    private readonly ICashLedgerRepository _cashLedgerRepository;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IPayPeriodClosingConfig _payPeriodClosingConfig;

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
        IPayoutReferenceAllocator payoutReferenceAllocator,
        ITenantRepository tenantRepository,
        ICashLedgerRepository cashLedgerRepository,
        IServiceScopeFactory serviceScopeFactory,
        IPayPeriodClosingConfig payPeriodClosingConfig)
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
        _tenantRepository = tenantRepository;
        _cashLedgerRepository = cashLedgerRepository;
        _serviceScopeFactory = serviceScopeFactory;
        _payPeriodClosingConfig = payPeriodClosingConfig;
    }

    public async Task EnsureOpenPeriodAsync(CancellationToken cancellationToken = default)
    {
        // Per-company bootstrap: the open-period read goes through the tenant
        // filter and the new row is stamped from the ambient tenant at commit,
        // so this answers for whatever tenant the caller has adopted. The
        // scheduled rollover loops per tenant in CloseExpiredPeriodsAndOpenNewAsync.
        var hasOpen = await _payPeriodRepository
            .GetQueryable()
            .AnyAsync(p => p.Status == PayPeriodStatus.Open, cancellationToken);

        if (hasOpen) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var latestEnd = await _payPeriodRepository
            .GetQueryable()
            .MaxAsync(p => (DateOnly?)p.EndDate, cancellationToken);
        var start = latestEnd >= today ? latestEnd.Value.AddDays(1) : today;
        var period = PayPeriod.CreateBiWeekly(start, _payPeriodClosingConfig.LengthInDays);
        _payPeriodRepository.Add(period);
        try
        {
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbConstraintViolation.IsUniqueViolation(ex))
        {
            // Another writer opened the company's period after the read above. Detach the refused row so a
            // later commit in this scope does not retry it.
            _payPeriodRepository.Remove(period);
            if (!await _payPeriodRepository.ExistsActivePeriodAsync(cancellationToken))
            {
                throw;
            }

            return;
        }

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
                // Reset before each iteration so the previous group's override
                // never outlives its group: what the next commit stamps is
                // decided here, not by whatever ran last.
                _tenantProvider.ClearTenantOverride();
                if (!string.IsNullOrEmpty(tenantGroup.Key))
                {
                    _tenantProvider.SetTenantOverride(tenantGroup.Key);
                }

                // A company that has closed its door has no next period: its last one is closed here
                // like any other, and the wind-down sweep closes whatever an InProgress order opens
                // after that (ADR-0064 D2).
                var company = string.IsNullOrEmpty(tenantGroup.Key)
                    ? null
                    : await _tenantRepository.GetByIdAsync(tenantGroup.Key, cancellationToken);
                var openNext = company is not { IsDeactivated: true };

                foreach (var period in tenantGroup)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await ClosePeriodAsync(period, "Automatically closed by background job", openNext, cancellationToken);
                }
            }

            _logger.LogInformation("Pay period auto-close job completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in pay period auto-close job");
            throw;
        }
    }

    public async Task ClosePeriodAsync(PayPeriod period, string closeNote, bool openNext, CancellationToken cancellationToken)
    {
        period.Close("System", closeNote);
        _logger.LogInformation(
            "Closed pay period {PeriodId} ({StartDate} - {EndDate})",
            period.Id,
            period.StartDate.ToString("yyyy-MM-dd"),
            period.EndDate.ToString("yyyy-MM-dd"));

        if (openNext)
        {
            // Within the current tenant — check if any open period exists. The close above is not
            // flushed yet, so the store still reads this period as Open and it has to be excluded by id
            // or no successor ever opens.
            var hasActivePeriod = await _payPeriodRepository
                .GetQueryable()
                .AnyAsync(p => p.Status == PayPeriodStatus.Open && p.Id != period.Id, cancellationToken);

            if (!hasActivePeriod)
            {
                var newPeriod = PayPeriod.CreateBiWeekly(period.EndDate.AddDays(1), _payPeriodClosingConfig.LengthInDays);
                _payPeriodRepository.Add(newPeriod);

                _logger.LogInformation(
                    "Created new pay period {PeriodId} ({StartDate} - {EndDate})",
                    newPeriod.Id,
                    newPeriod.StartDate.ToString("yyyy-MM-dd"),
                    newPeriod.EndDate.ToString("yyyy-MM-dd"));
            }
        }

        // Durable before the first invoice, so pay calculated from here on lands in the successor instead
        // of behind this period's invoice. A cleaner this run does not reach is left to the
        // reconciliation sweep, which invoices a closed period's un-invoiced pay.
        await _unitOfWork.CommitAsync(cancellationToken);

        await SendPeriodClosedEmailsAsync(period, cancellationToken);
    }

    private async Task SendPeriodClosedEmailsAsync(PayPeriod period, CancellationToken cancellationToken)
    {
        var employeeIds = await _orderEmployeePayRepository.GetQueryable()
            .Where(p => p.PayPeriodId == period.Id && p.EmployeeInvoiceId == null)
            .Select(p => p.EmployeeId)
            .Distinct()
            .OrderBy(id => id)
            .ToListAsync(cancellationToken);
        var periodId = period.Id;
        var startDate = period.StartDate;
        var endDate = period.EndDate;
        var closedAt = period.ClosedAt!.Value;
        var label = period.GetPeriodLabel();
        var tenantId = period.TenantId;

        foreach (var employeeId in employeeIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var invoiceFailed = false;
            try
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                if (tenantId is not null)
                    scope.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantOverride(tenantId);
                await scope.ServiceProvider.GetRequiredService<PayPeriodBackgroundService>()
                    .SendEmployeePeriodClosedEmailAsync(employeeId, periodId, startDate, endDate, closedAt, label, true, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate invoices for employee {EmployeeId} / period {PeriodId}; sending a plain email", employeeId, periodId);
                invoiceFailed = true;
            }

            if (!invoiceFailed) continue;
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                if (tenantId is not null)
                    scope.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantOverride(tenantId);
                await scope.ServiceProvider.GetRequiredService<PayPeriodBackgroundService>()
                    .SendEmployeePeriodClosedEmailAsync(employeeId, periodId, startDate, endDate, closedAt, label, false, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send plain period closed email to employee {EmployeeId} / period {PeriodId}", employeeId, periodId);
            }
        }
    }

    private async Task SendEmployeePeriodClosedEmailAsync(string employeeId, string periodId,
        DateOnly startDate, DateOnly endDate, DateTime closedAt, string periodLabel, bool generateInvoices,
        CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetQueryable()
            .Include(e => e.User)
            .Include(e => e.Address).ThenInclude(a => a!.Country)
            .FirstOrDefaultAsync(e => e.Id == employeeId && e.IsActive, cancellationToken);
        if (employee?.User is not { IsActive: true } || string.IsNullOrWhiteSpace(employee.User.Email))
        {
            _logger.LogWarning("Employee {EmployeeId} has no active user or email; skipping period notification", employeeId);
            return;
        }

        var languageCode = employee.User.PreferredLanguageCode ?? Constants.Language.English;
        var employeeName = $"{employee.User.FirstName} {employee.User.LastName}";
        IReadOnlyList<(byte[] PdfBytes, string FileName)> attachments = generateInvoices
            ? await GenerateInvoicesForEmployeeAsync(employee, periodId, startDate, endDate, periodLabel, languageCode, cancellationToken)
            : [];
        List<(byte[]? PdfBytes, string? FileName)> sends = attachments.Count == 0
            ? [(null, null)]
            : attachments.Select(a => ((byte[]?)a.PdfBytes, (string?)a.FileName)).ToList();
        try
        {
            foreach (var (bytes, fileName) in sends)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _emailService.SendPeriodClosedEmailAsync(employee.User.Email, employeeName,
                    startDate, endDate, closedAt, periodLabel, languageCode, bytes, fileName, cancellationToken);
                _logger.LogInformation("Sent period closed email to employee {EmployeeId} for period {PeriodId}", employeeId, periodId);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send period closed email to employee {EmployeeId} / period {PeriodId}", employeeId, periodId);
        }
    }

    /// <summary>
    /// ONE INVOICE PER CURRENCY the employee's unassigned pay holds in this period. A cleaner can work
    /// an order in each, and a tax document is in one unit, so a mixed period yields one document per
    /// currency rather than a refusal -- which had no admin action to resolve it and stranded the rows
    /// forever. Returns one rendered attachment per invoice that rendered; an invoice whose render
    /// failed is still written (its error stamped for the admin) and simply has no attachment.
    /// </summary>
    private async Task<IReadOnlyList<(byte[] PdfBytes, string FileName)>> GenerateInvoicesForEmployeeAsync(
        Employee employee,
        string periodId,
        DateOnly startDate, DateOnly endDate, string periodLabel,
        string languageCode,
        CancellationToken cancellationToken)
    {
        var orderPays = await _orderEmployeePayRepository.GetUnassignedForEmployeePeriodAsync(
            employee.Id, periodId, cancellationToken);

        if (!orderPays.Any())
        {
            return [];
        }

        // Per currency: a currency this pair already holds an invoice for is skipped (a late pay row
        // must not grow a second document), the others proceed. The old check was per pair and would
        // have refused the second currency forever.
        var alreadyInvoiced = await _employeeInvoiceRepository
            .GetQueryable()
            .Where(i => i.EmployeeId == employee.Id && i.PayPeriodId == periodId)
            .Select(i => i.CurrencyId)
            .ToListAsync(cancellationToken);

        var attachments = new List<(byte[] PdfBytes, string FileName)>();

        foreach (var group in orderPays.GroupBy(p => p.CurrencyId).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            if (alreadyInvoiced.Contains(group.Key))
            {
                _logger.LogWarning(
                    "Invoice already exists for employee {EmployeeId} / period {PeriodId} in currency {CurrencyId}, skipping that currency",
                    employee.Id,
                    periodId,
                    group.Key);
                continue;
            }

            var attachment = await GenerateOneInvoiceAsync(
                employee, periodId, startDate, endDate, periodLabel, group.Key, group.ToList(), languageCode, cancellationToken);
            if (attachment is not null)
            {
                attachments.Add(attachment.Value);
            }
        }

        return attachments;
    }

    /// <summary>One currency's rows into one invoice: reference, row, PDF, upload -- each step durable before the next.</summary>
    private async Task<(byte[] PdfBytes, string FileName)?> GenerateOneInvoiceAsync(
        Employee employee,
        string periodId,
        DateOnly startDate, DateOnly endDate, string periodLabel,
        string currencyId,
        IReadOnlyList<OrderEmployeePay> orderPays,
        string languageCode,
        CancellationToken cancellationToken)
    {
        // THE CURRENCY COMES FROM THE PAY ROWS. It used to come from Employee.PreferredCurrencyCode --
        // a field with no writer anywhere in the platform, so always null, so this always fell through
        // to the platform default -- while GenerateInvoice derived the same document's currency from the
        // work country's configuration. Two answers for one tax document, neither reading the rows being
        // invoiced, and whichever path ran first decided. Both are deleted.
        var currency = await _currencyRepository.GetByIdAsync(currencyId, cancellationToken);
        if (currency is null)
        {
            _logger.LogError(
                "Employee {EmployeeId}'s pay for period {PeriodId} names currency {CurrencyId}, which "
                    + "does not exist; skipping that currency's invoice",
                employee.Id,
                periodId,
                currencyId);
            return null;
        }

        var variableSymbol = await _payoutReferenceAllocator.AllocateAsync(cancellationToken);
        if (variableSymbol.IsFailure)
        {
            _logger.LogError(
                "Could not allocate a payout reference for employee {EmployeeId} / period {PeriodId} ({Error}); skipping this employee's invoice",
                employee.Id,
                periodId,
                variableSymbol.Error?.Message);
            return null;
        }

        var invoiceNumber = await _payoutReferenceAllocator.AllocateInvoiceNumberAsync(cancellationToken);
        if (invoiceNumber.IsFailure)
        {
            _logger.LogError(
                "Could not allocate an invoice number for employee {EmployeeId} / period {PeriodId} ({Error}); skipping this employee's invoice",
                employee.Id,
                periodId,
                invoiceNumber.Error?.Message);
            return null;
        }

        var invoice = EmployeeInvoice.CreateFromOrderPays(
            employee.Id,
            periodId,
            orderPays,
            variableSymbol.Value!,
            invoiceNumber.Value!);

        _employeeInvoiceRepository.Add(invoice);

        foreach (var orderPay in orderPays)
        {
            orderPay.AssignToInvoice(invoice.Id);
        }

        var cashHeld = await _cashLedgerRepository.GetHeldUnderLockAsync(employee.Id, currencyId, cancellationToken);
        if (invoice.SetOffCash(cashHeld) > 0m)
        {
            _cashLedgerRepository.Add(CashLedgerEntry.ForSetOff(invoice));
        }

        // Make the reference durable before a document can carry it.
        await _unitOfWork.CommitAsync(cancellationToken);

        var language = await _languageRepository.GetByCodeAsync(languageCode, cancellationToken) ??
                       await _languageRepository.GetByCodeAsync(Constants.Language.English, cancellationToken);

        if (language == null)
        {
            _logger.LogError("No language found for code {LanguageCode} or fallback 'en'", languageCode);
            return null;
        }

        byte[]? pdfBytes;
        string pdfBlobUrl;
        try
        {
            pdfBytes = await GenerateInvoicePdfAsync(invoice, employee, currency, orderPays, startDate, endDate, language.Code, cancellationToken);
            if (pdfBytes is null || pdfBytes.Length == 0)
                throw new InvalidOperationException(EmptyRenderMessage);
            pdfBlobUrl = await UploadInvoicePdfAsync(invoice, employee, periodLabel, pdfBytes, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to generate PDF for invoice {InvoiceId}", invoice.Id);
            invoice.SetPdfGenerationError(ex.Message);
            await _unitOfWork.CommitAsync(cancellationToken);
            return null;
        }

        invoice.SetPdfBlobUrl(pdfBlobUrl);
        invoice.ClearPdfGenerationError();
        await _unitOfWork.CommitAsync(cancellationToken);
        return (pdfBytes, $"{invoice.InvoiceNumber}.pdf");
    }

    private async Task<byte[]?> GenerateInvoicePdfAsync(
        EmployeeInvoice invoice,
        Employee employee,
        Currency? currency,
        IReadOnlyList<OrderEmployeePay> orderPays,
        DateOnly startDate, DateOnly endDate,
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

        var pdfData = invoice.CreatePdfData(employee, currency, orderPays, countryContext, companyInfo, payoutDetails, dateFormat, languageCode)
            with { PayPeriodStart = startDate.ToString(dateFormat), PayPeriodEnd = endDate.ToString(dateFormat) };

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
        string periodLabel,
        byte[] pdfBytes,
        CancellationToken cancellationToken)
    {
        // THE EMPLOYEE'S ID, NOT THEIR NAME, and the same segment the regenerate path keys on so both
        // writers address one file. The name is mutable, and it is also the only segment that told two
        // companies' documents apart: each company numbers its own invoices, so two first invoices of a
        // year are both INV-YYYY-000001 under the same auto-seeded period label.
        var employeeFolder = employee.Id;
        var invoiceFileName = invoice.InvoiceNumber;

        var blobName = $"{periodLabel}/{employeeFolder}/{invoiceFileName}.pdf";
        var blobClient = _blobContainerClientFactory.GetBlobContainerClient(Common.Constants.BlobContainers.GeneratedInvoices);

        using var pdfStream = new MemoryStream(pdfBytes);
        await blobClient.UploadAsync(blobName, pdfStream, cancellationToken: cancellationToken);

        return blobClient.GetBlobUri(blobName).ToString();
    }
}
