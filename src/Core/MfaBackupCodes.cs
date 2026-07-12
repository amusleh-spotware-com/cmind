using System.Security.Cryptography;
using System.Text;
using Core.Constants;

namespace Core;

/// <summary>
/// Generation, normalization and hashing of two-factor recovery codes. Codes are high-entropy random
/// tokens drawn from an unambiguous alphabet; because they carry full entropy a fast one-way hash
/// (SHA-256), not a password KDF, is the correct store — verification stays cheap and nothing reversible
/// is persisted. Pure BCL crypto so it lives in the domain.
/// </summary>
public static class MfaBackupCodes
{
    // Crockford-style base32 without 0/1/I/L/O to avoid transcription ambiguity.
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    public static IReadOnlyList<string> Generate(int count, int length)
    {
        var codes = new List<string>(count);
        for (var i = 0; i < count; i++) codes.Add(GenerateOne(length));
        return codes;
    }

    private static string GenerateOne(int length)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++) chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }

    public static string Hash(string code)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(code))));

    // Strip grouping separators / whitespace and upper-case so "abcde-fghjk" hashes the same as "ABCDEFGHJK".
    public static string Normalize(string code)
        => new(code.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    // Presentation grouping (XXXXX-XXXXX) shown to the user; irrelevant to hashing.
    public static string Format(string code)
        => code.Length == MfaConstants.BackupCodeLength ? $"{code[..5]}-{code[5..]}" : code;
}
