---
description: "Ship một cTrader Open API application cho every user (white-label shared mode), single redirect URL to register, và per-message-type client rate limits."
---

# Shared Open API application & rate limits

Theo mặc định every user registers **own** cTrader Open API application under
**Settings → Open API**. A white-label operator (thường là cTrader broker hoặc reseller) có thể instead
ship **một shared Open API application cho tất cả users** — không ai register own; everyone
authorizes their accounts through operator's single app.

## Hai cách để provide shared application

Shared application provisioned either from deployment config **or** from owner settings UI
(owner-set value wins). Provide it once và shared-mode turns on for everyone.

### 1. Deployment config (seeded on startup)

```jsonc
"App": {
  "OpenApi": {
    "PublicBaseUrl": "https://cmind.yourbroker.com",   // canonical public URL của THIS deployment
    "SharedApp": {
      "Enabled": true,
      "Name": "YourBroker Open API",
      "ClientId": "1234_abcd...",
      "ClientSecret": "…"                                // encrypted at rest; never logged
    }
  }
}
```

On startup app seeds một shared application owned by owner account (idempotent — nó never
overwrites an owner-edited runtime value, và re-seeding là no-op).

### 2. Owner settings (runtime, no redeploy)

**Settings → Open API** (owner only) shows **Deployment shared application** card: add / edit /
delete shared app, với redirect URL displayed for copy-paste. Changes take effect cho new
authorizations immediately.

## The redirect URL (register this in cTrader)

Mỗi cTrader Open API application registers **một** redirect URL — **same single value** cho
shared app và cho any per-user app:

```
{your deployment URL}/openapi/callback
```

ví dụ `https://cmind.yourbroker.com/openapi/callback`.

- App **displays exact value** on Open API settings page (với copy button) — paste nó
  into cTrader partner portal when you create Open API application.
- Nó composed from `App:OpenApi:PublicBaseUrl` vì vậy nó stays stable behind reverse proxy / CDN;
  when unset nó falls back to inbound request host.
- Invite vs normal-user experience differs only in where user lands **after** callback
  (their accounts list vs "accounts added" confirmation) — registered redirect URL unchanged.

## What users see under shared mode

When a shared application exists:

- Users get **no option** to register their own Open API application — settings page shows
  **"Open API is managed by your provider"** và **Authorize accounts** button sử dụng shared app.
- Any pre-existing personal applications are **removed**; their authorized accounts re-pointed to
  shared app và must be **re-authorized** (their old tokens were issued under different client
  id). Attempting to create personal app returns "managed by your provider" error.

## Client rate limits (per message type)

Client paces outbound cTrader Open API messages sao một burst không bao giờ trips server-side rate-limit
block. Limits là **per message type**, matching cTrader Open API docs:

| Category | What it covers | Default |
|---|---|---|
| `General` | trading + read messages (orders, symbols, account queries) | 45 msg/s |
| `HistoricalData` | trendbar / tick-data requests (throttled harder by cTrader) | 5 msg/s |

A historical-data request counts against **both** its own bucket và general bucket. Heartbeat và
authentication messages never paced. Messages queue và drain at available rate — không có gì dropped
và order preserved.

Tune them nếu broker đã negotiate **higher** cTrader limits, hoặc set a category to **`0`** to disable
pacing entirely (unlimited):

- **Config:** `App:OpenApi:RateLimits:General` / `App:OpenApi:RateLimits:HistoricalData` (msgs/sec).
- **Owner settings:** **Client rate limits** card on **Settings → Open API** (owner override wins,
  applies to new connections / on reconnect).
