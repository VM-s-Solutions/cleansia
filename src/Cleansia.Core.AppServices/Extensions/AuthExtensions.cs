using System.Security.Claims;
using System.Security.Cryptography;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.AppServices.Extensions;

public static class AuthExtensions
{
    public static bool CheckIfPasswordSame(this string providedPassword, string saltedHashedPassword)
    {
        var hashBytes = Convert.FromBase64String(saltedHashedPassword);
        var salt = GetSaltFromHashBytes(hashBytes);
        var computedHash = ComputePbkdf2Hash(providedPassword, salt);

        return CompareHashes(hashBytes, computedHash);
    }

    public static IEnumerable<Claim> SetClaims(this User user)
    {
        yield return new Claim(ClaimTypes.NameIdentifier, user.Id.ToString());
        yield return new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}");
        yield return new Claim(ClaimTypes.Email, user.Email);
        yield return new Claim(ClaimTypes.Role, user.Profile.ToString());
    }

    private static byte[] GetSaltFromHashBytes(byte[] hashBytes)
    {
        var salt = new byte[16];
        Array.Copy(hashBytes, 0, salt, 0, 16);
        return salt;
    }

    private static byte[] ComputePbkdf2Hash(string password, byte[] salt)
    {
        var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations: 10000, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(20);
    }

    private static bool CompareHashes(byte[] storedHashBytes, byte[] computedHash)
    {
        for (var i = 0; i < 20; i++)
        {
            if (storedHashBytes[i + 16] != computedHash[i])
            {
                return false;
            }
        }

        return true;
    }
}