---
description: "Ship one cTrader Open API application สำหรับ ทุก user (white-label shared mode) single redirect URL เพื่อ register และ per-message-type client rate limits"
---

# Shared Open API application & rate limits

By default ทุก user registers ของพวกเขา **own** cTrader Open API application ภายใต้ **Settings → Open API** white-label operator (typically cTrader broker หรือ reseller) สามารถ แทนที่ ship **one shared Open API application สำหรับ ทุก users** — ไม่มี one registers ของพวกเขา; ทุก one authorizes accounts ของพวกเขา ผ่าน operator ของ single app

## Two ways เพื่อ provide shared application

shared application ได้รับ provisioned จาก deployment config **หรือ** จาก owner settings UI (owner-set value wins) Provide มันครั้งเดียว และ shared-mode เปิด สำหรับ ทุก one

### 1 Deployment config (seeded บน startup)

```jsonc
"App": {
  "OpenApi": {
    "PublicBaseUrl": "https://cmind.yourbroker.com",   // canonical public URL ของ THIS deployment
    "SharedApp": {
      "Enabled": true,
      "Name": "YourBroker Open API",
      "ClientId": "1234_abcd...",
      "ClientSecret": "…"                                // encrypted ที่ rest; ไม่เคย logged
    }
  }
}
```

บน startup app seeds one shared application owned โดย owner account (idempotent — มัน ไม่เคย overwrites owner-edited runtime value และ re-seeding เป็น no-op)

### 2 Owner settings (runtime ไม่มี redeploy)

**Settings → Open API** (owner เท่านั้น) แสดง สอง สิ่ง: **Your Open API application** section — owner registers edits และ authorizes **own** per-user app ของพวกเขา เหมือนกับ user ใดก็ได้ (available ขณะที่ ไม่มี shared app configured) — และ **Deployment shared application** card เพื่อ add / edit / delete shared app ด้วย redirect URL displayed สำหรับ copy-paste Changes take effect สำหรับ new authorizations ทันที Once shared app configured มัน supersedes owner's own app และ **Your Open API application** section switches ไป notice ที่ accounts ปัจจุบัน authorize ผ่าน shared app

## redirect URL (register นี้ ใน cTrader)

ทุก cTrader Open API application registers **one** redirect URL — the **same single value** สำหรับ shared app และ สำหรับ any per-user app:

```
{your deployment URL}/openapi/callback
```

สำหรับ example `https://cmind.yourbroker.com/openapi/callback`

- app **displays exact value** บน Open API settings page (ด้วย copy button) — paste มัน เข้าไป cTrader partner portal เมื่อ คุณ create Open API application
- มัน composed จาก `App:OpenApi:PublicBaseUrl` ดังนั้น มัน stays stable behind reverse proxy / CDN; เมื่อ นั่น unset มัน falls back ไป inbound request host
- invite vs normal-user experience differs เฉพาะ ใน ที่ user lands **after** callback (accounts list ของพวกเขา vs "accounts added" confirmation) — registered redirect URL ไม่เปลี่ยนแปลง

## What users see ภายใต้ shared mode

เมื่อ shared application exists:

- Users get **ไม่มี option** เพื่อ register ของพวกเขา Open API application — settings page shows **"Open API ได้รับ managed โดย provider ของคุณ"** และ **Authorize accounts** button ที่ ใช้ shared app
- Any pre-existing personal applications จะ **removed**; ของพวกเขา authorized accounts จะ re-pointed ไป shared app และ ต้อง **re-authorized** (ของพวกเขา old tokens ได้รับ issued ภายใต้ ต่างไป client id) attempting เพื่อ create personal app returns "managed โดย provider ของคุณ" error

## Client rate limits (per message type)

client paces outbound cTrader Open API messages ดังนั้น burst ไม่เคย trips server-side rate-limit block Limits เป็น **per message type** matching cTrader Open API docs:

| Category | What it covers | Default |
|---|---|---|
| `General` | trading + read messages (orders symbols account queries) | 45 msg/s |
| `HistoricalData` | trendbar / tick-data requests (throttled harder โดย cTrader) | 5 msg/s |

historical-data request counts ผ่าน **both** bucket ของมัน และ general bucket Heartbeat และ authentication messages ไม่เคย paced Messages queue และ drain ที่ available rate — ไม่มี อะไร dropped และ order preserved

Tune พวกเขา ถ้า broker ของคุณ negotiated **higher** cTrader limits หรือ set category ไป **`0`** เพื่อ disable pacing ทั้งหมด (unlimited):

- **Config:** `App:OpenApi:RateLimits:General` / `App:OpenApi:RateLimits:HistoricalData` (msgs/sec)
- **Owner settings:** **Client rate limits** card บน **Settings → Open API** (owner override wins applies ไป new connections / บน reconnect)
