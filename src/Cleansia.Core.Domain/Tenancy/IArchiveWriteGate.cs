namespace Cleansia.Core.Domain.Tenancy;

/// <summary>
/// Lets the law's writes through the archived-company write guard (ADR-0064 D3): the retention
/// sweeps and an erasure keep pseudonymising a frozen company's books, because a company's GDPR
/// obligations do not end with its trading. Scoped; closed unless a caller opened it, and every
/// caller is pinned by a build-time test.
/// </summary>
public interface IArchiveWriteGate
{
    bool IsOpen { get; }

    IDisposable OpenForLegalObligation(string reason);
}
