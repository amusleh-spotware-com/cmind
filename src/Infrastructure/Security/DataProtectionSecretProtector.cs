using System.Text;
using Core;
using Microsoft.AspNetCore.DataProtection;

namespace Infrastructure.Security;

public sealed class DataProtectionSecretProtector : ISecretProtector
{
    private readonly IDataProtectionProvider _provider;
    public DataProtectionSecretProtector(IDataProtectionProvider provider) => _provider = provider;

    public byte[] Protect(ReadOnlySpan<byte> plaintext, string purpose)
        => _provider.CreateProtector($"ctw:{purpose}").Protect(plaintext.ToArray());

    public byte[] Unprotect(ReadOnlySpan<byte> ciphertext, string purpose)
        => _provider.CreateProtector($"ctw:{purpose}").Unprotect(ciphertext.ToArray());

    public string ProtectString(string plaintext, string purpose)
        => Convert.ToBase64String(Protect(Encoding.UTF8.GetBytes(plaintext), purpose));

    public string UnprotectString(string ciphertext, string purpose)
        => Encoding.UTF8.GetString(Unprotect(Convert.FromBase64String(ciphertext), purpose));
}
