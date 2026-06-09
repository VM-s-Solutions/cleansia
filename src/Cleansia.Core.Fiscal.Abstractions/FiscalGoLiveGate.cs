namespace Cleansia.Core.Fiscal.Abstractions;

/// <summary>
/// Enforces an ADR-0004 go-live gate: a fiscal provider may only run under a blocking
/// enforcement mode once its <see cref="IFiscalService.RegisterReceiptAsync"/> is verified idempotent
/// on <see cref="FiscalReceiptRequest.IdempotencyKey"/>. The recovery path re-registers the same
/// receipt; for <c>BlockingOnline</c> / <c>BlockingWithOfflineCache</c> a non-idempotent provider would
/// double-register at the authority, which is a compliance incident — so we fail fast at the seam
/// rather than risk it at runtime.
/// </summary>
public static class FiscalGoLiveGate
{
    public static bool RequiresRegisterIdempotency(FiscalEnforcementMode mode) =>
        mode is FiscalEnforcementMode.BlockingOnline or FiscalEnforcementMode.BlockingWithOfflineCache;

    public static void EnsureRegisterIdempotent(IFiscalService provider, FiscalEnforcementMode mode)
    {
        if (RequiresRegisterIdempotency(mode) && !provider.RegisterIsIdempotent)
        {
            throw new FiscalGoLiveGateException(provider.ProviderKey, mode);
        }
    }
}

public sealed class FiscalGoLiveGateException(string providerKey, FiscalEnforcementMode mode)
    : Exception(
        $"Fiscal provider '{providerKey}' is not register-idempotent and cannot run under enforcement mode '{mode}' " +
        "(ADR-0004 go-live gate 2: RegisterReceiptAsync must be idempotent on the receipt number for blocking regimes).")
{
    public string ProviderKey { get; } = providerKey;

    public FiscalEnforcementMode Mode { get; } = mode;
}
