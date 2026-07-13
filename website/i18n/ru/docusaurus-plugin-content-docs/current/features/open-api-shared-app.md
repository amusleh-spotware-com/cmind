---
description: "Ship один cTrader Open API application для каждого пользователя (white-label shared mode), единый redirect URL для регистрации, и per-message-type client rate limits."
---

# Shared Open API application & rate limits

По умолчанию каждый пользователь регистрирует **свой собственный** cTrader Open API application под
**Settings → Open API**. White-label оператор (typically cTrader broker или reseller) может instead
ship **один shared Open API application для всех пользователей** — никто не регистрирует свой;
все authorizуют свои счета через единый app оператора.

## Два способа предоставить shared application

Shared application провизионируется либо из deployment config **либо** из owner settings UI
(owner-set value wins). Provide it once и shared-mode включается для всех.

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

При старте приложение seed'ит один shared application owned by owner account (idempotent — never
overwrites owner-edited runtime value, и re-seeding это no-op).

### 2. Owner settings (runtime, no redeploy)

**Settings → Open API** (owner only) показывает **Deployment shared application** card: add / edit /
delete shared app, с отображённым redirect URL для copy-paste. Изменения применяются для новых
authorizations немедленно.

## Redirect URL (зарегистрируйте это в cTrader)

Каждое cTrader Open API application регистрирует **один** redirect URL — **тот же самое значение** для
shared app и для любого per-user app:

```
{your deployment URL}/openapi/callback
```

например `https://cmind.yourbroker.com/openapi/callback`.

- Приложение **отображает точное значение** на Open API settings page (с кнопкой копирования) — paste it
  into the cTrader partner portal при создании Open API application.
- Он составляется из `App:OpenApi:PublicBaseUrl` поэтому остаётся стабильным behind reverse proxy / CDN;
  когда unset, fallback'ит к inbound request host.
- Invite vs normal-user experience различается только где пользователь lands **after** callback
  (their accounts list vs "accounts added" confirmation) — зарегистрированный redirect URL unchanged.

## Что пользователи видят в shared mode

Когда shared application существует:

- Пользователи **не имеют опции** регистрировать свой собственный Open API application — settings page показывает
  **"Open API is managed by your provider"** и кнопку **Authorize accounts**, использующую shared app.
- Любые pre-existing personal applications **удаляются**; их authorized accounts re-pointed к
  shared app и должны быть **re-authorized** (their old tokens were issued under different client
  id). Attempting to create a personal app returns "managed by your provider" error.

## Client rate limits (per message type)

Клиент ограничивает outbound cTrader Open API messages so a burst никогда не trips server-side rate-limit
block. Лимиты **per message type**, matching cTrader Open API docs:

| Category | Что покрывает | Default |
|---|---|---|
| `General` | trading + read messages (orders, symbols, account queries) | 45 msg/s |
| `HistoricalData` | trendbar / tick-data requests (throttled harder by cTrader) | 5 msg/s |

A historical-data request counts against **both** its own bucket и the general bucket. Heartbeat и
authentication messages never paced. Messages queue и drain at available rate — nothing dropped и order preserved.

Tune их если ваш broker negotiated **higher** cTrader limits, или установите category в **`0`** to disable
pacing entirely (unlimited):

- **Config:** `App:OpenApi:RateLimits:General` / `App:OpenApi:RateLimits:HistoricalData` (msgs/sec).
- **Owner settings:** **Client rate limits** card on **Settings → Open API** (owner override wins,
  applies to new connections / on reconnect).
