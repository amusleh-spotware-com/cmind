using Core.Constants;
using Core.Options;
using Core.Time;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Web.Time;

/// <summary>
/// Resolves the time zone the current user's times are displayed in. Every human-facing timestamp is
/// converted through this — UTC storage stays untouched, only presentation localizes. Priority: the
/// signed-in user's <c>tz</c> claim (readable inside the Blazor circuit, which has no <c>HttpContext</c>),
/// then the time-zone cookie (anonymous / server-render path), then the deployment's white-label default
/// (<c>App:Branding:DefaultTimeZone</c>, owner-tunable), finally UTC.
/// </summary>
public interface IUserTimeZone
{
    ValueTask<TimeZoneInfo> GetZoneAsync();
    ValueTask<string> GetZoneIdAsync();
}

public sealed class UserTimeZone(
    AuthenticationStateProvider authState,
    IHttpContextAccessor httpContext,
    IOptionsMonitor<AppOptions> options) : IUserTimeZone
{
    private TimeZoneInfo? _zone;

    public async ValueTask<TimeZoneInfo> GetZoneAsync()
    {
        if (_zone is not null) return _zone;
        var id = await ResolveIdAsync();
        _zone = TimeZoneId.TryFrom(id, out var zone) ? zone.ToTimeZoneInfo() : TimeZoneInfo.Utc;
        return _zone;
    }

    public async ValueTask<string> GetZoneIdAsync() => (await GetZoneAsync()).Id;

    private async ValueTask<string> ResolveIdAsync()
    {
        var state = await authState.GetAuthenticationStateAsync();
        var claim = state.User.FindFirst(TimeConstants.TimeZoneClaimType)?.Value;
        if (TimeZoneId.TryFrom(claim, out var fromClaim)) return fromClaim.Value;

        var cookie = httpContext.HttpContext?.Request.Cookies[TimeConstants.TimeZoneCookieName];
        if (TimeZoneId.TryFrom(cookie, out var fromCookie)) return fromCookie.Value;

        if (TimeZoneId.TryFrom(options.CurrentValue.Branding.DefaultTimeZone, out var fromDefault))
            return fromDefault.Value;

        return TimeZoneId.Utc.Value;
    }
}
