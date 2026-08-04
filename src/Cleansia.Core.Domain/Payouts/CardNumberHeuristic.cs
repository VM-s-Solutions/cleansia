namespace Cleansia.Core.Domain.Payouts;

/// <summary>
/// ADR-0034 D9a — no card number ever reaches this database, and the invariant is enforced at runtime
/// rather than asserted in prose. A payout destination is an account, and a card payout would store a
/// provider's account id, never a PAN.
/// <para>The heuristic: 13–19 digits after stripping separators that pass the Luhn check (ISO/IEC 7812-1).
/// It is deliberately blunt — a false positive costs the cleaner a re-type, a false negative brings the
/// platform, its backups and its logs into PCI DSS scope.</para>
/// </summary>
public static class CardNumberHeuristic
{
    public static bool LooksLikeCardNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        if (value.Any(character => char.IsAsciiLetter(character)) || digits.Length is < 13 or > 19)
        {
            return false;
        }

        var sum = 0;
        var doubled = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var digit = digits[i] - '0';
            if (doubled)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
            doubled = !doubled;
        }

        return sum % 10 == 0;
    }
}
