using System.Net;
using System.Text;

namespace IntegrationTests;

// A minimal in-process OpenAI-compatible endpoint: serves POST /v1/chat/completions with a canned,
// deterministic reply. The wire is byte-identical to Ollama / LM Studio / vLLM / llama.cpp, so it is the
// "local LLM" stand-in every CI run uses — zero external dependency, fully deterministic. Reachable by
// both the in-process provider (integration) and a separate web process (E2E) via 127.0.0.1.
public sealed class FakeLocalLlmServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly string _reply;
    private readonly CancellationTokenSource _cts = new();

    public FakeLocalLlmServer(string reply = "LOCAL-LLM-OK")
    {
        _reply = reply;
        Port = GetFreePort();
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _listener.Start();
        _ = Task.Run(LoopAsync);
    }

    public int Port { get; }
    public string BaseUrl => $"http://127.0.0.1:{Port}/v1/";

    private async Task LoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch { return; }

            // GET /v1/models — the OpenAI-compatible model-discovery endpoint (LM Studio / Ollama / vLLM
            // all serve it). Return a deterministic single-model list for the catalog/probe tests.
            var body = ctx.Request.Url?.AbsolutePath.EndsWith("/models", StringComparison.Ordinal) == true
                ? ModelsListJson
                : "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"" + _reply + "\"}}]}";
            var bytes = Encoding.UTF8.GetBytes(body);
            ctx.Response.ContentType = "application/json";
            ctx.Response.StatusCode = (int)HttpStatusCode.OK;
            ctx.Response.ContentLength64 = bytes.Length;
            await ctx.Response.OutputStream.WriteAsync(bytes);
            ctx.Response.Close();
        }
    }

    // The single model id this fake advertises on GET /v1/models.
    public const string AdvertisedModelId = "fake-local-model";

    private const string ModelsListJson =
        "{\"object\":\"list\",\"data\":[{\"id\":\"" + AdvertisedModelId + "\",\"object\":\"model\"}]}";

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
