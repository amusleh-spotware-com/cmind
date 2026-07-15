using Core.Constants;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR.Client;

namespace Web.Hubs;

// Builds a LogsHub client for a server-side Blazor circuit. A bare HubConnection opened from the circuit has
// none of the browser's cookies, so its negotiate request to the [Authorize]'d /hubs/logs endpoint is
// redirected to the HTML login page — the SignalR client then fails with "Invalid negotiation response
// received." Forwarding the current request's auth cookie (the same mechanism CookieForwardingHandler uses
// for the app's HttpClient) lets negotiate authenticate, so log streaming actually connects.
public sealed class LogHubConnectionFactory(IHttpContextAccessor accessor, NavigationManager nav)
{
    private const string CookieHeader = "Cookie";

    public HubConnection Create()
    {
        var cookie = accessor.HttpContext?.Request.Headers[CookieHeader].ToString();
        return new HubConnectionBuilder()
            .WithUrl(nav.ToAbsoluteUri(HubRoutes.Logs), options =>
            {
                if (!string.IsNullOrEmpty(cookie))
                    options.Headers[CookieHeader] = cookie;
            })
            .Build();
    }
}
