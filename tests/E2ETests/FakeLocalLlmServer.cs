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

            var body = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"" + _reply + "\"}}]}";
            var bytes = Encoding.UTF8.GetBytes(body);
            ctx.Response.ContentType = "application/json";
            ctx.Response.StatusCode = (int)HttpStatusCode.OK;
            ctx.Response.ContentLength64 = bytes.Length;
            try { await ctx.Response.OutputStream.WriteAsync(bytes); } catch { /* client gone */ }
            ctx.Response.Close();
        }
    }

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
