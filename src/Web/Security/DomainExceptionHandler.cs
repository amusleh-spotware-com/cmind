using Core.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Web.Security;

// Maps domain/persistence failures on API routes to correct HTTP status codes + RFC7807 ProblemDetails
// instead of leaking a raw 500 with a stack trace. A DomainException is a broken invariant (client's
// fault) → 400; a unique-constraint violation (Postgres 23505) is a conflicting write → 409. Non-/api
// paths return false so the Blazor SSR error page still handles circuit/page failures.
internal sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            return false;

        var (status, title) = Classify(exception);
        if (status is null) return false;

        context.Response.StatusCode = status.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails =
            {
                Status = status.Value,
                Title = title,
                Detail = DetailFor(exception),
                Type = exception is DomainException de ? de.Code : null,
            },
        });
    }

    private static (int? Status, string Title) Classify(Exception exception) => exception switch
    {
        DomainException => (StatusCodes.Status400BadRequest, "Invalid request"),
        DbUpdateException dbEx when IsUniqueViolation(dbEx) => (StatusCodes.Status409Conflict, "Conflict"),
        _ => (null, ""),
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
