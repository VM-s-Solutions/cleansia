namespace Cleansia.Core.AppServices.Tenancy;

/// <summary>
/// Marks an anonymous request that writes a tenanted row or reads one through the filter: it names a
/// MARKET (a country id — the same thing it could pass to <c>Order/Quote</c>), never a tenant (S1), and
/// <see cref="OperatorTenantScopeBehavior{TRequest,TResponse}"/> resolves the market's operating
/// company into the ambient tenant before validation runs. Null names the default market. A request
/// that carries a tenant claim ordinarily retains it (ADR-0061 D3). A secret-key guest request instead
/// resolves the proven booking's operator before validation, regardless of a supplied account session.
/// </summary>
public interface IOperatorScopedRequest
{
    string? CountryId { get; }
}
