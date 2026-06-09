namespace Cleansia.Core.Fiscal.Abstractions;

/// <summary>
/// Contract for country-specific fiscal authority integration.
/// Each country with a mandatory fiscal system provides its own implementation.
/// Countries without a fiscal system use <c>NoOpFiscalService</c>.
/// </summary>
public interface IFiscalService
{
    /// <summary>
    /// Unique identifier of this fiscal service (e.g., "cz-eet2", "sk-ekasa", "de-tss-fiskaly").
    /// Stored on the receipt for auditability and cross-provider traceability.
    /// </summary>
    string ProviderKey { get; }

    /// <summary>
    /// ISO 3166-1 alpha-2 country code this service handles (e.g., "CZ", "SK", "DE").
    /// Use "*" for the no-op fallback.
    /// </summary>
    string CountryCode { get; }

    /// <summary>
    /// Whether <see cref="RegisterReceiptAsync"/> is idempotent on
    /// <see cref="FiscalReceiptRequest.IdempotencyKey"/> — i.e. a repeat call carrying the same key
    /// returns the prior signature/code and does NOT burn a second authority entry (ADR-0004).
    ///
    /// <para>This is a go-live gate, not decoration: the claim-before-register reorder leaves a rare
    /// registered-but-stamp-not-persisted residual whose recovery re-calls
    /// <see cref="RegisterReceiptAsync"/> with the same key. For a <c>BlockingOnline</c> regime a double
    /// registration is a compliance incident, so a provider used under a blocking enforcement mode MUST
    /// declare <c>true</c> here (enforced by <see cref="FiscalGoLiveGate"/>). An <c>AsyncBackground</c>
    /// regime tolerates a rare extra registration and may declare <c>false</c>.</para>
    /// </summary>
    bool RegisterIsIdempotent { get; }

    /// <summary>
    /// Registers a receipt with the country's fiscal authority.
    /// Returns a <see cref="FiscalResult"/> describing whether registration was performed,
    /// the resulting fiscal code (if any), and any errors that occurred.
    /// Implementations should not throw on expected fiscal errors — return a failed
    /// <see cref="FiscalResult"/> instead so the caller can decide how to proceed.
    ///
    /// <para>When <see cref="RegisterIsIdempotent"/> is <c>true</c>, a repeat call carrying the same
    /// <see cref="FiscalReceiptRequest.IdempotencyKey"/> MUST collapse onto the prior registration —
    /// return its signature/code and never create a second authority entry.</para>
    /// </summary>
    Task<FiscalResult> RegisterReceiptAsync(
        FiscalReceiptRequest request,
        CancellationToken cancellationToken);
}
