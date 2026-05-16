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
    ILanguageRepository languageRepository,
    ICompanyInfoRepository companyInfoRepository,
    ICountryRepository countryRepository,
    ICountryConfigurationRepository countryConfigurationRepository,
    IBlobContainerClientFactory blobClientFactory,
    IFiscalServiceResolver fiscalServiceResolver,
    ILogger<ReceiptService> logger)
    : IReceiptService
{
    public async Task<OrderReceipt> GenerateReceiptAsync(Order order, string languageCode, CancellationToken cancellationToken = default)
    {
        var language = await languageRepository.GetByCodeAsync(languageCode, cancellationToken);
        if (language == null)
        {
            throw new InvalidOperationException(BusinessErrorMessage.LanguageNotFound);
        }

        // Try to get company info by customer's country, fallback to any active
        var countryId = order.CustomerAddress?.CountryId;
        var companyInfo = countryId != null
            ? await companyInfoRepository.GetActiveByCountryAsync(countryId, cancellationToken)
            : null;

        companyInfo ??= await companyInfoRepository.GetActiveCompanyInfoAsync(cancellationToken);

        if (companyInfo == null)
        {
            throw new InvalidOperationException(BusinessErrorMessage.CompanyInfoNotFound);
        }

        var currentYear = DateTime.UtcNow.Year;
        var sequence = await receiptRepository.GetNextSequenceForYearAsync(currentYear, cancellationToken);
        var receiptNumber = string.Format(Constants.ReceiptNumberFormat.Pattern, currentYear, sequence);

        var fileName = $"receipt_{order.DisplayOrderNumber}_{DateTime.UtcNow:yyyyMMdd}.pdf";
        var blobName = $"{currentYear}/{order.DisplayOrderNumber}/{fileName}";

        var receipt = OrderReceipt.Create(order.Id, receiptNumber, fileName, blobName, language.Id);
        receiptRepository.Add(receipt);

        var receiptData = CreateReceiptData(order, receiptNumber, companyInfo);

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
        await blobClient.UploadAsync(blobName, pdfStream, cancellationToken: cancellationToken);

        return receipt;
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

        var fiscalRequest = new FiscalReceiptRequest(
            ReceiptNumber: receipt.ReceiptNumber,
            IssuedAt: receipt.IssuedAt,
            TotalAmount: order.TotalPrice,
            VatAmount: companyInfo.IsVatPayer && order.VatAmount > 0 ? order.VatAmount : null,
            CurrencyCode: order.Currency?.Code ?? Constants.Currency.Czk,
            CompanyLegalName: companyInfo.LegalName,
            CompanyRegistrationNumber: companyInfo.RegistrationNumber,
            CompanyVatNumber: companyInfo.VatNumber,
            CustomerName: order.CustomerName,
            CustomerEmail: order.CustomerEmail,
            LineItems: BuildFiscalLineItems(order, companyInfo.IsVatPayer ? order.AppliedVatRate : null),
            PaymentMethod: order.PaymentType.ToString(),
            CountryCode: isoCode);

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

        var fiscalRequest = new FiscalReceiptRequest(
            ReceiptNumber: receipt.ReceiptNumber,
            IssuedAt: receipt.IssuedAt,
            TotalAmount: order.TotalPrice,
            VatAmount: companyInfo.IsVatPayer && order.VatAmount > 0 ? order.VatAmount : null,
            CurrencyCode: order.Currency?.Code ?? Constants.Currency.Czk,
            CompanyLegalName: companyInfo.LegalName,
            CompanyRegistrationNumber: companyInfo.RegistrationNumber,
            CompanyVatNumber: companyInfo.VatNumber,
            CustomerName: order.CustomerName,
            CustomerEmail: order.CustomerEmail,
            LineItems: BuildFiscalLineItems(order, companyInfo.IsVatPayer ? order.AppliedVatRate : null),
            PaymentMethod: order.PaymentType.ToString(),
            CountryCode: isoCode);

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
