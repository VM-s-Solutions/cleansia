namespace Cleansia.Core.Fiscal.Abstractions;

/// <summary>
/// How strictly a country enforces fiscal registration before the receipt can be delivered to the customer.
/// Chosen per country via <c>CountryConfiguration.FiscalEnforcementMode</c>.
/// </summary>
public enum FiscalEnforcementMode
{
    /// <summary>
    /// No fiscal registration required. Receipt is generated and sent immediately.
    /// Example: Czech Republic today (EET was suspended).
    /// </summary>
    None = 0,

    /// <summary>
    /// Fiscal registration is required but lenient — the receipt PDF and email may be sent
    /// before fiscal registration succeeds. Failures are retried in the background by
    /// <c>FiscalRetryService</c>. The customer never waits on the fiscal authority.
    /// Example: CZ EET 2.0, SK eKasa, IT RT, HU, PL KSeF.
    /// </summary>
    AsyncBackground = 1,

    /// <summary>
    /// Fiscal registration is strictly required before the receipt is considered valid.
    /// The receipt PDF and email are held until the fiscal authority issues the signature.
    /// If the fiscal authority is unreachable, delivery is delayed — but the customer's
    /// order is never blocked. A background retry job completes delivery once the
    /// signature is obtained.
    /// Example: DE TSE, AT RKSV, ES VeriFactu.
    /// </summary>
    BlockingOnline = 2,

    /// <summary>
    /// Strict like <see cref="BlockingOnline"/>, but supports an offline cache at the POS
    /// that signs receipts locally and syncs with the authority later. Reserved for
    /// future hardware/TSE integrations where offline operation is required.
    /// </summary>
    BlockingWithOfflineCache = 3,
}
