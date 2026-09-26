using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Fiscal.Abstractions;

namespace Cleansia.Core.AppServices.Features.CompanyLifecycle.Archive;

/// <summary>
/// One row of each file in a company's archive bundle (ADR-0064 D3). A row carries what the
/// platform's own retention regime would leave on the live row after every window has run, plus the
/// ids the books need to cross-reference each other; identity lives in the receipt and invoice PDFs
/// beside them. A build-time guard walks every member here and fails on a name that could hold a
/// person, so a member is added by name, deliberately.
/// </summary>
public static class CompanyArchiveRecords
{
    public sealed record Order(
        string Id,
        string DisplayOrderNumber,
        string CountryId,
        string City,
        int Rooms,
        int Bathrooms,
        DateTime CleaningDateTime,
        PaymentType PaymentType,
        PaymentStatus PaymentStatus,
        DateTime? CashCollectedAt,
        string? CollectedByEmployeeId,
        decimal TotalPrice,
        decimal NetAmount,
        decimal VatAmount,
        decimal? AppliedVatRate,
        string CurrencyId,
        decimal CreditAppliedAmount,
        decimal ExpressSurchargeAmount,
        decimal? TierDiscountAmount,
        LoyaltyTier? TierAtPurchase,
        decimal? PromoDiscountAmount,
        decimal? MembershipDiscountAmount,
        int EstimatedTime,
        int? ActualCompletionTime,
        DateTime? CompletedAt,
        bool EmployeePayCalculated,
        int RequiredEmployees,
        int MaxEmployees,
        OrderStatus CurrentStatus,
        DateTime? CancelledAt,
        CancelledBy? CancelledBy,
        string? CancellationReason,
        decimal? CancellationRefundAmount,
        decimal? CancellationFeeRate,
        string? StripeSessionId,
        string? StripePaymentIntentId,
        string? ReceiptId,
        string? ReceiptNumber,
        string? WorkContractDocumentId,
        IReadOnlyList<OrderExtra> Extras,
        DateTimeOffset CreatedOn);

    public sealed record OrderExtra(string Id, string ExtraId, string Slug, decimal UnitPrice);

    /// <summary>
    /// The contract-for-work record without the request trio the retention sweep blanks: the act, the
    /// seat, the cleaner's id, the exact text row, the version as the human handle, the instant, the
    /// client and the job facts as shown at acceptance (ADR-0068 D5).
    /// </summary>
    public sealed record WorkContractAcceptance(
        string Id,
        string OrderId,
        string OrderEmployeeId,
        string EmployeeId,
        string LegalDocumentTextId,
        string DocumentVersion,
        DateTimeOffset AcceptedOn,
        string ClientAudience,
        string FactsJson);

    public sealed record OrderStatusTrack(string Id, string OrderId, OrderStatus Status, int Sequence, DateTimeOffset CreatedOn);

    public sealed record OrderEmployeePay(
        string Id,
        string OrderId,
        string EmployeeId,
        string PayPeriodId,
        string CurrencyId,
        decimal BasePay,
        decimal ExtrasPay,
        decimal ExpensesPay,
        decimal BonusPay,
        decimal DeductionPay,
        decimal MinPay,
        decimal MaxPay,
        decimal TotalPay,
        string? PayBreakdown,
        bool IsApproved,
        DateTime? ApprovedAt,
        string? ApprovedBy,
        string? EmployeeInvoiceId,
        DateTimeOffset CreatedOn);

    public sealed record OrderReceipt(
        string Id,
        string ReceiptNumber,
        string OrderId,
        DateTime IssuedAt,
        string FileName,
        string BlobName,
        string LanguageId,
        string? FiscalProviderKey,
        string? FiscalCode,
        DateTime? FiscalRegisteredAt,
        bool FiscalRegistrationFailed,
        FiscalErrorKind? FiscalErrorKind,
        int FiscalRetryCount,
        bool FiscalAcknowledged,
        DateTime? FiscalAcknowledgedAt,
        DateTimeOffset CreatedOn);

    public sealed record Refund(
        string Id,
        string OrderId,
        string? ReceiptId,
        string? DisputeId,
        decimal Amount,
        string Currency,
        string RefundKey,
        RefundReason Reason,
        string? StripeRefundId,
        RefundSource Source,
        RefundStatus Status,
        DateTimeOffset? ConfirmedOn,
        DateTimeOffset CreatedOn);

