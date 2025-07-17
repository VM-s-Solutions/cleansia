using System.Security.Cryptography;

namespace Cleansia.Core.Domain.Extensions;

public static class PasswordExtensions
{
    public static string HashAndSaltPassword(this string password)
    {
        var salt = GetSalt();
        var hash = GetPbkdf2HashBytes(password, salt);
        var hashBytes = GetHashSaltPasswordBytes(salt, hash);

        return Convert.ToBase64String(hashBytes);
    }

    private static byte[] GetSalt()
    {
        var salt = new byte[16];

        var randomNumberGenerator = RandomNumberGenerator.Create();
        randomNumberGenerator.GetBytes(salt);

        return salt;
    }

    private static byte[] GetPbkdf2HashBytes(string password, byte[] salt)
    {
        var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations: 10000, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(20);
    }

    private static byte[] GetHashSaltPasswordBytes(byte[] salt, byte[] hash)
    {
        var hashBytes = new byte[36];
        Array.Copy(salt, 0, hashBytes, 0, 16);
        Array.Copy(hash, 0, hashBytes, 16, 20);

        return hashBytes;
    }
}
