using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Models;

namespace Cleansia.Core.AppServices.Services;

public sealed class ReceiptService(
    IPdfService pdfService,
    IOrderReceiptRepository receiptRepository,
    ILanguageRepository languageRepository,
    ICompanyInfoRepository companyInfoRepository,
    ICountryRepository countryRepository,
    IBlobContainerClientFactory blobClientFactory)
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

        // Resolve country ISO code for layout builder
        string? countryCode = null;
        if (countryId != null)
        {
            var country = await countryRepository.GetByIdAsync(countryId, cancellationToken);
            countryCode = country?.IsoCode;
        }

        var pdfBytes = pdfService.GenerateReceiptPdf(receiptData, countryCode);

        var blobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.GeneratedReceipts);
        using var pdfStream = new MemoryStream(pdfBytes);
        await blobClient.UploadAsync(blobName, pdfStream, cancellationToken: cancellationToken);

        return receipt;
    }

    public async Task<byte[]> DownloadReceiptPdfAsync(OrderReceipt receipt, CancellationToken cancellationToken = default)
    {
        var blobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.GeneratedReceipts);
        var blobDownload = await blobClient.DownloadAsync(receipt.BlobName, cancellationToken);

        using var memoryStream = new MemoryStream();
        await blobDownload.Content.CopyToAsync(memoryStream, cancellationToken);
        return memoryStream.ToArray();
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
                .Select(s => new ReceiptLineItem(s.Service?.Name ?? "Service", s.Service?.BasePrice ?? 0))
                .ToList(),
            Packages = order.SelectedPackages
                .Select(p => new ReceiptLineItem(p.Package?.Name ?? "Package", p.Package?.Price ?? 0))
                .ToList(),
            Subtotal = order.TotalPrice,
            Total = order.TotalPrice,
            Currency = order.Currency?.Symbol ?? "€",
            PaymentStatus = order.PaymentStatus.ToString(),
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
