using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Fiscal.Abstractions;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Models;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Services;

public sealed class ReceiptService(
    IPdfService pdfService,
    IOrderReceiptRepository receiptRepository,
    IFiscalCounterRepository fiscalCounterRepository,
    ILanguageRepository languageRepository,
    ICompanyInfoRepository companyInfoRepository,
    ICountryRepository countryRepository,
    ICountryConfigurationRepository countryConfigurationRepository,
    IBlobContainerClientFactory blobClientFactory,
    IFiscalServiceResolver fiscalServiceResolver,
    ILogger<ReceiptService> logger)
    : IReceiptService
{
    // ADR-0004 D-F4.1 phase 1 — RESERVE. Allocate the sequence + Create + Add the row and
    // mark it born-retry-eligible for any enforcement mode != None. Does NOT register with the
    // authority, does NOT generate the PDF, and does NOT commit — the handler owns the claim commit,
    // which MUST land BEFORE the irreversible external effects in RealizeFiscalAndPdfAsync.
    public async Task<OrderReceipt> ReserveReceiptAsync(Order order, string languageCode, CancellationToken cancellationToken = default)
    {
        var language = await languageRepository.GetByCodeAsync(languageCode, cancellationToken);
        if (language == null)
        {
            throw new InvalidOperationException(BusinessErrorMessage.LanguageNotFound);
        }

        // Try to get company info by customer's country, fallback to any active. Resolved here too so a
        // misconfiguration (no active company) fails BEFORE the claim is staged, not after.
        var countryId = order.CustomerAddress?.CountryId;
        var companyInfo = countryId != null
            ? await companyInfoRepository.GetActiveByCountryAsync(countryId, cancellationToken)
            : null;

        companyInfo ??= await companyInfoRepository.GetActiveCompanyInfoAsync(cancellationToken);

        if (companyInfo == null)
        {
            throw new InvalidOperationException(BusinessErrorMessage.CompanyInfoNotFound);
        }

        var enforcementMode = await ResolveEnforcementModeAsync(countryId, cancellationToken);

        // The number comes from the per-issuer gapless counter, NOT a COUNT(*) of receipts. The
        // allocation runs on the same connection/transaction the caller commits the claim in, so a
        // committed claim never holds a rolled-back number and a rolled-back claim returns its number
        // to the pool — a reserved-but-never-signed number stays a documented void, never re-issued.
        var currentYear = DateTime.UtcNow.Year;
        var providerKey = await ResolveProviderKeyAsync(countryId, enforcementMode, cancellationToken);
        var (counterYear, issuerScope) = FiscalSequenceScope.Resolve(providerKey, currentYear);
        var sequence = await fiscalCounterRepository.AllocateNextAsync(counterYear, issuerScope, cancellationToken);
        var receiptNumber = string.Format(Constants.ReceiptNumberFormat.Pattern, currentYear, sequence);

        var fileName = $"receipt_{order.DisplayOrderNumber}_{DateTime.UtcNow:yyyyMMdd}.pdf";
        var blobName = $"{currentYear}/{order.DisplayOrderNumber}/{fileName}";

        var receipt = OrderReceipt.Create(order.Id, receiptNumber, fileName, blobName, language.Id);

        // C-A — the claim is BORN RETRY-ELIGIBLE for any fiscal mode != None. A crash between the claim
        // commit and the register call leaves FiscalRegistrationFailed == false, FiscalCode == null;
        // setting FiscalNextRetryAt now (and widening GetDueForRetryAsync) makes that committed-but-
        // unregistered row sweepable by the retry job. SetFiscalData clears it on a later success.
        if (enforcementMode != FiscalEnforcementMode.None)
        {
            receipt.ScheduleImmediateFiscalRetry();
        }

        receiptRepository.Add(receipt);
        return receipt;
    }

    // ADR-0004 D-F4.1 phase 2 — REALIZE the external effects for an already-claimed receipt:
    // register with the authority (stamp on success / mark failed on failure), then generate + upload
    // the PDF. Called AFTER the claim commit, so a redelivery is already deduped by the committed row.
    public async Task RealizeFiscalAndPdfAsync(Order order, OrderReceipt receipt, string languageCode, CancellationToken cancellationToken = default)
    {
        var countryId = order.CustomerAddress?.CountryId;
        var companyInfo = countryId != null
            ? await companyInfoRepository.GetActiveByCountryAsync(countryId, cancellationToken)
            : null;

        companyInfo ??= await companyInfoRepository.GetActiveCompanyInfoAsync(cancellationToken);

        if (companyInfo == null)
        {
            throw new InvalidOperationException(BusinessErrorMessage.CompanyInfoNotFound);
        }

        var receiptData = CreateReceiptData(order, receipt.ReceiptNumber, companyInfo);

        // Resolve country ISO code + fiscal enforcement mode.
        string? countryCode = null;
        var enforcementMode = FiscalEnforcementMode.None;
        if (countryId != null)
        {
            var country = await countryRepository.GetByIdAsync(countryId, cancellationToken);
            countryCode = country?.IsoCode;

            var countryConfig = await countryConfigurationRepository.GetByCountryIdAsync(countryId, cancellationToken);
            if (countryConfig != null)
            {
                enforcementMode = countryConfig.FiscalEnforcementMode;
            }
        }

        // Mode-aware fiscal handling. For async/lenient modes, we try once and regardless
        // of outcome hand back to the caller so the customer flow continues. For blocking
        // modes we still try once; if the authority is unreachable the retry job will
        // regenerate the PDF and release the email later.
        await HandleFiscalAsync(order, receipt, companyInfo, countryCode, enforcementMode, cancellationToken);

        // Stamp the fiscal code into the PDF only when it was actually issued.
        if (receipt.FiscalCode != null)
        {
            receiptData.FiscalProviderKey = receipt.FiscalProviderKey;
            receiptData.FiscalCode = receipt.FiscalCode;
            receiptData.FiscalRegisteredAt = receipt.FiscalRegisteredAt?.ToString("d");
        }

        var pdfBytes = pdfService.GenerateReceiptPdf(receiptData, countryCode);

        var blobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.GeneratedReceipts);
        using var pdfStream = new MemoryStream(pdfBytes);
        await blobClient.UploadAsync(receipt.BlobName, pdfStream, cancellationToken: cancellationToken);
    }

    private async Task<FiscalEnforcementMode> ResolveEnforcementModeAsync(string? countryId, CancellationToken cancellationToken)
    {
        if (countryId == null)
        {
            return FiscalEnforcementMode.None;
        }

        var countryConfig = await countryConfigurationRepository.GetByCountryIdAsync(countryId, cancellationToken);
        return countryConfig?.FiscalEnforcementMode ?? FiscalEnforcementMode.None;
    }

    // The provider key identifies the fiscal regime, which decides the counter's issuer scope and
    // year-reset rule. With no fiscal system (None) there is no provider, so the empty key resolves to
    // the default annually-reset scope — matching CZ's current behaviour.
    private async Task<string> ResolveProviderKeyAsync(string? countryId, FiscalEnforcementMode enforcementMode, CancellationToken cancellationToken)
    {
        if (enforcementMode == FiscalEnforcementMode.None || countryId == null)
        {
            return string.Empty;
        }

        var country = await countryRepository.GetByIdAsync(countryId, cancellationToken);
        var isoCode = country?.IsoCode ?? "CZ";
        return fiscalServiceResolver.Resolve(isoCode).ProviderKey;
    }

    private async Task HandleFiscalAsync(
        Order order,
        OrderReceipt receipt,
        CompanyInfo companyInfo,
        string? countryCode,
        FiscalEnforcementMode enforcementMode,
        CancellationToken cancellationToken)
    {
        // None = no fiscal system in this country. Nothing to do.
        if (enforcementMode == FiscalEnforcementMode.None)
        {
            return;
        }

        var isoCode = countryCode ?? "CZ";
        var fiscalService = fiscalServiceResolver.Resolve(isoCode);

        FiscalGoLiveGate.EnsureRegisterIdempotent(fiscalService, enforcementMode);

        var fiscalRequest = BuildFiscalRequest(order, receipt, companyInfo, isoCode);

        try
        {
            var result = await fiscalService.RegisterReceiptAsync(fiscalRequest, cancellationToken);

            if (result.IsRegistered && result.FiscalCode != null)
            {
                receipt.SetFiscalData(
                    fiscalService.ProviderKey,
                    result.FiscalCode,
                    DateTime.TryParse(result.RegisteredAt, out var parsedAt) ? parsedAt : DateTime.UtcNow);
            }
            else if (result.IsRequired && !result.IsRegistered)
            {
                receipt.MarkFiscalRegistrationFailed(
                    fiscalService.ProviderKey,
                    result.ErrorKind,
                    $"{result.ErrorCode}: {result.ErrorMessage}");
                logger.LogError(
                    "Fiscal registration failed for ReceiptNumber={ReceiptNumber} Provider={ProviderKey} Mode={Mode} ErrorKind={ErrorKind} ErrorCode={ErrorCode} ErrorMessage={ErrorMessage}",
                    receipt.ReceiptNumber, fiscalService.ProviderKey, enforcementMode, result.ErrorKind, result.ErrorCode, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            // Never fail the receipt generation over a fiscal authority hiccup.
            // The receipt is marked as failed so the retry job can pick it up later.
            receipt.MarkFiscalRegistrationFailed(fiscalService.ProviderKey, FiscalErrorKind.Unknown, ex.Message);
            logger.LogError(ex,
                "Fiscal registration threw for ReceiptNumber={ReceiptNumber} Provider={ProviderKey} Mode={Mode}",
                receipt.ReceiptNumber, fiscalService.ProviderKey, enforcementMode);
        }
    }

    // The initial register and the recovery re-register MUST build the request the same way so they
    // carry the same explicit idempotency token (the receipt number) — that is what lets an idempotent
    // authority collapse a recovery re-register onto the prior entry instead of double-registering.
    private static FiscalReceiptRequest BuildFiscalRequest(Order order, OrderReceipt receipt, CompanyInfo companyInfo, string isoCode) =>
        FiscalReceiptRequest.Create(
            receiptNumber: receipt.ReceiptNumber,
            issuedAt: receipt.IssuedAt,
            totalAmount: order.TotalPrice,
            vatAmount: companyInfo.IsVatPayer && order.VatAmount > 0 ? order.VatAmount : null,
            currencyCode: order.Currency?.Code ?? Constants.Currency.Czk,
            companyLegalName: companyInfo.LegalName,
            companyRegistrationNumber: companyInfo.RegistrationNumber,
            companyVatNumber: companyInfo.VatNumber,
            customerName: order.CustomerName,
            customerEmail: order.CustomerEmail,
            lineItems: BuildFiscalLineItems(order, companyInfo.IsVatPayer ? order.AppliedVatRate : null),
            paymentMethod: order.PaymentType.ToString(),
            countryCode: isoCode);

    private static IReadOnlyList<FiscalLineItem> BuildFiscalLineItems(Order order, decimal? vatRate)
    {
        var items = new List<FiscalLineItem>();

        foreach (var s in order.SelectedServices)
        {
            var basePrice = s.Service?.BasePrice ?? 0;
            var perRoom = (s.Service?.PerRoomPrice ?? 0) * (order.Rooms + order.Bathrooms);
            items.Add(new FiscalLineItem(
                Description: s.Service?.Name ?? "Service",
                Quantity: 1,
                UnitPrice: basePrice + perRoom,
                VatRate: vatRate));
        }

        foreach (var p in order.SelectedPackages)
        {
            items.Add(new FiscalLineItem(
                Description: p.Package?.Name ?? "Package",
                Quantity: 1,
                UnitPrice: p.Package?.Price ?? 0,
                VatRate: vatRate));
        }

        return items;
    }

    public async Task<byte[]> DownloadReceiptPdfAsync(OrderReceipt receipt, CancellationToken cancellationToken = default)
    {
        var blobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.GeneratedReceipts);
        var blobDownload = await blobClient.DownloadAsync(receipt.BlobName, cancellationToken);

        using var memoryStream = new MemoryStream();
        await blobDownload.Content.CopyToAsync(memoryStream, cancellationToken);
        return memoryStream.ToArray();
    }

    public async Task<bool> RetryFiscalRegistrationAsync(OrderReceipt receipt, Order order, CancellationToken cancellationToken = default)
    {
        var countryId = order.CustomerAddress?.CountryId;
        var companyInfo = countryId != null
            ? await companyInfoRepository.GetActiveByCountryAsync(countryId, cancellationToken)
            : null;
        companyInfo ??= await companyInfoRepository.GetActiveCompanyInfoAsync(cancellationToken);
        if (companyInfo == null)
        {
            throw new InvalidOperationException(BusinessErrorMessage.CompanyInfoNotFound);
        }

        string? countryCode = null;
        if (countryId != null)
        {
            var country = await countryRepository.GetByIdAsync(countryId, cancellationToken);
            countryCode = country?.IsoCode;
        }

        var isoCode = countryCode ?? "CZ";
        var fiscalService = fiscalServiceResolver.Resolve(isoCode);

        var fiscalRequest = BuildFiscalRequest(order, receipt, companyInfo, isoCode);

        try
        {
            var result = await fiscalService.RegisterReceiptAsync(fiscalRequest, cancellationToken);

            if (result.IsRegistered && result.FiscalCode != null)
            {
                receipt.SetFiscalData(
                    fiscalService.ProviderKey,
                    result.FiscalCode,
                    DateTime.TryParse(result.RegisteredAt, out var parsedAt) ? parsedAt : DateTime.UtcNow);

                // Regenerate the PDF with the fiscal code and re-upload it.
                var receiptData = CreateReceiptData(order, receipt.ReceiptNumber, companyInfo);
                receiptData.FiscalProviderKey = receipt.FiscalProviderKey;
                receiptData.FiscalCode = receipt.FiscalCode;
                receiptData.FiscalRegisteredAt = receipt.FiscalRegisteredAt?.ToString("d");

                var pdfBytes = pdfService.GenerateReceiptPdf(receiptData, countryCode);
                var blobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.GeneratedReceipts);
                using var pdfStream = new MemoryStream(pdfBytes);
                await blobClient.UploadAsync(receipt.BlobName, pdfStream, cancellationToken: cancellationToken);

                logger.LogInformation(
                    "Fiscal retry succeeded for ReceiptNumber={ReceiptNumber} Provider={ProviderKey} Attempt={Attempt}",
                    receipt.ReceiptNumber, fiscalService.ProviderKey, receipt.FiscalRetryCount + 1);
                return true;
            }

            var errorKind = result.ErrorKind == FiscalErrorKind.None ? FiscalErrorKind.Unknown : result.ErrorKind;
            receipt.MarkFiscalRetryAttempted(
                errorKind,
                $"{result.ErrorCode}: {result.ErrorMessage}");
            logger.LogWarning(
                "Fiscal retry failed for ReceiptNumber={ReceiptNumber} Provider={ProviderKey} Attempt={Attempt} ErrorKind={ErrorKind} ErrorCode={ErrorCode}",
                receipt.ReceiptNumber, fiscalService.ProviderKey, receipt.FiscalRetryCount, errorKind, result.ErrorCode);
            return false;
        }
        catch (Exception ex)
        {
            receipt.MarkFiscalRetryAttempted(FiscalErrorKind.Unknown, ex.Message);
            logger.LogError(ex,
                "Fiscal retry threw for ReceiptNumber={ReceiptNumber} Provider={ProviderKey} Attempt={Attempt}",
                receipt.ReceiptNumber, fiscalService.ProviderKey, receipt.FiscalRetryCount);
            return false;
        }
    }

    private static ReceiptPdfData CreateReceiptData(Order order, string receiptNumber, CompanyInfo companyInfo)
    {
        return new ReceiptPdfData
        {
            ReceiptNumber = receiptNumber,
            OrderNumber = order.DisplayOrderNumber,
            IssuedDate = DateTime.UtcNow.ToString("d"),
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            CustomerPhone = order.CustomerPhone,
            CustomerAddress = $"{order.CustomerAddress?.Street}, {order.CustomerAddress?.City}, {order.CustomerAddress?.ZipCode}",
            Services = order.SelectedServices
                .Select(s =>
                {
                    var basePrice = s.Service?.BasePrice ?? 0;
                    var perRoom = (s.Service?.PerRoomPrice ?? 0) * (order.Rooms + order.Bathrooms);
                    return new ReceiptLineItem(s.Service?.Name ?? "Service", basePrice + perRoom);
                })
                .ToList(),
            Packages = order.SelectedPackages
                .Select(p => new ReceiptLineItem(p.Package?.Name ?? "Package", p.Package?.Price ?? 0))
                .ToList(),
            Extras = order.Extras
                .Where(e => e.Value)
                .Select(e => e.Key)
                .ToList(),
            Total = order.TotalPrice,
            Currency = order.Currency?.Symbol ?? "Kč",
            PaymentStatus = order.PaymentStatus.ToString(),
            PaymentType = order.PaymentType.ToString(),
            CleaningDate = order.CleaningDateTime.ToString("dd.MM.yyyy HH:mm"),
            Rooms = order.Rooms,
            Bathrooms = order.Bathrooms,
            EstimatedTime = order.EstimatedTime,
            IsVatPayer = companyInfo.IsVatPayer,
            NetAmount = companyInfo.IsVatPayer ? order.NetAmount : null,
            VatAmount = companyInfo.IsVatPayer ? order.VatAmount : null,
            VatRate = companyInfo.IsVatPayer ? order.AppliedVatRate : null,
            NonVatPayerNotice = companyInfo.IsVatPayer ? null : "Nejsme plátci DPH",
            Company = new CompanyInfoData
            {
                LegalName = companyInfo.LegalName,
                TradingName = companyInfo.TradingName,
                Tagline = companyInfo.Tagline,
                RegistrationNumber = companyInfo.RegistrationNumber,
                VatNumber = companyInfo.VatNumber,
                Street = companyInfo.Street,
                City = companyInfo.City,
                ZipCode = companyInfo.ZipCode,
                Address = companyInfo.GetFullAddress(),
                Phone = companyInfo.Phone,
                Email = companyInfo.Email,
                Website = companyInfo.Website,
                BankName = companyInfo.BankName,
                BankAccountNumber = companyInfo.BankAccountNumber,
                Iban = companyInfo.Iban,
                Swift = companyInfo.Swift,
                ContactInfo = companyInfo.GetFormattedContactInfo()
            }
        };
    }
}
