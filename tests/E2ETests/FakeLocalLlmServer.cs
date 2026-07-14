using System.Net;
using System.Text;

namespace E2ETests;

// A minimal in-process OpenAI-compatible endpoint serving POST /v1/chat/completions with a canned,
// deterministic reply. Wire-identical to Ollama / LM Studio / vLLM / llama.cpp, so it stands in for a
// local LLM in E2E: the app-under-test (a separate process) reaches it over 127.0.0.1 and every AI
// feature returns the canned text — proving the whole path UI → endpoint → provider adapter → back.
public sealed class FakeLocalLlmServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly string _reply;
    private readonly CancellationTokenSource _cts = new();

    public FakeLocalLlmServer(string reply)
    {
        _reply = reply;
        _alertJson = BuildAlertJson(reply);
        _agentJson = BuildAgentJson(reply);
        Port = GetFreePort();
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _listener.Start();
        _ = Task.Run(LoopAsync);
    }

    public int Port { get; }
    public string BaseUrl => $"http://127.0.0.1:{Port}/v1/";

    // Marker from AiPrompts.CurrencyForwardSystem — the currency-strength gather is the one AI call whose
    // reply must be structured JSON (a plain canned string parses to no trajectories, so no snapshot is
    // ever produced and the page renders empty). We reply with a valid forward-gather payload for the major
    // currencies so the deterministic model produces a snapshot; every other call still gets the canned reply.
    private const string CurrencyGatherMarker = "deterministic currency-strength model";

    private static readonly string CurrencyGatherJson = BuildCurrencyGatherJson();

    // Marker from AiPrompts.AlertSystem — the market-watch alert call whose reply must be a structured
    // alert JSON (the plain canned string parses to no alert, so the worker raises nothing). We reply with
    // alert=true and embed the canned reply in the message so the raised AlertEvent carries it. Every other
    // call still gets the canned reply.
    private const string AlertMarker = "alerting agent";

    // Marker from AiPrompts.AgentSystem — the portfolio-agent proposal call whose reply must be a
    // structured agent-action JSON ({reasoning, name, parameters}); the plain canned string parses to no
    // action, so the worker records no proposal. The reasoning embeds the canned reply so the persisted
    // AgentProposal carries it. Every other call still gets the canned reply.
    private const string AgentMarker = "autonomous trading portfolio agent";

    private readonly string _alertJson;
    private readonly string _agentJson;

    private async Task LoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch { return; }

            string requestBody;
            using (var reader = new System.IO.StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                requestBody = await reader.ReadToEndAsync();

            var content = requestBody.Contains(CurrencyGatherMarker, StringComparison.Ordinal) ? CurrencyGatherJson
                : requestBody.Contains(AlertMarker, StringComparison.Ordinal) ? _alertJson
                : requestBody.Contains(AgentMarker, StringComparison.Ordinal) ? _agentJson
                : _reply;

            var body = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"" + content + "\"}}]}";
            var bytes = Encoding.UTF8.GetBytes(body);
            ctx.Response.ContentType = "application/json";
            ctx.Response.StatusCode = (int)HttpStatusCode.OK;
            ctx.Response.ContentLength64 = bytes.Length;
            try { await ctx.Response.OutputStream.WriteAsync(bytes); } catch { /* client gone */ }
            ctx.Response.Close();
        }
    }

    // A minimal, deterministic forward-gather payload covering the majors, JSON-escaped for embedding in the
    // OpenAI chat "content" string. Only code + trajectory.ratePathBp are required by CurrencyMacroParser.
    private static string BuildCurrencyGatherJson()
    {
        string[] majors = ["USD", "EUR", "GBP", "JPY", "AUD", "CAD", "CHF", "NZD"];
        var entries = string.Join(",", majors.Select((code, i) =>
            "{\\\"code\\\": \\\"" + code + "\\\", \\\"trajectory\\\": {\\\"ratePathBp\\\": " + (25 - i * 5) +
            ", \\\"inflationTrend\\\": -0.2, \\\"growthMomentum\\\": 0.1, \\\"geopoliticalDelta\\\": 0.0}, " +
            "\\\"dataConfidence\\\": \\\"Medium\\\"}"));
        return "{\\\"currencies\\\": [" + entries + "]}";
    }

    // A valid alert-assessment payload (alert raised), JSON-escaped for embedding in the OpenAI chat
    // "content" string. The message embeds the canned reply so the raised AlertEvent carries a marker the
    // E2E can assert on.
    private static string BuildAlertJson(string reply) =>
        "{\\\"alert\\\": true, \\\"severity\\\": \\\"warning\\\", \\\"message\\\": \\\"" + reply + " market update\\\"}";

    // A valid agent-action payload, JSON-escaped for embedding in the OpenAI chat "content" string. The
    // reasoning embeds the canned reply so the persisted AgentProposal carries a marker the E2E asserts on.
    private static string BuildAgentJson(string reply) =>
        "{\\\"reasoning\\\": \\\"" + reply + " tighten the stop\\\", \\\"name\\\": \\\"opt-1\\\", " +
        "\\\"parameters\\\": {\\\"period\\\": 20}}";

    private static int GetFreePort()
    {
        var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _listener.Stop(); } catch { /* best effort */ }
        _listener.Close();
        _cts.Dispose();
    }
}
