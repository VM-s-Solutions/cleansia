namespace Cleansia.Infra.Common.Configuration.Interfaces;

/// <summary>
/// Master switch for the nightly pay-period job (section <c>PayPeriodClosing</c>). That job closes
/// expired pay periods, opens the next one, and — the part that matters — <b>generates and emails an
/// invoice per employee</b>. It is a money-producing job on a fixed 02:00 schedule
/// (<c>PayPeriodTimerFunction</c>) and until T-0689 it had no switch of any kind.
///
/// <para>Defaults to <c>true</c>: an absent section means the job runs, and turning it off is something
/// somebody typed. This is the same shape as <c>IDataRetentionConfig</c> and
/// <c>IOutboxRetentionConfig</c>, and deliberately NOT a database row — see the T-0685 note on
/// <c>IDataRetentionConfig</c> for why a switch that lives in a table nobody seeds reads as "off" in
/// production.</para>
///
/// <para><b>Gated at the timer handler, not inside the service.</b> <c>EnsureOpenPeriodAsync</c> is
/// called inline by pay calculation and must keep working regardless, or pay-calc starts failing with
/// "NoActivePeriod". Only the scheduled sweep is switchable.</para>
/// </summary>
public interface IPayPeriodClosingConfig
{
    /// <summary>Master switch. When false the nightly pay-period timer is a no-op.</summary>
    bool Enabled { get; set; }
}
