using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Core;

namespace Infrastructure.Security;

public sealed class Argon2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 3;
    private const int MemoryKb = 64 * 1024;
    private const int Parallelism = 2;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Compute(password, salt);
        return $"$argon2id$v=19$m={MemoryKb},t={Iterations},p={Parallelism}$" +
               Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
    }

    public bool Verify(string password, string hash)
    {
        var parts = hash.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5) return false;
        var salt = Convert.FromBase64String(parts[^2]);
        var expected = Convert.FromBase64String(parts[^1]);
        var actual = Compute(password, salt);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static byte[] Compute(string password, byte[] salt)
    {
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            Iterations = Iterations,
            MemorySize = MemoryKb,
            DegreeOfParallelism = Parallelism
        };
        return argon.GetBytes(HashSize);
    }
}
