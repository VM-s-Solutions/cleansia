namespace Cleansia.Core.Domain.Payouts;

/// <summary>
/// The Czechoslovak domestic account-number check (ADR-0034 D4.1). CZ and SK share the numbering
/// structure, so one implementation serves both schemes.
/// <para>Rule: zero-pad the field to ten digits, apply the weights 6,3,7,9,10,5,8,4,2,1
/// <b>left-to-right</b>, and require the weighted sum ≡ 0 (mod 11). The same rule serves the six-digit
/// prefix, which after padding lands on the <i>last</i> six weights.</para>
/// </summary>
public static class CzskAccountNumber
{
    /// <remarks>
    /// The direction is not a formatting detail. Applied right-to-left this same vector scores the
    /// reference account 5885638003 as 1 and rejects roughly nine out of ten real Czech accounts — and
    /// because payout validation fails closed, that lands as a refusal to save a cleaner's bank details.
    /// Pinned by <c>CzskAccountNumberTests.Reversing_The_Weight_Direction_Rejects_The_Reference_Specimen</c>.
    /// </remarks>
    private static readonly int[] Weights = [6, 3, 7, 9, 10, 5, 8, 4, 2, 1];

    public const int PrefixWidth = 6;
    public const int NumberWidth = 10;

    public static bool IsValidPrefix(string? value) => IsValid(value, PrefixWidth);

    public static bool IsValidNumber(string? value) => IsValid(value, NumberWidth);

    public static string? CanonicalizePrefix(string? value) => Canonicalize(value, PrefixWidth);

    public static string? CanonicalizeNumber(string? value) => Canonicalize(value, NumberWidth);

    private static bool IsValid(string? value, int maxWidth)
    {
        if (!IsDigitsWithin(value, maxWidth))
        {
            return false;
        }

        var padded = value!.Trim().PadLeft(NumberWidth, '0');
        var sum = 0;
        for (var i = 0; i < NumberWidth; i++)
        {
            sum += (padded[i] - '0') * Weights[i];
        }

        return sum % 11 == 0;
    }

    private static string? Canonicalize(string? value, int width) =>
        IsDigitsWithin(value, width) ? value!.Trim().PadLeft(width, '0') : null;

    private static bool IsDigitsWithin(string? value, int maxWidth)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxWidth && trimmed.All(char.IsAsciiDigit);
    }
}
