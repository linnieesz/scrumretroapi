using System.Security.Cryptography;

namespace ScrumRetroApi.Common;

/// <summary>PBKDF2 (HMAC-SHA256) com salt aleatório por usuário.</summary>
public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 210_000;

    private static readonly byte[] DummySalt = RandomNumberGenerator.GetBytes(SaltSize);

    public static (string Hash, string Salt) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public static bool Verify(string password, string hashBase64, string saltBase64)
    {
        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(saltBase64);
            expected = Convert.FromBase64String(hashBase64);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>Gasta o mesmo tempo de uma verificação real (reduz enumeração de e-mails por timing no login).</summary>
    public static void BurnTime(string password)
    {
        _ = Rfc2898DeriveBytes.Pbkdf2(password, DummySalt, Iterations, HashAlgorithmName.SHA256, HashSize);
    }
}
