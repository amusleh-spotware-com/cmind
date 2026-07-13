---
id: white-label-owner-settings
title: White-label options ใน Owner settings
sidebar_label: White-label owner settings
---

# White-label options ใน Owner settings

ทุก white-label option deployment สามารถ set ผ่าน configuration (`appsettings`/env) เป็น **also
settable ที่ runtime โดย app owner** จาก **Settings → Deployment** ไม่มี redeploy owner
override **wins over configuration**; clearing มัน reverts option ไป deployment ของ configured (หรือ
built-in default) value

นี้ mirrors วิธี white-label *deployment* configures product — knobs เดียวกัน effect เดียวกัน —
ดังนั้น operator สามารถ tune branding gates และ policy live และ see result immediately

## Where มันlives

- **UI:** the owner-only **Deployment** section ใน settings dialog และ deep-linkable page
  **`/settings/deployment`** options grouped ไป **tab per category** (Branding Theme
  Features Registration Accounts Email AI Open API Prop firm) mobile-first ด้วย windowed
  dialog on desktop และ full-screen surface on phones
- **API:** `/api/whitelabel` (owner-only never feature-gated):
  - `GET /api/whitelabel` — ทุก option ด้วย effective value ของมัน provenance (`Config` / `Owner` /
    `Default`) และ ว่า override set หรือไม่ **Secrets masked** (value never returned)
  - `PUT /api/whitelabel/{key}` `{ "value": "…" }` — set override (validated per option kind) blank
    value on **secret** keeps existing secret
  - `DELETE /api/whitelabel/{key}` — clear one override (revert ไป config)
  - `POST /api/whitelabel/reset` — clear **all** overrides (revert deployment ไป pure config)

## How overrides take effect

Owner overrides stored เป็น encrypted-where-needed `AppSetting` rows และ layered บน top ของ bound
`AppOptions` โดย decorated `IOptionsMonitor<AppOptions>` เพราะ ทุก consumer already reads options
ผ่าน monitor override applies **live** ข้าม whole app — theme page title MFA
gate AI-provider gates broker allow-list registration policy email transport settings ฯลฯ update
on next read (theme/branding re-render immediately) ถ้า database briefly unavailable layer
**fails open** ไป configured baseline ดังนั้น override read can never break app

**Feature flags** part ของ same surface แต่ persisted ผ่าน existing feature-override
store (`IFeatureGate`) ดังนั้น Features tab และ standalone feature toggles never diverge

**Secrets** (SMTP password CAPTCHA secret provisioning secret) encrypted at rest
(`ISecretProtector` purpose `whitelabel.secret`) write-only ใน UI และ never returned โดย API

## Delegated options

**shared Open API application** credentials และ **per-message-type rate limits** managed on
**Open API** settings section (ดู copy-trading / Open API docs) พวกเขา appear ใน Deployment
catalog เป็น *delegated* entries (read-only ที่นี่ ด้วย link) ดังนั้นอะไรก็ไม่ duplicated และ sync
guarantee still counts พวกเขา covered

## Always ใน sync (enforced)

adding new white-label option ไป configuration **must** surface มัน ใน owner settings ใน same
commit นี้ enforced โดย `WhiteLabelCatalogParityTests`: มันreflects over ทุก white-label
options-record property และ fails build unless property registered ใน
`Core/WhiteLabel/WhiteLabelCatalog` (หรือ explicitly listed ใน `IntentionallyExcluded` ด้วย reason)
ดู mandate 10 ใน `CLAUDE.md`

## Notes

- enabling SMTP on deployment ที่ started ด้วย **no** email configured needs restart (sender
  type chosen at startup); host/credentials ของ already-configured sender update live
- option **labels/descriptions** technical config-knob identifiers shown as data; tab labels และ
  ทั้งหมด interactive chrome fully localized
