using System.Net;
using System.Text;
using Core.Ai;
using Core.Options;
using FluentAssertions;
using Infrastructure.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace UnitTests.Ai;

public sealed class BuiltInModelInstallerTests
{
    // Serves each requested file a tiny body; a missing optional file (added_tokens.json) 404s and is skipped.
    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsoluteUri.EndsWith("added_tokens.json", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            var name = request.RequestUri.Segments[^1];
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"content-of-{name}", Encoding.UTF8)
            });
        }
    }

    private static IHttpClientFactory Factory()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(new StubHandler()));
        return factory;
    }

    [Fact]
    public async Task Downloads_model_files_and_reports_installed()
    {
        var dir = Path.Combine(Path.GetTempPath(), "onnx-install-" + Guid.NewGuid().ToString("N"));
        var options = Substitute.For<IOptionsMonitor<AppOptions>>();
        options.CurrentValue.Returns(new AppOptions
        {
            Ai = new AiOptions
            {
                BuiltIn = new AiBuiltInOptions
                {
                    ModelPath = dir,
                    AutoDownload = true,
                    DownloadBaseUrl = "https://example.test/model/",
                    DownloadFiles = ["genai_config.json", "tokenizer.json", "added_tokens.json"]
                }
            }
        });

        var installer = new BuiltInModelInstaller(Factory(), options, NullLogger<BuiltInModelInstaller>.Instance);
        installer.IsInstalled().Should().BeFalse();

        try
        {
            installer.EnsureInstalling();
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (installer.State == BuiltInModelInstallState.Downloading && DateTime.UtcNow < deadline)
                await Task.Delay(50);

            installer.State.Should().Be(BuiltInModelInstallState.Installed);
            installer.IsInstalled().Should().BeTrue();
            File.Exists(Path.Combine(dir, "genai_config.json")).Should().BeTrue();
            File.Exists(Path.Combine(dir, "tokenizer.json")).Should().BeTrue();
            // The optional 404 file is skipped, not fatal.
            File.Exists(Path.Combine(dir, "added_tokens.json")).Should().BeFalse();
        }
        finally
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void Does_not_install_when_auto_download_disabled()
    {
        var dir = Path.Combine(Path.GetTempPath(), "onnx-noinstall-" + Guid.NewGuid().ToString("N"));
        var options = Substitute.For<IOptionsMonitor<AppOptions>>();
        options.CurrentValue.Returns(new AppOptions
        {
            Ai = new AiOptions { BuiltIn = new AiBuiltInOptions { ModelPath = dir, AutoDownload = false } }
        });

        var installer = new BuiltInModelInstaller(Factory(), options, NullLogger<BuiltInModelInstaller>.Instance);
        installer.EnsureInstalling();
        installer.State.Should().Be(BuiltInModelInstallState.NotStarted);
        installer.IsInstalled().Should().BeFalse();
    }
}
