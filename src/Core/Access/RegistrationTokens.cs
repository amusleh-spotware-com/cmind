using System.Security.Cryptography;
using Core.Constants;

namespace Core.Access;

/// <summary>
/// Email-verification token primitives. The raw token is high-entropy and url-safe; only its SHA-256 hash is
/// ever stored, so a database leak cannot yield a usable verification link.
/// </summary>
public static class RegistrationTokens
{
    public static string Generate()
        => Base64Url(RandomNumberGenerator.GetBytes(RegistrationConstants.VerificationTokenBytes));

    public static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
