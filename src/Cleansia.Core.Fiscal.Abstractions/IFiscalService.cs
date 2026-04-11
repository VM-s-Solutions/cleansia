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
    /// Registers a receipt with the country's fiscal authority.
    /// Returns a <see cref="FiscalResult"/> describing whether registration was performed,
    /// the resulting fiscal code (if any), and any errors that occurred.
    /// Implementations should not throw on expected fiscal errors — return a failed
    /// <see cref="FiscalResult"/> instead so the caller can decide how to proceed.
    /// </summary>
    Task<FiscalResult> RegisterReceiptAsync(
        FiscalReceiptRequest request,
        CancellationToken cancellationToken);
}
