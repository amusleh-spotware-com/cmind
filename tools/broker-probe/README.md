# Broker-probe cBot

A tiny cTrader cBot cMind runs to discover an account's **real broker name**, so a white-label
deployment can enforce its `App:Accounts:AllowedBrokers` allowlist when a user adds an account by
manual cID (username / password). See the [white-label docs](../../website/docs/features/white-label.md#broker-allowlist).

On start the cBot prints:

```
##CMIND-BROKER##<Account.BrokerName>##END##
```

then stops. `Core.Accounts.BrokerProbeOutput` parses that line; `Web.Accounts.BrokerVerifier` runs the
container on the web host and reads it back. **Keep the marker in sync** with `BrokerProbeOutput`.

## Building the `.algo`

The compiled `broker-probe.algo` is not committed (it is a cTrader build artifact that requires the
cTrader Automate reference). Build it with the cTrader CLI / cTrader Automate SDK, e.g.:

```bash
# from a project referencing cTrader.Automate, with BrokerProbeBot.cs as the source:
dotnet build -c Release   # produces broker-probe.algo
```

Then place the resulting `broker-probe.algo` at the path configured by
`App:Accounts:BrokerProbeAlgoPath` (default: `broker-probe/broker-probe.algo`, resolved on the web
host that has the Docker socket).

## Behaviour when the algo is absent

If the algo file is missing, manual-cID broker verification **fails closed** (the account is not
added and the user sees a "couldn't verify the broker" notification). Accounts under a restricted
allowlist can still be linked via the **Open API (OAuth)** path, which reads the broker name directly
from the cTrader Open API and needs no probe.
