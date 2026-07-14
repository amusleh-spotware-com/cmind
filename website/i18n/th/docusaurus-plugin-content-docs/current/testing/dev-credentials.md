---
description: "ทั้งหมด credentials ที่ชุด test ต้องอาศัยอยู่ในไฟล์ gitignored เดียว: secrets/dev-credentials.local.json คัดลอกแม่แบบที่ commit และเติมสิ่งที่คุณ"
---

# Dev credentials — ไฟล์เดียวสำหรับทุก test

ทั้งหมด credentials ที่ชุด test ต้องอาศัยอยู่ในไฟล์ gitignored เดียว: `secrets/dev-credentials.local.json` คัดลอกแม่แบบที่ commit และเติมสิ่งที่คุณมี — ทุกค่าเป็น optional และ tests ที่ต้องการค่าที่หายไปให้ข้ามอย่างสะอาด

```bash
mkdir -p secrets
cp dev-credentials.example.json secrets/dev-credentials.local.json
# edit secrets/dev-credentials.local.json
```

## สิ่งที่ test tier แต่ละอ่าน

| Tier | Needs | From |
|------|-------|------|
| **Unit** (`tests/UnitTests`) | ไม่มีอะไร | — deterministic ไม่มี secrets ไม่มี network |
| **Integration** (`tests/IntegrationTests`) | Postgres | Testcontainers (Docker) — auto |
| **Live copy** (`tests/IntegrationTests/CopyLive`) | OpenAPI app + token cache | `OpenApi.App`, `OpenApi.Tokens` |
| **E2E onboarding** (`tests/E2ETests/CopyLive`) | OpenAPI app + cID logins | `OpenApi.App`, `OpenApi.Cids` |
| **E2E real run/backtest** (`CBotRealRunBacktestTests`) | cID login + **demo** account number | `OpenApi.Cids[].{Username,Password,Accounts}` |
| **AI features** | Anthropic key | `Ai.ApiKey` (unset ⇒ AI features return disabled app ยังคงทำงาน) |
| **Live economic-calendar sources** (`tests/IntegrationTests/Calendar/CalendarSourceLiveTests`) | FRED / BLS API keys | `Calendar.FredApiKey`, `Calendar.BlsApiKey` (unset ⇒ that source's live test skips; the keyless central-bank schedule still works) |

## Schema

ดู `dev-credentials.example.json` ที่ repo root Sections:

- `OpenApi.App` — `{ ClientId, ClientSecret }` ของ cTrader Open API application
- `OpenApi.Cids` — cTrader ID logins ที่ใช้โดย headless OAuth onboarding แต่ละรายการยังมี **`Accounts`** array — cTrader trading-account numbers (login/account number เช่น `3635817`) ภายใต้ cID นั้นที่ test infrastructure ได้รับอนุญาต link เข้าไปในแอป และ drive `CBotRealRunBacktestTests` reads รายการแรกที่มี non-empty `Accounts` array เพิ่มว่า cID + account ไปเข้าไปในแอป แล้ว really runs และ backtests cBot บน **Put only demo account numbers here** — ไม่เคย live account; run/backtest tests place real orders บน account ใด ๆ ที่คุณเรียก Empty/omitted `Accounts` ⇒ real run/backtest test ข้ามอย่างสะอาด
- `OpenApi.Tokens` — multi-cID token cache (รายการเดียวต่อ cID ที่ authorize ด้วย refresh/access token + account list) เขียน automatically โดย onboarding และโดย token-refresh step; คุณ rarely edit มันด้วยมือ
- `Owner` — seed owner login สำหรับแอปภายใต้ E2E
- `Database.ConnectionString` — เพียงเมื่อชี้ tests ที่ external Postgres แทน Testcontainers
- `Ai.ApiKey` — Anthropic API key สำหรับ AI features
- `Calendar.FredApiKey` — [FRED](https://fredaccount.stlouisfed.org/apikeys) (St. Louis Fed) API key. The primary economic-calendar value source (interest rates, inflation, employment).
- `Calendar.BlsApiKey` — [BLS](https://data.bls.gov/registrationEngine/) (US Bureau of Labor Statistics) v2 registration key (CPI, PPI, employment, JOLTS). Absent ⇒ the low-quota public tier.

  Both feed the exact `FredSource`/`BlsSource` the ingestion worker uses. With a key present, `CalendarSourceLiveTests` hits the real provider and asserts observations come back; absent, that source's test skips cleanly. The app also reads these at runtime via `App:Calendar:FredApiKey` / `App:Calendar:BlsApiKey` (environment variables override — e.g. `FRED_API_KEY`, `BLS_API_KEY`).

## Precedence

1. **Environment variables** override ทั้งหมด (เช่น `App__OwnerPassword` `App:Ai:ApiKey`)
2. **`secrets/dev-credentials.local.json`** — ไฟล์ unified (preferred)
3. **Legacy split files** — `openapi-test-app.local.json` `openapi-cids.local.json` `openapi-tokens.local.json` ยังคง read เมื่อไฟล์ unified absent ดังนั้น existing machines ทำงานต่อไป New setups ควรใช้ไฟล์เดียว

## Safety

- `secrets/` และ `*.local.json` คือ gitignored — ไม่มีอะไรที่นี่เคยอยู่ commit
- Live copy tests ปฏิเสธที่จะทำงาน non-demo accounts (`IsLive` accounts จะ filter ออก โดย `LiveCopyFixture`) Keep only demo accounts ใน token cache
- In-cluster (Kubernetes) รันเมาท์ไฟล์เป็น read-only Secret; token refreshes เก็บไว้ในหน่วยความจำและ read-only write-back เป็น silent no-op
