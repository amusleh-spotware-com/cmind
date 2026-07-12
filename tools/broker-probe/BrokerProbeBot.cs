using cAlgo.API;

namespace cMind.BrokerProbe;

// Broker-probe cBot. cMind runs this via the cTrader CLI (`run <algo> --ctid <user> --pwd-file <file>
// --account <n>`) to discover which broker a set of cID credentials + account number actually belongs
// to, so a white-label deployment can enforce its App:Accounts:AllowedBrokers list on manual-cID adds.
//
// On start it prints Account.BrokerName in the marker format the verifier reads, then stops. Keep the
// marker string in sync with Core.Accounts.BrokerProbeOutput (##CMIND-BROKER##...##END##).
[Robot(AccessRights = AccessRights.None, AddIndicators = false)]
public class BrokerProbeBot : Robot
{
    protected override void OnStart()
    {
        Print($"##CMIND-BROKER##{Account.BrokerName}##END##");
        Stop();
    }
}
