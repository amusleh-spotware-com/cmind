using System.Net;
using System.Net.Sockets;
using Core.Constants;
using Core.Domain;

namespace Core.Ai;

/// <summary>
/// Base URL of an AI provider endpoint. Must be an absolute URI. Plaintext <c>http://</c> is allowed
/// only for loopback / private (intranet) hosts — the local-LLM case (Ollama, LM Studio, vLLM, an
/// on-prem box) — so an API key is never sent in the clear to a public host; anything routable on the
/// public internet must be <c>https</c>.
/// </summary>
public readonly record struct AiEndpoint
{
    public string Value { get; }

    public AiEndpoint(string value)
    {
        var trimmed = DomainGuard.AgainstNullOrWhiteSpace(value, DomainErrors.AiEndpointInvalid);
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new DomainException(DomainErrors.AiEndpointInvalid);

        if (uri.Scheme == Uri.UriSchemeHttp && !IsLocalOrPrivate(uri))
            throw new DomainException(DomainErrors.AiEndpointInsecure);

        // Preserve a trailing slash so relative wire paths (v1/chat/completions) resolve under the base.
        Value = trimmed.EndsWith('/') ? trimmed : trimmed + "/";
    }

    public Uri ToUri() => new(Value);

    /// <summary>True when the endpoint targets loopback or a private/intranet host — i.e. a local /
    /// self-hosted provider (Ollama, LM Studio, vLLM, an on-prem box) rather than a public cloud.</summary>
    public bool IsLocal => IsLocalOrPrivate(ToUri());

    public override string ToString() => Value;

    private static bool IsLocalOrPrivate(Uri uri)
    {
        var host = uri.Host;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
        // A single-label host (no dots) is an intranet name, not a public FQDN.
        if (!host.Contains('.') && !host.Contains(':')) return true;

        if (!IPAddress.TryParse(host, out var ip)) return false;
        if (IPAddress.IsLoopback(ip)) return true;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] == 10
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 169 && b[1] == 254);
        }

        return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal
            || (ip.AddressFamily == AddressFamily.InterNetworkV6 && (ip.GetAddressBytes()[0] & 0xFE) == 0xFC);
    }
}
