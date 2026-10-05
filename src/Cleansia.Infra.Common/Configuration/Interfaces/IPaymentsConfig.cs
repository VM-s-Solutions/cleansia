namespace Cleansia.Infra.Common.Configuration.Interfaces;

/// <summary>
/// Payment switches that are business decisions rather than provider settings (section <c>Payments</c>).
/// </summary>
public interface IPaymentsConfig
{
    /// <summary>
    /// Whether an open receivable is charged to the customer's saved card with the customer absent. OFF, and
    /// it stays off: switching it on would contradict the owner's ruling of 2026-10-04 that no saved card is
    /// charged for a fee or unpaid cash, which replaced decision 16 of 2026-09-28 (off only until the terms
    /// carried the lawyer's consent wording). While off, a receivable stays open, administrators see it and
    /// the customer pays it through their pay link; even on, only a card saved under a card-guarantee consent
    /// is charged. Defaults to false when the section is absent.
    /// </summary>
    bool OffSessionChargesEnabled { get; set; }
}
