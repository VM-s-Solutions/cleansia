using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.CompanyLifecycle.Archive;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Services;

public sealed class CompanyArchiveService(
    ITenantRepository tenantRepository,
    ITenantProvider tenantProvider,
    IUnitOfWork unitOfWork,
    IOrderRepository orderRepository,
    IOrderEmployeePayRepository orderEmployeePayRepository,
    IOrderReceiptRepository orderReceiptRepository,
    IRefundRepository refundRepository,
    IDisputeRepository disputeRepository,
    IPayPeriodRepository payPeriodRepository,
    IEmployeeInvoiceRepository employeeInvoiceRepository,
    IEmployeeRepository employeeRepository,
    ICreditAccountRepository creditAccountRepository,
    IPromoCodeRepository promoCodeRepository,
    IPromoCodeRedemptionRepository promoCodeRedemptionRepository,
    ICompanyInfoRepository companyInfoRepository,
    IFiscalCounterRepository fiscalCounterRepository,
    IPayoutReferenceCounterRepository payoutReferenceCounterRepository,
    ITenantConfigurationRepository tenantConfigurationRepository,
    IAdminActionAuditRepository adminActionAuditRepository,
    IEmployeeActionAuditRepository employeeActionAuditRepository,
    IBlobContainerClientFactory blobClientFactory,
    ISchemaVersionReader schemaVersionReader,
    IAdminNotifier adminNotifier,
    TimeProvider timeProvider,
    ILogger<CompanyArchiveService> logger) : ICompanyArchiveService
{
    public const string AdrId = "ADR-0064";
    public const string ManifestFileName = "manifest.json";

    private const int PageSize = 500;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions ManifestJsonOptions = new(JsonOptions) { WriteIndented = true };

    private static readonly byte[] NewLine = "\n"u8.ToArray();

    public static string FolderOf(string tenantId, DateTimeOffset requestedOn) =>
        $"{tenantId}/{requestedOn.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}";

    public async Task<CompanyArchiveRunSummary> RunAsync(string tenantId, DateTimeOffset requestedOn, CancellationToken cancellationToken)
    {
        tenantProvider.SetTenantOverride(tenantId);

        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return Skip(tenantId, "the company does not exist");
        }

        if (tenant.IsArchived)
        {
            return Skip(tenantId, "the company is already archived");
        }

        if (tenant.ArchiveRequestedOn is not { } frozenOn)
        {
            return Skip(tenantId, "the company is not frozen for archive");
        }

        var folder = FolderOf(tenantId, frozenOn);
        if (folder != FolderOf(tenantId, requestedOn))
        {
            return Skip(tenantId, "the message names an archive request the company row no longer carries");
        }

        var archives = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.CompanyArchives);
        var files = new List<CompanyArchiveRecords.ManifestFile>();

        files.Add(await WriteAsync(archives, $"{folder}/books/orders.jsonl",
            orderRepository.GetQueryable().AsNoTracking()
                .Include(o => o.CustomerAddress)
                .Include(o => o.Receipt)
                .Include(o => o.SelectedExtras),
            ToRow, cancellationToken));
        files.Add(await WriteAsync(archives, $"{folder}/books/order-status-history.jsonl",
            orderRepository.GetQueryable().AsNoTracking().SelectMany(o => o.OrderStatusHistory),
            t => new CompanyArchiveRecords.OrderStatusTrack(t.Id, t.OrderId, t.Status, t.Sequence, t.CreatedOn),
            cancellationToken));
        files.Add(await WriteAsync(archives, $"{folder}/books/order-employee-pays.jsonl",
            orderEmployeePayRepository.GetQueryable().AsNoTracking(), ToRow, cancellationToken));
        files.Add(await WriteAsync(archives, $"{folder}/books/order-receipts.jsonl",
            orderReceiptRepository.GetQueryable().AsNoTracking(), ToRow, cancellationToken));
        files.Add(await WriteAsync(archives, $"{folder}/books/refunds.jsonl",
            refundRepository.GetQueryable().AsNoTracking(), ToRow, cancellationToken));
        files.Add(await WriteAsync(archives, $"{folder}/books/disputes.jsonl",
            disputeRepository.GetQueryable().AsNoTracking().Include(d => d.Lines), ToRow, cancellationToken));
        files.Add(await WriteAsync(archives, $"{folder}/books/pay-periods.jsonl",
            payPeriodRepository.GetQueryable().AsNoTracking(),
            p => new CompanyArchiveRecords.PayPeriod(p.Id, p.StartDate, p.EndDate, p.Status, p.ClosedAt, p.ClosedBy, p.PaidAt, p.CreatedOn),
            cancellationToken));
        files.Add(await WriteAsync(archives, $"{folder}/books/employee-invoices.jsonl",
            employeeInvoiceRepository.GetQueryable().AsNoTracking(), ToRow, cancellationToken));
        files.Add(await WriteAsync(archives, $"{folder}/books/employees.jsonl",
            employeeRepository.GetQueryable().AsNoTracking(),
            e => new CompanyArchiveRecords.Employee(e.Id, e.LegalEntityName, e.RegistrationNumber, e.WorkCountryId, e.ContractStatus),
            cancellationToken));
        files.Add(await WriteAsync(archives, $"{folder}/books/credit-accounts.jsonl",
            creditAccountRepository.GetQueryable().AsNoTracking(),
            a => new CompanyArchiveRecords.CreditAccount(a.Id, a.UserId, a.CurrencyId, a.Balance, a.ExpiresOn, a.CreatedOn),
            cancellationToken));
        files.Add(await WriteAsync(archives, $"{folder}/books/credit-transactions.jsonl",
            creditAccountRepository.GetQueryable().AsNoTracking().SelectMany(a => a.Transactions),
            t => new CompanyArchiveRecords.CreditTransaction(
                t.Id, t.CreditAccountId, t.Amount, t.Reason, t.OrderId, t.DisputeId, t.IdempotencyKey, t.CreatedBy, t.CreatedOn),
            cancellationToken));
        files.Add(await WriteAsync(archives, $"{folder}/books/promo-codes.jsonl",
            promoCodeRepository.GetQueryable().AsNoTracking(), ToRow, cancellationToken));
        files.Add(await WriteAsync(archives, $"{folder}/books/promo-code-redemptions.jsonl",
            promoCodeRedemptionRepository.GetQueryable().AsNoTracking(),
            r => new CompanyArchiveRecords.PromoCodeRedemption(r.Id, r.PromoCodeId, r.UserId, r.OrderId, r.AppliedDiscount, r.RedeemedOn, r.SlotOrdinal),
            cancellationToken));
        files.Add(await WriteAsync(archives, $"{folder}/books/company-info.jsonl",
            companyInfoRepository.GetQueryable().AsNoTracking(), ToRow, cancellationToken));
        files.Add(await WriteAsync(archives, $"{folder}/books/fiscal-counters.jsonl",
            fiscalCounterRepository.GetQueryable().AsNoTracking(),
            c => new CompanyArchiveRecords.FiscalCounter(c.Id, c.Year, c.IssuerScope, c.Value),
            cancellationToken));
        files.Add(await WriteAsync(archives, $"{folder}/books/payout-reference-counters.jsonl",
            payoutReferenceCounterRepository.GetQueryable().AsNoTracking(),
            c => new CompanyArchiveRecords.PayoutReferenceCounter(c.Id, c.Year, c.Scope, c.Value),
            cancellationToken));
        files.Add(await WriteAsync(archives, $"{folder}/books/tenant-configurations.jsonl",
            tenantConfigurationRepository.GetQueryable().AsNoTracking(),
            c => new CompanyArchiveRecords.TenantConfiguration(c.Key, c.Value, c.Category),
            cancellationToken));
        files.Add(await WriteAsync(archives, $"{folder}/audit/admin-action-audits.jsonl",
            adminActionAuditRepository.GetQueryable().AsNoTracking(), ToRow, cancellationToken));
        files.Add(await WriteAsync(archives, $"{folder}/audit/employee-action-audits.jsonl",
            employeeActionAuditRepository.GetQueryable().AsNoTracking(),
            a => new CompanyArchiveRecords.EmployeeActionAudit(a.Id, a.EmployeeId, a.OrderId, a.Action, a.CreatedBy, a.CreatedOn),
            cancellationToken));

        files.AddRange(await CopyReceiptPdfsAsync(archives, folder, cancellationToken));
        files.AddRange(await CopyInvoicePdfsAsync(archives, folder, cancellationToken));

        var companies = await companyInfoRepository.GetQueryable().AsNoTracking()
            .OrderBy(c => c.Id)
            .ToListAsync(cancellationToken);
        var builtOn = timeProvider.GetUtcNow();
        var manifest = new CompanyArchiveRecords.Manifest(
            tenant.Id,
            tenant.Name,
            AdrId,
            await schemaVersionReader.ReadAsync(cancellationToken),
            frozenOn,
            builtOn,
            companies.Select(ToRow).ToList(),
            files);
        var manifestSha256 = await SealAsync(archives, $"{folder}/{ManifestFileName}", JsonSerializer.SerializeToUtf8Bytes(manifest, ManifestJsonOptions), cancellationToken);
        tenant.MarkArchived(manifestSha256, builtOn);

        // The company is frozen, so this commit admits only the account surface — which is exactly
        // what the feed row and the outbox row are; a books write here would throw.
        await adminNotifier.NotifyAsync(
            new AdminEvent(
                AdminNotificationEventCatalog.CompanyArchived,
                tenant.Id,
                Subject: $"{tenant.Id}:{frozenOn.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}",
                Args: new Dictionary<string, string>
                {
                    ["archivedOn"] = builtOn.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                }),
            cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        // Error, not Information: the Functions host's Sentry integration drops Warning to a
        // breadcrumb, and a company's books being sealed is a thing a person must see.
        logger.LogError(
            "Company archive built for {TenantId} under {Folder}: {Files} files, manifest {ManifestSha256}",
            tenantId, folder, files.Count + 1, manifestSha256);

        return new CompanyArchiveRunSummary(true, null, folder, files.Count + 1, manifestSha256);
    }

    private CompanyArchiveRunSummary Skip(string tenantId, string reason)
    {
        logger.LogError("Company archive message for {TenantId} discarded: {Reason}", tenantId, reason);
        return CompanyArchiveRunSummary.Skipped(reason);
    }

    /// <summary>
    /// One JSON object per line, keyset-paged by id and streamed straight into the blob, hashed as it
    /// is written; a partial file from a run that died is overwritten whole on the re-run.
    /// </summary>
    private static async Task<CompanyArchiveRecords.ManifestFile> WriteAsync<TEntity, TRow>(
        IBlobContainerClient archives,
        string path,
        IQueryable<TEntity> source,
        Func<TEntity, TRow> project,
        CancellationToken cancellationToken)
        where TEntity : BaseEntity
    {
        var stream = await archives.CreateFileForWritingAsync(path, cancellationToken);
        await using (stream)
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long rows = 0;
            string? after = null;
            while (true)
            {
                var pageQuery = after is null ? source : source.Where(e => string.Compare(e.Id, after) > 0);
                var page = await pageQuery.OrderBy(e => e.Id).Take(PageSize).ToListAsync(cancellationToken);
                if (page.Count == 0)
                {
                    break;
                }

                foreach (var entity in page)
                {
                    var line = JsonSerializer.SerializeToUtf8Bytes(project(entity), JsonOptions);
                    await stream.WriteAsync(line, cancellationToken);
                    await stream.WriteAsync(NewLine, cancellationToken);
                    hash.AppendData(line);
                    hash.AppendData(NewLine);
                    rows++;
                }

                after = page[^1].Id;
                if (page.Count < PageSize)
                {
                    break;
                }
            }

            await stream.FlushAsync(cancellationToken);
            return new CompanyArchiveRecords.ManifestFile(path, rows, Hex(hash.GetHashAndReset()));
        }
    }

    private async Task<List<CompanyArchiveRecords.ManifestFile>> CopyReceiptPdfsAsync(
        IBlobContainerClient archives, string folder, CancellationToken cancellationToken)
    {
        var receipts = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.GeneratedReceipts);
        var files = new List<CompanyArchiveRecords.ManifestFile>();
        var rows = await orderReceiptRepository.GetQueryable().AsNoTracking()
            .OrderBy(r => r.Id)
            .Select(r => new { r.ReceiptNumber, r.BlobName })
            .ToListAsync(cancellationToken);
        foreach (var receipt in rows)
        {
            files.Add(await CopyAsync(archives, receipts.GetBlobUri(receipt.BlobName), $"{folder}/receipts/{receipt.ReceiptNumber}.pdf", cancellationToken));
        }

        return files;
    }

    private async Task<List<CompanyArchiveRecords.ManifestFile>> CopyInvoicePdfsAsync(
        IBlobContainerClient archives, string folder, CancellationToken cancellationToken)
    {
        var files = new List<CompanyArchiveRecords.ManifestFile>();
        var rows = await employeeInvoiceRepository.GetQueryable().AsNoTracking()
            .Where(i => i.PdfBlobUrl != null)
            .OrderBy(i => i.Id)
            .Select(i => new { i.InvoiceNumber, i.PdfBlobUrl })
            .ToListAsync(cancellationToken);
        foreach (var invoice in rows)
        {
            files.Add(await CopyAsync(archives, new Uri(invoice.PdfBlobUrl!), $"{folder}/payout-invoices/{invoice.InvoiceNumber}.pdf", cancellationToken));
        }

        return files;
    }

    /// <summary>The copy is server-side; the hash is of the bytes that landed, read back once.</summary>
    private static async Task<CompanyArchiveRecords.ManifestFile> CopyAsync(
        IBlobContainerClient archives, Uri source, string path, CancellationToken cancellationToken)
    {
        await archives.CopyAsync(source, path, cancellationToken);
        var copied = await archives.DownloadAsync(path, cancellationToken);
        await using (copied.Content)
        {
            return new CompanyArchiveRecords.ManifestFile(path, null, Hex(await SHA256.HashDataAsync(copied.Content, cancellationToken)));
        }
    }

    /// <summary>
    /// Two builds of one frozen company can overlap — a redelivery beside a "build again" — and while
    /// the files they write are the same bytes, their manifests differ by the build instant. The first
    /// manifest to land is the seal: a build that finds one already there stamps the row with that
    /// one's hash, never its own, so the row and the blob cannot disagree.
    /// </summary>
    private static async Task<string> SealAsync(IBlobContainerClient archives, string manifestPath, byte[] manifestBytes, CancellationToken cancellationToken)
    {
        using (var manifestStream = new MemoryStream(manifestBytes))
        {
            if (await archives.UploadIfAbsentAsync(manifestPath, manifestStream, cancellationToken))
            {
                return Hex(SHA256.HashData(manifestBytes));
            }
        }

        var landed = await archives.DownloadAsync(manifestPath, cancellationToken);
        await using (landed.Content)
        {
            return Hex(await SHA256.HashDataAsync(landed.Content, cancellationToken));
        }
    }

    private static string Hex(byte[] hash) => Convert.ToHexString(hash).ToLowerInvariant();

    private static CompanyArchiveRecords.Order ToRow(Domain.Orders.Order o) => new(
        o.Id,
        o.DisplayOrderNumber,
        o.CustomerAddress?.CountryId ?? string.Empty,
        o.CustomerAddress?.City ?? string.Empty,
        o.Rooms,
        o.Bathrooms,
        o.CleaningDateTime,
        o.PaymentType,
        o.PaymentStatus,
        o.CashCollectedAt,
        o.CollectedByEmployeeId,
        o.TotalPrice,
        o.NetAmount,
        o.VatAmount,
        o.AppliedVatRate,
        o.CurrencyId,
        o.CreditAppliedAmount,
        o.TierDiscountAmount,
        o.TierAtPurchase,
        o.PromoDiscountAmount,
        o.MembershipDiscountAmount,
        o.EstimatedTime,
        o.ActualCompletionTime,
        o.CompletedAt,
        o.EmployeePayCalculated,
        o.RequiredEmployees,
        o.MaxEmployees,
        o.CurrentStatus,
        o.CancelledAt,
        o.CancelledBy,
        o.CancellationReason,
        o.CancellationRefundAmount,
        o.CancellationFeeRate,
        string.IsNullOrEmpty(o.StripeSessionId) ? null : o.StripeSessionId,
        o.StripePaymentIntentId,
        o.ReceiptId,
        o.Receipt?.ReceiptNumber,
        o.SelectedExtras.Select(e => new CompanyArchiveRecords.OrderExtra(e.Id, e.ExtraId, e.Slug, e.UnitPrice)).ToList(),
        o.CreatedOn);

    private static CompanyArchiveRecords.OrderEmployeePay ToRow(Domain.EmployeePayroll.OrderEmployeePay p) => new(
        p.Id, p.OrderId, p.EmployeeId, p.PayPeriodId, p.CurrencyId,
        p.BasePay, p.ExtrasPay, p.ExpensesPay, p.BonusPay, p.DeductionPay, p.MinPay, p.MaxPay, p.TotalPay, p.PayBreakdown,
        p.IsApproved, p.ApprovedAt, p.ApprovedBy, p.EmployeeInvoiceId, p.CreatedOn);

    private static CompanyArchiveRecords.OrderReceipt ToRow(Domain.Receipts.OrderReceipt r) => new(
        r.Id, r.ReceiptNumber, r.OrderId, r.IssuedAt, r.FileName, r.BlobName, r.LanguageId,
        r.FiscalProviderKey, r.FiscalCode, r.FiscalRegisteredAt, r.FiscalRegistrationFailed, r.FiscalErrorKind, r.FiscalRetryCount,
        r.FiscalAcknowledged, r.FiscalAcknowledgedAt, r.CreatedOn);

    private static CompanyArchiveRecords.Refund ToRow(Domain.Payments.Refund r) => new(
        r.Id, r.OrderId, r.ReceiptId, r.DisputeId, r.Amount, r.Currency, r.RefundKey, r.Reason, r.StripeRefundId,
        r.Source, r.Status, r.ConfirmedOn, r.CreatedOn);

    private static CompanyArchiveRecords.Dispute ToRow(Domain.Disputes.Dispute d) => new(
        d.Id, d.OrderId, d.Reason, d.Status, d.RefundAmount, d.ResolvedBy, d.ResolvedOn, d.StripeDisputeId, d.TextRetainedUntil,
        d.Lines.Select(l => new CompanyArchiveRecords.DisputeLine(l.Id, l.ServiceId, l.PackageId)).ToList(),
        d.CreatedOn);

    private static CompanyArchiveRecords.EmployeeInvoice ToRow(Domain.EmployeePayroll.EmployeeInvoice i) => new(
        i.Id, i.EmployeeId, i.PayPeriodId, i.InvoiceNumber, i.TotalOrders, i.SubTotal, i.BonusAmount, i.DeductionAmount, i.TotalAmount,
        i.CurrencyId, i.Status, i.CountryId, i.LanguageId, i.GeneratedAt, i.ApprovedAt, i.ApprovedBy, i.PaidAt,
        i.VariableSymbol, i.SpecificSymbol, i.PaymentReference, i.IsCancelled, i.CancellationReason, i.CancelledAt, i.CancelledBy, i.CreatedOn);

    private static CompanyArchiveRecords.PromoCode ToRow(Domain.Loyalty.PromoCode p) => new(
        p.Id, p.Code, p.Type, p.DiscountPercent, p.DiscountAmount, p.CurrencyId, p.MinimumOrderAmount, p.MaxRedemptionsPerUser,
        p.GlobalMaxRedemptions, p.CurrentRedemptionsCount, p.ValidFrom, p.ValidUntil, p.IsActive, p.CreatedOn);

    private static CompanyArchiveRecords.CompanyInfo ToRow(Domain.Company.CompanyInfo c) => new(
        c.Id, c.LegalName, c.TradingName, c.RegistrationNumber, c.VatNumber, c.IsVatPayer, c.VatRegisteredFrom,
        c.City, c.ZipCode, c.CountryId, c.Website);

    private static CompanyArchiveRecords.AdminActionAudit ToRow(Domain.Auditing.AdminActionAudit a) => new(
        a.Id, a.ActorId, a.ActorProfile, a.Action, a.ResourceType, a.ResourceId, a.Success, a.ErrorCode, a.OccurredOn,
        a.Reason, a.BeforeJson, a.AfterJson, a.CorrelationId);
}
