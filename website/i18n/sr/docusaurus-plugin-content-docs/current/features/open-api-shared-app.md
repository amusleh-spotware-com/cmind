---
description: "Isporucite jednu cTrader Open API aplikaciju za svakog korisnika (white-label shared rezim), jedinstveni redirect URL za registraciju, i per-message-type client rate limits."
---

# Deljena Open API aplikacija i rate limits

Podrazumevano svaki korisnik registrira **sopstvenu** cTrader Open API aplikaciju under
**Settings → Open API**. White-label operator (tipicno cTrader broker ili reseller) can instead
ship **one shared Open API application for all users** — niko ne registrira sopstvenu; everyone
authorizes their accounts kroz operatorovu jednu aplikaciju.

## Two ways to provide the shared application

The shared application is provisioned either from deployment config **or** from the owner settings UI
(the owner-set value wins). Provide it once and shared-mode turns on for everyone.

### 1. Deployment config (seeded on startup)

```jsonc
"App": {
  "OpenApi": {
    "PublicBaseUrl": "https://cmind.yourbroker.com",   // canonical public URL of THIS deployment
    "SharedApp": {
      "Enabled": true,
      "Name": "YourBroker Open API",
      "ClientId": "1234_abcd...",
      "ClientSecret": "…"                                // encrypted at rest; never logged
    }
  }
}
```

On startup the app seeds one shared application owned by the owner account (idempotent — it never
overwrites an owner-edited runtime value, and re-seeding is a no-op).

### 2. Owner settings (runtime, no redeploy)

**Settings → Open API** (owner only) shows a **Deployment shared application** card: add / edit /
delete the shared app, with the redirect URL displayed for copy-paste. Changes take effect for new
authorizations immediately.

## The redirect URL (register this in cTrader)

Every cTrader Open API application registers **one** redirect URL — the **same single value** for the
shared app and for any per-user app:

```
{your deployment URL}/openapi/callback
```

for example `https://cmind.yourbroker.com/openapi/callback`.

- The app **displays the exact value** on the Open API settings page (with a copy button) — paste it
  into the cTrader partner portal when you create the Open API application.
- It is composed from `App:OpenApi:PublicBaseUrl` so it stays stable behind a reverse proxy / CDN;
  when that is unset it falls back to the inbound request host.
- The invite vs normal-user experience differs only in where the user lands **after** the callback
  (their accounts list vs an "accounts added" confirmation) — the registered redirect URL is unchanged.

## What users see under shared mode

When a shared application exists:

- Users get **no option** to register their own Open API application — the settings page shows
  **"Open API is managed by your provider"** and an **Authorize accounts** button that uses the shared
  app.
- Any pre-existing personal applications are **removed**; their authorized accounts are re-pointed to
  the shared app and must be **re-authorized** (their old tokens were issued under a different client
  id). Attempting to create a personal app returns a "managed by your provider" error.

## Client rate limits (per message type)

The client paces outbound cTrader Open API messages so a burst never trips a server-side rate-limit
block. Limits are **per message type**, matching the cTrader Open API docs:

| Category | What it covers | Default |
|---|---|---|
| `General` | trading + read messages (orders, symbols, account queries) | 45 msg/s |
| `HistoricalData` | trendbar / tick-data requests (throttled harder by cTrader) | 5 msg/s |

A historical-data request counts against **both** its own bucket and the general bucket. Heartbeat and
authentication messages are never paced. Messages queue and drain at the available rate — nothing is
dropped and order is preserved.

Tune them if your broker negotiated **higher** cTrader limits, or set a category to **`0`** to disable
pacing entirely (unlimited):

- **Config:** `App:OpenApi:RateLimits:General` / `App:OpenApi:RateLimits:HistoricalData` (msgs/sec).
- **Owner settings:** the **Client rate limits** card on **Settings → Open API** (owner override wins,
  applies to new connections / on reconnect).
