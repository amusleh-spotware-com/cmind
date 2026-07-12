using System.Net.Http.Json;
using System.Text.Json;
using Core.Access;
using Core.Options;
using Microsoft.Extensions.Options;

namespace Infrastructure.Access;

/// <summary>
/// CAPTCHA validator for the reCAPTCHA / hCaptcha / Cloudflare Turnstile family, which share a verify
/// contract: POST form fields <c>secret</c> + <c>response</c>, receive JSON <c>{ "success": bool }</c>.
/// Short-circuits to <c>true</c> when CAPTCHA is disabled so callers invoke it unconditionally.
/// </summary>
public sealed class HttpCaptchaValidator(HttpClient http, IOptionsMonitor<AppOptions> options) : ICaptchaValidator
{
    public async Task<bool> ValidateAsync(string? token, string? remoteIp, CancellationToken ct)
    {
        var captcha = options.CurrentValue.Registration.Captcha;
        if (!captcha.Enabled) return true;
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(captcha.VerifyUrl)) return false;

        var form = new Dictionary<string, string>
        {
            ["secret"] = captcha.Secret,
            ["response"] = token
        };
        if (!string.IsNullOrWhiteSpace(remoteIp)) form["remoteip"] = remoteIp;

        try
        {
            using var resp = await http.PostAsync(captcha.VerifyUrl, new FormUrlEncodedContent(form), ct);
            if (!resp.IsSuccessStatusCode) return false;
            var payload = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
            return payload.TryGetProperty("success", out var success) && success.GetBoolean();
        }
        catch (Exception)
        {
            return false;
        }
    }
}
