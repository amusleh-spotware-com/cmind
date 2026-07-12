using System.Text;
using Core;
using Core.Accounts;
using Core.Constants;
using Core.Options;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Web.Accounts;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Exercises the REAL broker probe end-to-end: builds a container from the shipped broker-probe .algo,
/// logs in with real cID credentials via the cTrader CLI, and reads the account's broker name back.
/// Skips cleanly when the credentials/algo are not provided (needs Docker + the ctrader-console image +
/// a live account), so CI stays green while the path is still covered when a dev supplies secrets.
///
/// Provide: BROKER_PROBE_CID_USER, BROKER_PROBE_CID_PWD, BROKER_PROBE_ACCOUNT, BROKER_PROBE_ALGO
/// (path to broker-probe.algo). Optional: BROKER_PROBE_LIVE=1, BROKER_PROBE_EXPECT (expected broker).
/// </summary>
public sealed class BrokerVerifierLiveTests
{
    [Fact]
    public async Task Real_probe_reads_the_account_broker_when_credentials_are_provided()
    {
        var user = Environment.GetEnvironmentVariable("BROKER_PROBE_CID_USER");
        var password = Environment.GetEnvironmentVariable("BROKER_PROBE_CID_PWD");
        var accountRaw = Environment.GetEnvironmentVariable("BROKER_PROBE_ACCOUNT");
        var algoPath = Environment.GetEnvironmentVariable("BROKER_PROBE_ALGO");
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password)
            || !long.TryParse(accountRaw, out var account)
            || string.IsNullOrWhiteSpace(algoPath) || !File.Exists(algoPath))
            return; // secrets/algo absent — skip cleanly (never a reason to fail CI)

        var isLive = string.Equals(Environment.GetEnvironmentVariable("BROKER_PROBE_LIVE"), "1", StringComparison.Ordinal);
        var options = new StaticOptionsMonitor<AppOptions>(new AppOptions
        {
            Accounts = new AccountsOptions { BrokerProbeAlgoPath = algoPath }
        });
        var verifier = new BrokerVerifier(
            new PassthroughSecretProtector(), options, TimeProvider.System, NullLogger<BrokerVerifier>.Instance);

        var encryptedPassword = Encoding.UTF8.GetBytes(password); // passthrough protector => Unprotect is identity
        var result = await verifier.VerifyAsync(
            new BrokerProbeRequest(user!, encryptedPassword, account, isLive), CancellationToken.None);

        result.Success.Should().BeTrue($"probe failed with {result.Error}");
        result.Broker!.Value.Value.Should().NotBeNullOrWhiteSpace();

        var expected = Environment.GetEnvironmentVariable("BROKER_PROBE_EXPECT");
        if (!string.IsNullOrWhiteSpace(expected))
            result.Broker!.Value.Should().Be(new BrokerName(expected!));
    }

    private sealed class PassthroughSecretProtector : ISecretProtector
    {
        public byte[] Protect(ReadOnlySpan<byte> plaintext, string purpose) => plaintext.ToArray();
        public byte[] Unprotect(ReadOnlySpan<byte> ciphertext, string purpose) => ciphertext.ToArray();
        public string ProtectString(string plaintext, string purpose) => plaintext;
        public string UnprotectString(string ciphertext, string purpose) => ciphertext;
    }
}
