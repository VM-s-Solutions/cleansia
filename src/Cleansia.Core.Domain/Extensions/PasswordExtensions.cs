using System.Security.Cryptography;
using System.Text;

namespace Cleansia.Core.Domain.Extensions;

public static class PasswordExtensions
{
    private const string CurrentVersionPrefix = "v2$";
    private const int CurrentIterations = 600_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    private const int LegacyHashSize = 20;
    private const int LegacyIterations = 10_000;
    private const int LegacyTotalBytes = SaltSize + LegacyHashSize;

    public static string HashAndSaltPassword(this string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, CurrentIterations, HashAlgorithmName.SHA256, HashSize);
        var combined = new byte[SaltSize + HashSize];
        Buffer.BlockCopy(salt, 0, combined, 0, SaltSize);
        Buffer.BlockCopy(hash, 0, combined, SaltSize, HashSize);
        return CurrentVersionPrefix + Convert.ToBase64String(combined);
    }

    public static bool VerifyPassword(this string candidate, string stored)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return false;
        }

        if (stored.StartsWith(CurrentVersionPrefix, StringComparison.Ordinal))
        {
            return VerifyV2(candidate, stored.AsSpan(CurrentVersionPrefix.Length));
        }

        return VerifyLegacy(candidate, stored);
    }

    public static bool NeedsRehash(string stored)
    {
        return string.IsNullOrEmpty(stored)
            || !stored.StartsWith(CurrentVersionPrefix, StringComparison.Ordinal);
    }

    private static bool VerifyV2(string candidate, ReadOnlySpan<char> base64)
    {
        Span<byte> combined = stackalloc byte[SaltSize + HashSize];
        if (!Convert.TryFromBase64Chars(base64, combined, out var written) || written != combined.Length)
        {
            return false;
        }

        Span<byte> salt = stackalloc byte[SaltSize];
        Span<byte> stored = stackalloc byte[HashSize];
        combined.Slice(0, SaltSize).CopyTo(salt);
        combined.Slice(SaltSize, HashSize).CopyTo(stored);

        Span<byte> computed = stackalloc byte[HashSize];
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(candidate),
            salt,
            computed,
            CurrentIterations,
            HashAlgorithmName.SHA256);

        return CryptographicOperations.FixedTimeEquals(computed, stored);
    }

    private static bool VerifyLegacy(string candidate, string stored)
    {
        byte[] hashBytes;
        try
        {
            hashBytes = Convert.FromBase64String(stored);
        }
        catch (FormatException)
        {
            return false;
        }
        if (hashBytes.Length != LegacyTotalBytes)
        {
            return false;
        }

        var salt = new byte[SaltSize];
        Buffer.BlockCopy(hashBytes, 0, salt, 0, SaltSize);

        var computed = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(candidate),
            salt,
            LegacyIterations,
            HashAlgorithmName.SHA256,
            LegacyHashSize);

        var storedHash = new byte[LegacyHashSize];
        Buffer.BlockCopy(hashBytes, SaltSize, storedHash, 0, LegacyHashSize);

        return CryptographicOperations.FixedTimeEquals(computed, storedHash);
    }
}
