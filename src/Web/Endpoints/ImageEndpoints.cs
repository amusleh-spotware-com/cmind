using Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Web.Endpoints;

public static class ImageEndpoints
{
    public static IEndpointRouteBuilder MapImageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/images/tags", async (IGithubContainerRegistryTagProvider p) => await p.GetTagsAsync(default))
            .RequireAuthorization("UserOrAbove");
        return app;
    }
}
