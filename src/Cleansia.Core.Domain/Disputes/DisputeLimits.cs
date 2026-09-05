namespace Cleansia.Core.Domain.Disputes;

public static class DisputeLimits
{
    public const int DescriptionMin = 10;
    public const int DescriptionMax = 2000;

    /// <summary>
    /// A ceiling on how many items one dispute may name. An order has a handful of lines, so
    /// this is not a product rule — it is the bound that stops an unbounded list arriving from
    /// a public client and being written row by row.
    /// </summary>
    public const int MaxLines = 50;

    /// <summary>
    /// The window the platform advertises for reporting a problem with a clean.
    ///
    /// <para><b>It gates the GUARANTEE, not the door.</b> Owner ruling, 2026-09-05: 24 hours is the
    /// stated deadline and a dispute filed inside it is entitled to be put right — but a later one is
    /// still accepted, because a serious case (something broken, something missing) has to be
    /// investigable on its merits, and a hard cutoff with no override is a support team telling an
    /// honest customer "the system will not let me". Outside the window the claim is judged, not
    /// refused at the door.</para>
    ///
    /// <para>Measured from when the clean actually ended, or from when it was due to start if it
    /// never did — a cleaner who never arrives has no completion time, and that is precisely the case
    /// the window most needs to cover.</para>
    /// </summary>
    public const int FilingWindowHours = 24;

    /// <summary>
    /// Whether a dispute raised at <paramref name="raisedAt"/> falls inside
    /// <see cref="FilingWindowHours"/> of the clean.
    /// </summary>
    public static bool IsWithinFilingWindow(
        DateTime? completedAt, DateTime cleaningDateTime, DateTimeOffset raisedAt) =>
        raisedAt.UtcDateTime <= (completedAt ?? cleaningDateTime).AddHours(FilingWindowHours);
}
