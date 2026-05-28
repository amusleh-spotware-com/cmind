using Microsoft.AspNetCore.Http;

namespace Web.Auth;

public sealed class CookieForwardingHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    private const string CookieHeader = "Cookie";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var ctx = accessor.HttpContext;
        if (ctx is not null && ctx.Request.Headers.TryGetValue(CookieHeader, out var cookie))
            request.Headers.TryAddWithoutValidation(CookieHeader, cookie.ToString());
        return base.SendAsync(request, ct);
    }
}
