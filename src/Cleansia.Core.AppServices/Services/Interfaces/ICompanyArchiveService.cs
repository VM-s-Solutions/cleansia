namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// Builds a frozen company's archive bundle (ADR-0064 D3): every books table as JSON Lines, the
/// receipt and payout-invoice PDFs copied beside them, and a manifest written last whose hash lands
/// on the company row. The input is frozen, so a run that died is rebuilt from scratch into the same
/// folder and a finished archive is a no-op.
/// </summary>
public interface ICompanyArchiveService
{
    Task<CompanyArchiveRunSummary> RunAsync(string tenantId, DateTimeOffset requestedOn, CancellationToken cancellationToken);
}

/// <summary>
/// What one build did. <see cref="Ran"/> is false for the permanent no-ops — a company that does
/// not exist, is not frozen, is already archived, or whose row names a different request — and
/// <see cref="SkippedBecause"/> says which.
/// </summary>
public sealed record CompanyArchiveRunSummary(
    bool Ran,
    string? SkippedBecause,
    string? Folder,
    int Files,
    string? ManifestSha256)
{
    public static CompanyArchiveRunSummary Skipped(string reason) => new(false, reason, null, 0, null);
}
