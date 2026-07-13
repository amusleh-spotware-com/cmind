using Core.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Web.Security;

// Maps domain/persistence failures on API routes to correct HTTP status codes + RFC7807 ProblemDetails
// instead of leaking a raw 500 with a stack trace. A DomainException is a broken invariant (client's
// fault) → 400; a unique-constraint violation (Postgres 23505) is a conflicting write → 409. Non-/api
// paths rethrow so the Blazor SSR error page / dev exception page still handle circuit/page failures.
//
// Implemented as explicit early middleware (not IExceptionHandler) so it catches BEFORE the developer
// exception page that ASP.NET auto-inserts in Development — the /api ProblemDetails contract must hold
// in every environment, including tests.
internal sealed class DomainExceptionMiddleware(RequestDelegate next, IProblemDetailsService problemDetails)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
                                   && Classify(ex) is { } mapped)
        {
            if (context.Response.HasStarted) throw;
            context.Response.Clear();
            context.Response.StatusCode = mapped.Status;
            await problemDetails.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                Exception = ex,
                ProblemDetails =
                {
                    Status = mapped.Status,
                    Title = mapped.Title,
                    Detail = DetailFor(ex),
                    Type = ex is DomainException de ? de.Code : null,
                },
            });
        }
    }

    private static (int Status, string Title)? Classify(Exception exception) => exception switch
    {
        DomainException => (StatusCodes.Status400BadRequest, "Invalid request"),
        DbUpdateException when IsUniqueViolation(exception) => (StatusCodes.Status409Conflict, "Conflict"),
        _ => null,
    };

    private static string DetailFor(Exception exception) => exception switch
    {
        DomainException de => de.Message,
        DbUpdateException when IsUniqueViolation(exception) => "A record with the same key already exists.",
        _ => "",
    };

    private static bool IsUniqueViolation(Exception exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
