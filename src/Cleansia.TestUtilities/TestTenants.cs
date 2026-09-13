namespace Cleansia.TestUtilities;

/// <summary>
/// The operating companies the test fixtures stamp rows with. Every stamped table is NOT NULL
/// (ADR-0061 D8), so a fixture that commits an ITenantEntity names one; the default is the seed's.
/// </summary>
public static class TestTenants
{
    public const string Default = "cleansia-cz";
    public const string Second = "cleansia-sk";
}
