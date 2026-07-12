namespace Web.Calendar;

/// <summary>
/// A compact, hand-authored OpenAPI 3 document for the public Calendar API, served at
/// <c>/api/calendar/v1/openapi.json</c> so integrators and cBot authors get a discoverable, versioned
/// contract. Kept deliberately small (paths + the bearer security scheme); the feature docs carry the prose.
/// </summary>
public static class CalendarOpenApi
{
    public static IReadOnlyDictionary<string, object?> Document { get; } = Build();

    private static Dictionary<string, object?> Build()
    {
        static Dictionary<string, object?> Get(string summary, string scope) => new()
        {
            ["get"] = new Dictionary<string, object?>
            {
                ["summary"] = summary,
                ["security"] = new object[] { new Dictionary<string, object?> { ["bearer"] = new[] { scope } } },
                ["responses"] = new Dictionary<string, object?> { ["200"] = new Dictionary<string, object?> { ["description"] = "OK" } }
            }
        };

        return new Dictionary<string, object?>
        {
            ["openapi"] = "3.0.3",
            ["info"] = new Dictionary<string, object?>
            {
                ["title"] = "cMind Economic Calendar API",
                ["version"] = "1.0",
                ["description"] = "Point-in-time economic calendar: events, revisions, surprises, blackout and "
                                  + "symbol resolution. JWT-secured, scope-gated."
            },
            ["servers"] = new object[] { new Dictionary<string, object?> { ["url"] = "/api/calendar/v1" } },
            ["components"] = new Dictionary<string, object?>
            {
                ["securitySchemes"] = new Dictionary<string, object?>
                {
                    ["bearer"] = new Dictionary<string, object?>
                    {
                        ["type"] = "http", ["scheme"] = "bearer", ["bearerFormat"] = "JWT"
                    }
                }
            },
            ["paths"] = new Dictionary<string, object?>
            {
                ["/token"] = new Dictionary<string, object?>
                {
                    ["post"] = new Dictionary<string, object?>
                    {
                        ["summary"] = "Exchange a client id + secret for a short-lived JWT",
                        ["responses"] = new Dictionary<string, object?> { ["200"] = new Dictionary<string, object?> { ["description"] = "OK" } }
                    }
                },
                ["/events"] = Get("Events in a window (upcoming or historical); point-in-time via asOf", "calendar:read"),
                ["/events/{id}"] = Get("One event with its full revision chain, surprise and affected symbols", "calendar:read"),
                ["/events/batch"] = new Dictionary<string, object?>
                {
                    ["post"] = new Dictionary<string, object?>
                    {
                        ["summary"] = "Multiplex several event queries in one round-trip",
                        ["security"] = new object[] { new Dictionary<string, object?> { ["bearer"] = new[] { "calendar:read" } } },
                        ["responses"] = new Dictionary<string, object?> { ["200"] = new Dictionary<string, object?> { ["description"] = "OK" } }
                    }
                },
                ["/history"] = Get("Deep historical events for a series (>=10y)", "calendar:read"),
                ["/series"] = Get("Catalog of tracked indicators", "calendar:read"),
                ["/surprises"] = Get("Actual/forecast/surprise z-score history", "calendar:surprises"),
                ["/next"] = Get("Next relevant release for a symbol", "calendar:read"),
                ["/blackout"] = Get("Is a symbol inside a high-impact news window", "calendar:blackout"),
                ["/affected-symbols"] = Get("Resolve an event to affected watchlist symbols", "calendar:read"),
                ["/health"] = Get("Per-source freshness and coverage", "calendar:read")
            }
        };
    }
}
