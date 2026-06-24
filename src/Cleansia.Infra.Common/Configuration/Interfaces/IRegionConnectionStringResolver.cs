namespace Cleansia.Infra.Common.Configuration.Interfaces;

/// <summary>
/// The single seam (ADR-0017) where a region is mapped to its database connection string. Today every
/// region resolves to the one shared West-Europe database; a second region becomes a resolver change
/// here, not an app rewrite. Region is INFRA/config — it is never a clause in the tenancy filter.
/// </summary>
public interface IRegionConnectionStringResolver
{
    string Resolve(string region);

    string ResolveDefault();
}
