namespace Cleansia.Core.Fiscal.Abstractions;

/// <summary>
/// Maps a fiscal regime (identified by its provider key) to the
/// <c>(Year, IssuerScope)</c> pair that keys its gapless sequence counter.
///
/// <para>The numbering unit and the year-reset rule are legally regime-specific:</para>
/// <list type="bullet">
///   <item><description>CZ EET 2.0 / SK eKasa: numbering is per provider and resets each calendar
///   year, so the scope is the provider key and the year is the calendar year.</description></item>
///   <item><description>DE TSE: the transaction counter is per TSE and does NOT reset annually, so
///   the year is <c>NoAnnualResetYear</c> and the scope carries the TSE identity (the provider key
///   today; a provider-plus-device key once multi-TSE config lands).</description></item>
///   <item><description>AT RKSV / ES VeriFactu: per issuer, continuous (no annual reset).</description></item>
/// </list>
/// </summary>
public static class FiscalSequenceScope
{
    public const int NoAnnualResetYear = 0;

    public const string DefaultIssuerScope = "DEFAULT";

    private static readonly HashSet<string> YearResetProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "cz-eet2",
        "sk-ekasa",
    };

    public static (int Year, string IssuerScope) Resolve(string providerKey, int calendarYear)
    {
        var scope = string.IsNullOrWhiteSpace(providerKey) ? DefaultIssuerScope : providerKey;
        var year = ResetsAnnually(providerKey) ? calendarYear : NoAnnualResetYear;
        return (year, scope);
    }

    public static bool ResetsAnnually(string providerKey) =>
        !string.IsNullOrWhiteSpace(providerKey) && YearResetProviders.Contains(providerKey);
}
