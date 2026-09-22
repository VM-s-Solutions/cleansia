namespace Cleansia.Core.AppServices.Tenancy;

/// <summary>
/// Answers two questions for one country id: is it a market, and which operating company serves it
/// (ADR-0061 D3). Null <c>countryId</c> means the default market — the SAME choice
/// <c>GetMarkets</c> makes, not a re-derivation. <c>IsMarket</c> is serviced ∧ configured ∧ active
/// currency (ADR-0058 D1); <c>OperatorTenantId</c> is the configuration's column, null when the
/// country is not a market or when nobody operates it. The two answers map to two different refusals,
/// which is why both are returned rather than one.
/// </summary>
public interface IOperatorTenantResolver
{
    Task<OperatorResolution> ResolveAsync(string? countryId, CancellationToken cancellationToken);
}

public readonly record struct OperatorResolution(bool IsMarket, string? OperatorTenantId)
{
    public static readonly OperatorResolution NotAMarket = new(false, null);
}
