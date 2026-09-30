namespace Cleansia.Infra.Common.Configuration.Interfaces;

/// <summary>
/// Payment switches that are business decisions rather than provider settings (section <c>Payments</c>).
/// </summary>
public interface IPaymentsConfig
{
    /// <summary>
    /// Whether an open receivable is charged to the customer's saved card with the customer absent. OFF
    /// until the terms carry the lawyer's consent wording for the card guarantee (owner ruling 2026-09-28,
    /// decision 16); while off, a receivable stays open, administrators see it and the customer pays it
    /// through their pay link. Defaults to false when the section is absent.
    /// </summary>
    bool OffSessionChargesEnabled { get; set; }
}
