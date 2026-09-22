namespace Cleansia.HostTests.Infrastructure;

/// <summary>
/// The operating companies the host harness knows (ADR-0061). Every stamped table is NOT NULL, so
/// every seeded row and every minted token names one; <see cref="Default"/> is the seed script's first
/// company, and both rows are re-inserted into <c>Tenants</c> by the harness reset so a
/// <c>CountryConfiguration</c> can point at either.
/// </summary>
public static class HostTestTenants
{
    public const string Default = "cleansia-cz";
    public const string A = "cleansia-cz";
    public const string B = "cleansia-sk";
}
