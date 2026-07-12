namespace Core.Accounts;

/// <summary>
/// The wire contract between the broker-probe cBot and the verifier: the cBot prints
/// <c>##CMIND-BROKER##&lt;name&gt;##END##</c> on start. This pure parser extracts the broker name from a
/// log line (and recognises a login failure) so the container orchestration stays free of parsing rules
/// and the contract is unit-tested in one place. Keep in sync with <c>tools/broker-probe/BrokerProbeBot.cs</c>.
/// </summary>
public static class BrokerProbeOutput
{
    public const string StartMarker = "##CMIND-BROKER##";
    public const string EndMarker = "##END##";

    /// <summary>Extracts the broker name from a probe log line, or false when the line is not a broker marker.</summary>
    public static bool TryParseBroker(string line, out BrokerName broker)
    {
        broker = default;
        if (string.IsNullOrEmpty(line)) return false;

        var start = line.IndexOf(StartMarker, StringComparison.Ordinal);
        if (start < 0) return false;
        var valueStart = start + StartMarker.Length;
        var end = line.IndexOf(EndMarker, valueStart, StringComparison.Ordinal);
        if (end < 0) return false;

        var value = line[valueStart..end].Trim();
        if (string.IsNullOrWhiteSpace(value)) return false;

        broker = new BrokerName(value);
        return true;
    }

    /// <summary>True when the probe output indicates the credentials could not sign in.</summary>
    public static bool IndicatesLoginFailure(string output) =>
        !string.IsNullOrEmpty(output)
        && (output.Contains("authentication failed", StringComparison.OrdinalIgnoreCase)
            || output.Contains("invalid credentials", StringComparison.OrdinalIgnoreCase)
            || output.Contains("login failed", StringComparison.OrdinalIgnoreCase)
            || output.Contains("could not authenticate", StringComparison.OrdinalIgnoreCase));
}
