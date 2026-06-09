using Cleansia.Core.Fiscal.Abstractions;

namespace Cleansia.Infra.Fiscal.NoOp;

/// <summary>
/// Default fiscal service for countries without a mandatory fiscal reporting system.
/// Returns <see cref="FiscalResult.NotRequired"/> — the receipt is issued without any
/// fiscal authority registration.
/// Currently used for: Czech Republic (until EET 2.0 launches January 2027).
/// </summary>
public class NoOpFiscalService : IFiscalService
{
    public string ProviderKey => "noop";

    // Wildcard: resolver uses this as the fallback for unrecognized country codes.
    public string CountryCode => "*";

    // No authority is contacted, so re-running is always a no-op — trivially idempotent.
    public bool RegisterIsIdempotent => true;

    public Task<FiscalResult> RegisterReceiptAsync(
        FiscalReceiptRequest request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(FiscalResult.NotRequired());
    }
}
