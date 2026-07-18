---
description: "Ship jeden cTrader Open API application dla każdy user (white-label shared mode), single redirect URL do register, i per-message-type client rate limits."
---

# Shared Open API application & rate limits

Domyślnie każdy user registers swoje **własne** cTrader Open API application pod
**Settings → Open API**. White-label operator (typowo cTrader broker lub reseller) może zamiast
ship **jeden shared Open API application dla wszystkie users** — nikt nie registers swoje own; everyone
authorizes ich accounts przez operator's single app.

## Dwie ways do provide shared application

Shared application to provisioned albo z deployment config **lub** z owner settings UI
(owner-set value wins). Provide to raz i shared-mode turns on dla everyone.

### 1. Deployment config (seeded na startup)

```jsonc
"App": {
  "OpenApi": {
    "PublicBaseUrl": "https://cmind.yourbroker.com",   // canonical public URL z THIS deployment
    "SharedApp": {
      "Enabled": true,
      "Name": "YourBroker Open API",
      "ClientId": "1234_abcd...",
      "ClientSecret": "…"                                // encrypted na rest; nigdy logged
    }
  }
}
```

Na startup app seeds jeden shared application owned przez owner account (idempotent — nigdy
nie overwrites owner-edited runtime value, i re-seeding to no-op).

### 2. Owner settings (runtime, no redeploy)

**Settings → Open API** (owner tylko) shows dwie rzeczy: sekcja **Your Open API application** — owner registers, edits, i authorizes swoje **własne** per-user app dokładnie jak każdy user (dostępne podczas gdy żaden shared app nie to configured) — i **Deployment shared application** card do add / edit / delete shared app, z redirect URL displayed dla copy-paste. Changes take effect dla new authorizations immediately. Raz gdy shared app to configured supersedes to owner's own app, i sekcja **Your Open API application** switches do notice że accounts now authorize przez shared app.

## Redirect URL (register to w cTrader)

Każdy cTrader Open API application registers **jeden** redirect URL — **same single value** dla
shared app i dla każdy per-user app:

```
{your deployment URL}/openapi/callback
```

na przykład `https://cmind.yourbroker.com/openapi/callback`.

- App **displays dokładny value** na Open API settings page (z copy button) — paste to
  do cTrader partner portal gdy create Open API application.
- To to composed z `App:OpenApi:PublicBaseUrl` więc stays stable behind reverse proxy / CDN;
  gdy to to unset falls back do inbound request host.
- Invite vs normal-user experience differs tylko w gdzie user lands **after** callback
  (ich accounts list vs "accounts added" confirmation) — registered redirect URL to unchanged.

## Co users see pod shared mode

Gdy shared application exists:

- Users get **no option** do register swoje own Open API application — settings page shows
  **"Open API jest managed przez Twojego provider"** i **Authorize accounts** button że uses shared
  app.
- Każdy pre-existing personal applications są **removed**; ich authorized accounts są re-pointed do
  shared app i muszą być **re-authorized** (ich old tokens były issued pod different client
  id). Attempting do create personal app returns "managed przez Twojego provider" error.

## Client rate limits (per message type)

Client paces outbound cTrader Open API messages więc burst nigdy nie trips server-side rate-limit
block. Limits to **per message type**, matching cTrader Open API docs:

| Category | Co covers | Domyślnie |
|---|---|---|
| `General` | trading + read messages (orders, symbols, account queries) | 45 msg/s |
| `HistoricalData` | trendbar / tick-data requests (throttled harder przez cTrader) | 5 msg/s |

Historical-data request counts przeciw **oba** jego own bucket i general bucket. Heartbeat i
authentication messages nigdy nie są paced. Messages queue i drain na available rate — nic nie
drops i order to preserved.

Tune je jeśli Twój broker negotiated **higher** cTrader limits, lub set category do **`0`** do disable
pacing entirely (unlimited):

- **Config:** `App:OpenApi:RateLimits:General` / `App:OpenApi:RateLimits:HistoricalData` (msgs/sec).
- **Owner settings:** **Client rate limits** card na **Settings → Open API** (owner override wins,
  applies do new connections / na reconnect).
