using Core;
using Core.Constants;
using OtpNet;

namespace Infrastructure.Security;

/// <summary>
/// RFC 6238 TOTP authenticator backed by the Otp.NET library. Keeps the crypto/library dependency out of the
/// pure domain (see <see cref="ITotpAuthenticator"/>). SHA-1 / 6 digits / 30s step — the profile every stock
/// authenticator app (Google, Microsoft, Authy, Aegis, FreeOTP) speaks.
/// </summary>
public sealed class OtpNetTotpAuthenticator : ITotpAuthenticator
{
    public string GenerateSecret()
        => Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(MfaConstants.SecretSizeBytes));

    public string BuildOtpAuthUri(string issuer, string accountName, string secret)
    {
        var label = Uri.EscapeDataString($"{issuer}:{accountName}");
        var query =
            $"secret={secret}" +
            $"&issuer={Uri.EscapeDataString(issuer)}" +
            "&algorithm=SHA1" +
            $"&digits={MfaConstants.Digits}" +
            $"&period={MfaConstants.PeriodSeconds}";
        return $"otpauth://totp/{label}?{query}";
    }

    public bool VerifyCode(string secret, string code, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code)) return false;

        var totp = new Totp(Base32Encoding.ToBytes(secret),
            step: MfaConstants.PeriodSeconds, mode: OtpHashMode.Sha1, totpSize: MfaConstants.Digits);
        var window = new VerificationWindow(
            previous: MfaConstants.VerificationWindowSteps, future: MfaConstants.VerificationWindowSteps);
        return totp.VerifyTotp(now.UtcDateTime, code.Trim(), out _, window);
    }
}
