using System.Globalization;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Internationalization;
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
    public async Task RealizeFiscalAndPdfAsync(Order order, OrderReceipt receipt, CancellationToken cancellationToken = default)
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

        // Resolve country ISO code + fiscal enforcement mode.
        string? countryCode = null;
        var enforcementMode = FiscalEnforcementMode.None;
        string? timeZoneId = null;
        if (countryId != null)
        {
            var country = await countryRepository.GetByIdAsync(countryId, cancellationToken);
            countryCode = country?.IsoCode;

            var countryConfig = await countryConfigurationRepository.GetByCountryIdAsync(countryId, cancellationToken);
            if (countryConfig != null)
            {
                enforcementMode = countryConfig.FiscalEnforcementMode;
                timeZoneId = countryConfig.TimeZoneId;
            }
        }

        var receiptData = CreateReceiptData(
            order,
            receipt,
            companyInfo,
            await ResolveDocumentLanguageAsync(receipt, cancellationToken),
            TimeZoneResolution.Resolve(timeZoneId));

        // Mode-aware fiscal handling. For async/lenient modes, we try once and regardless
        // of outcome hand back to the caller so the customer flow continues. For blocking
        // modes we still try once; if the authority is unreachable the retry job will
        // regenerate the PDF and release the email later.
        await HandleFiscalAsync(order, receipt, companyInfo, countryCode, enforcementMode, cancellationToken);

        // Stamp the fiscal code into the PDF only when it was actually issued.
        StampFiscalData(receiptData, receipt);

        await UploadAsync(receipt, pdfService.GenerateReceiptPdf(receiptData, countryCode), cancellationToken);
    }

    /// <summary>
    /// Re-renders an already-issued receipt from the order as it stands now, over the same number, the
    /// same blob and the same language. The fiscal registration is deliberately not re-attempted: the
    /// sale was registered (or is on the retry job's list) under this number already, and a second
    /// register is what the claim-first ordering in ADR-0004 exists to prevent.
    /// </summary>
    public async Task RegenerateReceiptPdfAsync(Order order, OrderReceipt receipt, CancellationToken cancellationToken = default)
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

        var receiptData = CreateReceiptData(
            order,
            receipt,
            companyInfo,
            await ResolveDocumentLanguageAsync(receipt, cancellationToken),
            await MarketZoneAsync(countryId, cancellationToken));

        StampFiscalData(receiptData, receipt);

        string? countryCode = null;
        if (countryId != null)
        {
            countryCode = (await countryRepository.GetByIdAsync(countryId, cancellationToken))?.IsoCode;
        }

        // Rendered BEFORE the writer opens. Opening it re-creates the blob empty, so a render that threw
        // after that point would leave the customer's only copy of the receipt at zero bytes.
        var pdf = pdfService.GenerateReceiptPdf(receiptData, countryCode);

        // The blob already exists: UploadAsync creates only, and would refuse this write on every attempt.
        var blobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.GeneratedReceipts);
        await using var stored = await blobClient.CreateFileForWritingAsync(receipt.BlobName, cancellationToken);
        await stored.WriteAsync(pdf, cancellationToken);
    }

    private async Task UploadAsync(OrderReceipt receipt, byte[] pdfBytes, CancellationToken cancellationToken)
    {
        var blobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.GeneratedReceipts);
        using var pdfStream = new MemoryStream(pdfBytes);
        await blobClient.UploadAsync(receipt.BlobName, pdfStream, cancellationToken: cancellationToken);
    }

    private static void StampFiscalData(ReceiptPdfData receiptData, OrderReceipt receipt)
    {
        if (receipt.FiscalCode == null)
        {
            return;
        }

        receiptData.FiscalProviderKey = receipt.FiscalProviderKey;
        receiptData.FiscalCode = receipt.FiscalCode;
        receiptData.FiscalRegisteredAt = receipt.FiscalRegisteredAt?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Read from the receipt row, not from the current caller: a re-render reproduces the document the
    /// customer was issued, it does not re-decide it.
    /// </summary>
    private async Task<string> ResolveDocumentLanguageAsync(OrderReceipt receipt, CancellationToken cancellationToken)
    {
        var language = await languageRepository.GetByIdAsync(receipt.LanguageId, cancellationToken);
        return language?.Code ?? Constants.Language.English;
    }

    /// <summary>The market's own clock, so a receipt issued just after local midnight carries the local day.</summary>
    private async Task<TimeZoneInfo> MarketZoneAsync(string? countryId, CancellationToken cancellationToken) =>
        countryId is null
            ? TimeZoneInfo.Utc
            : TimeZoneResolution.Resolve(
                (await countryConfigurationRepository.GetByCountryIdAsync(countryId, cancellationToken))?.TimeZoneId);

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
    // FiscalSequenceScope.DefaultIssuerScope, which never resets annually (NoAnnualResetYear).
    private async Task<string> ResolveProviderKeyAsync(string? countryId, FiscalEnforcementMode enforcementMode, CancellationToken cancellationToken)
    {
        if (enforcementMode == FiscalEnforcementMode.None || countryId == null)
        {
            return string.Empty;
        }

        var country = await countryRepository.GetByIdAsync(countryId, cancellationToken);
        // No ISO code, no regime: the empty key resolves to the default scope, never to the Czech one.
        var isoCode = ToFiscalCountryCode(country?.IsoCode);
        return isoCode is null ? string.Empty : fiscalServiceResolver.Resolve(isoCode).ProviderKey;
    }

    /// <summary>
    /// Every <see cref="IFiscalService"/> declares an ISO 3166-1 alpha-2 code and the resolver keys on
    /// it, while <c>Country.IsoCode</c> is stored alpha-3 by the seed ("CZE"). Resolving on the stored
    /// code matched no provider in any market, so every receipt fell through to the no-op regardless of
    /// the enforcement mode. Unmapped resolves to null, which lands on the same no-op — fail-closed,
    /// never a guess at which authority a receipt belongs to.
    /// </summary>
    private static string? ToFiscalCountryCode(string? storedIsoCode) =>
        CountryIsoCode.ToAlpha2(storedIsoCode);

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

        var fiscalCountryCode = ToFiscalCountryCode(countryCode);

        // An unresolved country resolves to the no-op provider, and the request below refuses to be
        // built for it -- the same fail-closed landing as a missing currency, never the Czech regime.
        var fiscalService = fiscalServiceResolver.Resolve(fiscalCountryCode ?? string.Empty);

        FiscalGoLiveGate.EnsureRegisterIdempotent(fiscalService, enforcementMode);

        try
        {
            var fiscalRequest = BuildFiscalRequest(order, receipt, companyInfo, FiscalCountryCodeOf(order, fiscalCountryCode));
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
            // Never fail the receipt generation over a fiscal authority hiccup, and never over a
            // request the platform refused to build (an order without its currency lands here too).
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
    /// <summary>
    /// Whether VAT was applied to THIS ORDER, read from the order's own frozen breakdown.
    ///
    /// <para><b>Deliberately not <c>companyInfo.IsVatPayer</c>.</b> That is live company state, and a
    /// receipt is a statement about a sale that already happened. Reading it live failed in both
    /// directions: before registration a re-rendered order could claim VAT it never charged, and after
    /// registration a pre-registration order re-rendered through <c>RetryFiscalRegistrationAsync</c> —
    /// which re-resolves company info and re-uploads the stored blob — produced a document with
    /// neither a VAT line (the layout needs <c>VatAmount &gt; 0</c>) nor the statutory non-payer notice
    /// (the layout needs <c>!IsVatPayer</c>). A tax document asserting neither posture.</para>
    ///
    /// <para><c>AppliedVatRate</c> is the right discriminator because it is written exactly once, at
    /// order creation, and is null precisely when no VAT regime applied. <c>VatCalculator</c> now
    /// throws rather than returning a silent zero for a VAT payer with no country configuration, so
    /// null no longer doubles as "we could not tell".</para>
    /// </summary>
    private static bool VatApplied(Order order) => order.AppliedVatRate is not null;

    private static FiscalReceiptRequest BuildFiscalRequest(Order order, OrderReceipt receipt, CompanyInfo companyInfo, string isoCode) =>
        FiscalReceiptRequest.Create(
            receiptNumber: receipt.ReceiptNumber,
            issuedAt: receipt.IssuedAt,
            totalAmount: order.TotalPrice,
            vatAmount: VatApplied(order) && order.VatAmount > 0 ? order.VatAmount : null,
            currencyCode: FiscalCurrencyCodeOf(order),
            companyLegalName: companyInfo.LegalName,
            companyRegistrationNumber: companyInfo.RegistrationNumber,
            companyVatNumber: companyInfo.VatNumber,
            customerName: order.CustomerName,
            customerEmail: order.CustomerEmail,
            lineItems: BuildFiscalLineItems(order, order.AppliedVatRate),
            // The tender actually taken, not the booked one — a card booking the cleaner settled in cash
            // must be registered with the fiscal authority as a cash sale.
            paymentMethod: order.ActualPaymentType.ToString(),
            countryCode: isoCode);

    // A declaration to a tax authority in a guessed unit is a false one, not a degraded one. An order
    // that reaches the register without its currency loaded is refused; the throw lands in the
    // caller's failure recording, never in a registered CZK receipt for a EUR sale.
    private static string FiscalCurrencyCodeOf(Order order) =>
        order.Currency?.Code
        ?? throw new InvalidOperationException(
            $"Order {order.Id} has no resolved currency; refusing to register its receipt in a default one");

    // The regime is the country's, and a receipt whose country could not be resolved used to be declared
    // to the Czech authority by default. Same refusal, same landing.
    private static string FiscalCountryCodeOf(Order order, string? countryCode) =>
        countryCode
        ?? throw new InvalidOperationException(
            $"Order {order.Id} has no resolved country; refusing to register its receipt under a default regime");

    private static IReadOnlyList<FiscalLineItem> BuildFiscalLineItems(Order order, decimal? vatRate)
    {
        var items = new List<FiscalLineItem>();

        // Prices from the ORDER'S snapshot, names from the catalogue. These figures are declared to a
        // tax authority, and they used to be recomputed from the live catalogue every time the receipt
        // was rendered — including by RetryFiscalRegistrationAsync, which re-renders an old receipt
        // against today's prices. The name is a label and may drift; the number may not.
        foreach (var s in order.SelectedServices)
        {
            items.Add(new FiscalLineItem(
                Description: s.Service?.Name ?? "Service",
                Quantity: 1,
                UnitPrice: s.LineTotal,
                VatRate: vatRate));
        }

        foreach (var p in order.SelectedPackages)
        {
            items.Add(new FiscalLineItem(
                Description: p.Package?.Name ?? "Package",
                Quantity: 1,
                UnitPrice: p.LineTotal,
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

        var fiscalCountryCode = ToFiscalCountryCode(countryCode);
        var fiscalService = fiscalServiceResolver.Resolve(fiscalCountryCode ?? string.Empty);

        try
        {
            var fiscalRequest = BuildFiscalRequest(order, receipt, companyInfo, FiscalCountryCodeOf(order, fiscalCountryCode));
            var result = await fiscalService.RegisterReceiptAsync(fiscalRequest, cancellationToken);

            if (result.IsRegistered && result.FiscalCode != null)
            {
                receipt.SetFiscalData(
                    fiscalService.ProviderKey,
                    result.FiscalCode,
                    DateTime.TryParse(result.RegisteredAt, out var parsedAt) ? parsedAt : DateTime.UtcNow);

                // Regenerate the PDF with the fiscal code and re-upload it.
                var receiptData = CreateReceiptData(
                    order,
                    receipt,
                    companyInfo,
                    await ResolveDocumentLanguageAsync(receipt, cancellationToken),
                    await MarketZoneAsync(countryId, cancellationToken));
                StampFiscalData(receiptData, receipt);

                await UploadAsync(receipt, pdfService.GenerateReceiptPdf(receiptData, countryCode), cancellationToken);

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

    /// <summary>
    /// A catalogue entry's name in the document's language: its own translation, then the English one,
    /// then the base name it was created with.
    /// </summary>
    private static string NameIn(
        string languageCode, IReadOnlyDictionary<string, Translation> translations, string baseName)
    {
        if (translations.TryGetValue(languageCode, out var requested) && !string.IsNullOrWhiteSpace(requested.Name))
        {
            return requested.Name;
        }

        if (translations.TryGetValue(Constants.Language.English, out var english) && !string.IsNullOrWhiteSpace(english.Name))
        {
            return english.Name;
        }

        return baseName;
    }

    private static ReceiptPdfData CreateReceiptData(
        Order order, OrderReceipt receipt, CompanyInfo companyInfo, string languageCode, TimeZoneInfo marketZone)
    {
        return new ReceiptPdfData
        {
            LanguageCode = languageCode,
            ReceiptNumber = receipt.ReceiptNumber,
            OrderNumber = order.DisplayOrderNumber,
            // The row's date, not the render's: a restated receipt keeps the date it was issued on.
            IssuedDate = TimeZoneInfo.ConvertTimeFromUtc(receipt.IssuedAt, marketZone)
                .ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            CustomerPhone = order.CustomerPhone,
            CustomerAddress = $"{order.CustomerAddress?.Street}, {order.CustomerAddress?.City}, {order.CustomerAddress?.ZipCode}",
            // Snapshot prices, catalogue names — see BuildFiscalLineItems. An unloaded catalogue row is a
            // loader omission, printed as the line's own id rather than an English word.
            Services = order.SelectedServices
                .Select(s => new ReceiptLineItem(
                    s.Service is { } service ? NameIn(languageCode, service.Translations, service.Name) : s.ServiceId,
                    s.LineTotal))
                .ToList(),
            Packages = order.SelectedPackages
                .Select(p => new ReceiptLineItem(
                    p.Package is { } package ? NameIn(languageCode, package.Translations, package.Name) : p.PackageId,
                    p.LineTotal))
                .ToList(),
            Extras = order.SelectedExtras
                .Select(e => new ReceiptLineItem(
                    e.Extra is { } extra ? NameIn(languageCode, extra.Translations, extra.Name) : e.Slug,
                    e.UnitPrice))
                .ToList(),
            // The surcharge was applied to the lines' sum and each discount came off the result, so the
            // stored discounts are measured against the charged price. → OrderFactory.DiscountResolution
            ExpressSurcharge = order.ExpressSurchargeAmount,
            TierDiscount = order.TierDiscountAmount ?? 0m,
            MembershipDiscount = order.MembershipDiscountAmount ?? 0m,
            PromoDiscount = order.PromoDiscountAmount ?? 0m,
            Total = order.TotalPrice,
            // The sale above, how it was settled below. -> ReceiptPdfData.CreditApplied
            CreditApplied = order.CreditAppliedAmount,
            AmountDueOnCard = order.AmountDueOnCard,
            // An unloaded Currency navigation is a loader omission, not a CZK order: no unit rather than a guessed one. → /architecture/platform-expandability#_5-where-czk-kc-is-hardcoded-vs-configurable
            Currency = order.Currency?.Symbol ?? string.Empty,
            // The VALUES, not their names: the layout picks the word in the document's language.
            PaymentStatus = order.PaymentStatus,
            PaymentType = order.ActualPaymentType,
            CleaningDate = TimeZoneInfo.ConvertTimeFromUtc(order.CleaningDateTime, marketZone)
                .ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture),
            Rooms = order.Rooms,
            Bathrooms = order.Bathrooms,
            EstimatedTime = order.EstimatedTime,
            // THE ORDER'S OWN SNAPSHOT, never the live company row. A receipt is a statement about a
            // sale that already happened, so its VAT posture is a property of that sale.
            IsVatPayer = VatApplied(order),
            NetAmount = VatApplied(order) ? order.NetAmount : null,
            VatAmount = VatApplied(order) ? order.VatAmount : null,
            VatRate = order.AppliedVatRate,
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
