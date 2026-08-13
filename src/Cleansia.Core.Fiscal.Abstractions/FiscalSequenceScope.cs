namespace Cleansia.Core.Fiscal.Abstractions;

/// <summary>
/// Maps a fiscal regime to the <c>(Year, IssuerScope)</c> pair keying its gapless counter.
///
/// <para><b>The numbering unit and the year-reset rule are legally regime-specific</b> — CZ and SK reset
/// each calendar year per provider; a German TSE counter is per device and does NOT reset annually;
/// AT and ES are per issuer and continuous. → /architecture/fiscal-compliance</para>
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
