using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Fiscal.Abstractions;

namespace Cleansia.Core.Domain.Receipts;

public class OrderReceipt : Auditable, ITenantEntity
{
    [Required]
    [MaxLength(50)]
    public string ReceiptNumber { get; private set; } = default!;

    public string OrderId { get; private set; } = default!;
    public Order? Order { get; private set; }

    public DateTime IssuedAt { get; private set; }

    [MaxLength(100)]
    public string FileName { get; private set; } = default!;

    [MaxLength(100)]
    public string BlobName { get; private set; } = default!;

    public string LanguageId { get; private set; } = default!;
    public Language? Language { get; private set; }

    public bool EmailSent { get; private set; }
    public DateTime? EmailSentAt { get; private set; }

    [MaxLength(255)]
    public string? EmailMessageId { get; private set; }

    // Fiscal registration fields — populated by IFiscalService when a country has
    // a mandatory fiscal reporting system (e.g., CZ EET 2.0, SK eKasa, DE TSS).
    // Null for countries without a fiscal system (no-op path).

    [MaxLength(50)]
    public string? FiscalProviderKey { get; private set; }

    [MaxLength(255)]
    public string? FiscalCode { get; private set; }

    public DateTime? FiscalRegisteredAt { get; private set; }

    public bool FiscalRegistrationFailed { get; private set; }

    [MaxLength(1000)]
    public string? FiscalError { get; private set; }

    /// <summary>
    /// Classification of the last fiscal error, if any.
    /// Null = success or not required; set only when FiscalRegistrationFailed = true.
    /// </summary>
    public FiscalErrorKind? FiscalErrorKind { get; private set; }

    /// <summary>
    /// How many times the retry job has attempted fiscal registration after the initial failure.
    /// Starts at 0 (initial attempt during receipt generation).
    /// </summary>
    public int FiscalRetryCount { get; private set; }

    /// <summary>
    /// Timestamp of the most recent retry attempt (null if never retried after the initial attempt).
    /// </summary>
    public DateTime? FiscalLastRetryAt { get; private set; }

    /// <summary>
    /// Timestamp when the retry job should next pick up this receipt.
    /// Null = do not retry (permanent failure, already succeeded, or not required).
    /// </summary>
    public DateTime? FiscalNextRetryAt { get; private set; }

    /// <summary>
    /// True when an admin has explicitly acknowledged this fiscal failure and
    /// removed it from the action queue. Used to hide resolved/known-bad rows
    /// from the admin dashboard without deleting them.
    /// </summary>
    public bool FiscalAcknowledged { get; private set; }

    public DateTime? FiscalAcknowledgedAt { get; private set; }

    public static OrderReceipt Create(
        string orderId,
        string receiptNumber,
        string fileName,
        string blobName,
        string languageId)
    {
        return new OrderReceipt
        {
            OrderId = orderId,
            ReceiptNumber = receiptNumber,
            IssuedAt = DateTime.UtcNow,
            FileName = fileName,
            BlobName = blobName,
            LanguageId = languageId,
            EmailSent = false
        };
    }

    public void MarkEmailSent(string messageId)
    {
        EmailSent = true;
        EmailSentAt = DateTime.UtcNow;
        EmailMessageId = messageId;
    }

    public void AcknowledgeFiscalFailure()
    {
        FiscalAcknowledged = true;
        FiscalAcknowledgedAt = DateTime.UtcNow;
        FiscalNextRetryAt = null;
    }

    public void ScheduleImmediateFiscalRetry()
    {
        FiscalNextRetryAt = DateTime.UtcNow;
    }

    public void SetFiscalData(string providerKey, string fiscalCode, DateTime registeredAt)
    {
        FiscalProviderKey = providerKey;
        FiscalCode = fiscalCode;
        FiscalRegisteredAt = registeredAt;
        FiscalRegistrationFailed = false;
        FiscalError = null;
        FiscalErrorKind = null;
        FiscalNextRetryAt = null;
    }

    public void MarkFiscalRegistrationFailed(
        string providerKey,
        FiscalErrorKind errorKind,
        string error)
    {
        FiscalProviderKey = providerKey;
        FiscalRegistrationFailed = true;
        FiscalErrorKind = errorKind;
        FiscalError = error.Length > 1000 ? error[..1000] : error;

        // Schedule a retry only for transient / unknown errors.
        // Permanent / Configuration failures are not retried automatically.
        FiscalNextRetryAt = ShouldRetry(errorKind)
            ? ComputeNextRetry(FiscalRetryCount)
            : null;
    }

    public void MarkFiscalRetryAttempted(
        FiscalErrorKind errorKind,
        string error)
    {
        FiscalRetryCount++;
        FiscalLastRetryAt = DateTime.UtcNow;
        FiscalErrorKind = errorKind;
        FiscalError = error.Length > 1000 ? error[..1000] : error;

        // After MaxRetries attempts or a non-retryable error kind, stop retrying.
        if (FiscalRetryCount >= MaxFiscalRetries || !ShouldRetry(errorKind))
        {
            FiscalNextRetryAt = null;
        }
        else
        {
            FiscalNextRetryAt = ComputeNextRetry(FiscalRetryCount);
        }
    }

    private const int MaxFiscalRetries = 10;

    private static bool ShouldRetry(FiscalErrorKind errorKind) =>
        errorKind == Fiscal.Abstractions.FiscalErrorKind.Transient
        || errorKind == Fiscal.Abstractions.FiscalErrorKind.Unknown;

    private static DateTime ComputeNextRetry(int attemptNumber)
    {
        // Exponential backoff: 1m, 2m, 5m, 15m, 1h, 6h, 24h, 24h, 24h, 24h
        var delay = attemptNumber switch
        {
            0 => TimeSpan.FromMinutes(1),
            1 => TimeSpan.FromMinutes(2),
            2 => TimeSpan.FromMinutes(5),
            3 => TimeSpan.FromMinutes(15),
            4 => TimeSpan.FromHours(1),
            5 => TimeSpan.FromHours(6),
            _ => TimeSpan.FromHours(24),
        };
        return DateTime.UtcNow.Add(delay);
    }
}
