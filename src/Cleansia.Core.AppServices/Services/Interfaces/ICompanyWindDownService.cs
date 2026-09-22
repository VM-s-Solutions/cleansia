namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// The company wind-down sweep (ADR-0064 D2): one run over the company named by the message, in
/// the order notices → orders → templates → Plus → credit → the last pay period, each step
/// idempotent on state and committed per unit of work, so a redelivery or a later request resumes
/// past what is done and a company with nothing left to settle is a no-op.
/// </summary>
public interface ICompanyWindDownService
{
    Task<CompanyWindDownRunSummary> RunAsync(string tenantId, CancellationToken cancellationToken);
}

/// <summary>
/// What one run did. <see cref="Ran"/> is false for the permanent no-ops — a company that does not
/// exist, has no wind-down date, or is frozen for archive — and <see cref="SkippedBecause"/> says which.
/// </summary>
public sealed record CompanyWindDownRunSummary(
    bool Ran,
    string? SkippedBecause,
    int NoticesEnqueued,
    int OrdersCancelled,
    int Refunded,
    int RefundsRedriven,
    int RefundFailures,
    int TemplatesPaused,
    int MembershipsCancelled,
    int CreditAccountsDischarged,
    int PeriodsClosed,
    string? PeriodStepSkippedBecause)
{
    public static CompanyWindDownRunSummary Skipped(string reason) =>
        new(false, reason, 0, 0, 0, 0, 0, 0, 0, 0, 0, null);
}
