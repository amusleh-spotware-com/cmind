namespace Core.Access;

/// <summary>
/// Validates a CAPTCHA token from the public registration form. The implementation short-circuits to
/// <c>true</c> when CAPTCHA is disabled for the deployment, so callers can invoke it unconditionally.
/// </summary>
public interface ICaptchaValidator
{
    Task<bool> ValidateAsync(string? token, string? remoteIp, CancellationToken ct);
}