    public sealed record Dispute(
        string Id,
        string OrderId,
        DisputeReason Reason,
        DisputeStatus Status,
        decimal? RefundAmount,
        string? ResolvedBy,
        DateTimeOffset? ResolvedOn,
        string? StripeDisputeId,
        DateTimeOffset? TextRetainedUntil,
        IReadOnlyList<DisputeLine> Lines,
        DateTimeOffset CreatedOn);

    public sealed record DisputeLine(string Id, string ServiceId, string? PackageId);

    public sealed record PayPeriod(
        string Id,
        DateOnly StartDate,
        DateOnly EndDate,
        PayPeriodStatus Status,
        DateTime? ClosedAt,
        string? ClosedBy,
        DateTime? PaidAt,
        DateTimeOffset CreatedOn);

    public sealed record EmployeeInvoice(
        string Id,
        string EmployeeId,
        string PayPeriodId,
        string InvoiceNumber,
        int TotalOrders,
        decimal SubTotal,
        decimal BonusAmount,
        decimal DeductionAmount,
        decimal TotalAmount,
        string CurrencyId,
        EmployeeInvoiceStatus Status,
        string? CountryId,
        string? LanguageId,
        DateTime GeneratedAt,
        DateTime? ApprovedAt,
        string? ApprovedBy,
        DateTime? PaidAt,
        string? VariableSymbol,
        string? SpecificSymbol,
        string? PaymentReference,
        bool IsCancelled,
        string? CancellationReason,
        DateTime? CancelledAt,
        string? CancelledBy,
        DateTimeOffset CreatedOn);

    public sealed record Employee(
        string Id,
        string? LegalEntityName,
        string? RegistrationNumber,
        string? WorkCountryId,
        ContractStatus ContractStatus);

    public sealed record CreditAccount(
        string Id,
        string UserId,
        string CurrencyId,
        decimal Balance,
        DateTimeOffset? ExpiresOn,
        DateTimeOffset CreatedOn);

    public sealed record CreditTransaction(
        string Id,
        string CreditAccountId,
        decimal Amount,
        CreditTransactionReason Reason,
        string? OrderId,
        string? DisputeId,
        string IdempotencyKey,
        string CreatedBy,
        DateTimeOffset CreatedOn);

    public sealed record PromoCode(
        string Id,
        string Code,
        PromoCodeType Type,
        decimal? DiscountPercent,
        decimal? DiscountAmount,
        string? CurrencyId,
        decimal? MinimumOrderAmount,
        int MaxRedemptionsPerUser,
        int? GlobalMaxRedemptions,
        int CurrentRedemptionsCount,
        DateTimeOffset? ValidFrom,
        DateTimeOffset? ValidUntil,
        bool IsActive,
        DateTimeOffset CreatedOn);

    public sealed record PromoCodeRedemption(
        string Id,
        string PromoCodeId,
        string UserId,
        string OrderId,
        decimal AppliedDiscount,
        DateTimeOffset RedeemedOn,
        int SlotOrdinal);

    public sealed record CompanyInfo(
        string Id,
        string LegalName,
        string TradingName,
        string RegistrationNumber,
        string? VatNumber,
        bool IsVatPayer,
        DateOnly? VatRegisteredFrom,
        string City,
        string ZipCode,
        string CountryId,
        string? Website);

    public sealed record FiscalCounter(string Id, int Year, string IssuerScope, long Value);

    public sealed record PayoutReferenceCounter(string Id, int Year, string Scope, long Value);

    public sealed record TenantConfiguration(string Key, string Value, string? Category);

    public sealed record AdminActionAudit(
        string Id,
        string ActorId,
        UserProfile ActorProfile,
        string Action,
        string? ResourceType,
        string? ResourceId,
        bool Success,
        string? ErrorCode,
        DateTimeOffset OccurredOn,
        string? Reason,
        string? BeforeJson,
        string? AfterJson,
        string? CorrelationId);

    public sealed record EmployeeActionAudit(
        string Id,
        string EmployeeId,
        string OrderId,
        EmployeeAuditAction Action,
        string CreatedBy,
        DateTimeOffset CreatedOn);

    /// <summary>
    /// Written last; its presence is the bundle's completion. The hash of this file is what the
    /// company row carries, so an operator with the bundle in hand can check it against the database.
    /// </summary>
    public sealed record Manifest(
        string TenantId,
        string Name,
        string Adr,
        string? SchemaVersion,
        DateTimeOffset FrozenOn,
        DateTimeOffset BuiltOn,
        IReadOnlyList<CompanyInfo> Companies,
        IReadOnlyList<ManifestFile> Files);

    /// <summary><see cref="Rows"/> is null for a copied PDF, which has no rows to count.</summary>
    public sealed record ManifestFile(string Path, long? Rows, string Sha256);
}
