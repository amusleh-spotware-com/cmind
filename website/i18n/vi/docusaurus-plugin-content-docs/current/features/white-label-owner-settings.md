---
id: white-label-owner-settings
title: White-label options in Owner settings
sidebar_label: White-label owner settings
---

# White-label options in Owner settings

Mọi white-label option a deployment có thể set through configuration (`appsettings`/env) là **also
settable at runtime by app owner**, từ **Settings → Deployment**, without a redeploy. An owner
override **wins over configuration**; clearing it reverts option to deployment's configured (hoặc
built-in default) value.

Điều này mirrors cách một white-label *deployment* configures product — same knobs, same effect —
vì vậy an operator có thể tune branding, gates và policy live và see result immediately.

## Where it lives

- **UI:** owner-only **Deployment** section in settings dialog, và deep-linkable page
  **`/settings/deployment`**. Options grouped into a **tab per category** (Branding, Theme,
  Features, Registration, Accounts, Email, AI, Open API, Prop firm), mobile-first, với windowed
  dialog on desktop và full-screen surface on phones.
- **API:** `/api/whitelabel` (owner-only, never feature-gated):
  - `GET /api/whitelabel` — every option với its effective value, provenance (`Config` / `Owner` /
    `Default`) và whether an override set. **Secrets masked** (value never returned).
  - `PUT /api/whitelabel/{key}` `{ "value": "…" }` — set an override (validated per option kind). A blank
    value on a **secret** keeps existing secret.
  - `DELETE /api/whitelabel/{key}` — clear one override (revert to config).
  - `POST /api/whitelabel/reset` — clear **all** overrides (revert deployment to pure config).

## How overrides take effect

Owner overrides stored as encrypted-where-needed `AppSetting` rows và layered on top of bound
`AppOptions` by a decorated `IOptionsMonitor<AppOptions>`. Because every consumer already reads options
through that monitor, an override applies **live** across whole app — theme, page title, MFA
gate, AI-provider gates, broker allow-list, registration policy, email transport settings, etc. update
on next read (theme/branding re-render immediately). If database briefly unavailable, layer
**fails open** to configured baseline, vì vậy an override read never breaks app.

**Feature flags** are part of same surface but persisted through existing feature-override
store (`IFeatureGate`), vì vậy Features tab và standalone feature toggles never diverge.

**Secrets** (SMTP password, CAPTCHA secret, provisioning secret) are encrypted at rest
(`ISecretProtector`, purpose `whitelabel.secret`), write-only in UI, và never returned by API.

## Delegated options

**Shared Open API application** credentials và **per-message-type rate limits** are managed on
**Open API** settings section (xem copy-trading / Open API docs). Chúng appear in Deployment
catalog as *delegated* entries (read-only here, with a link) vì vậy nothing duplicated và sync
guarantee still counts them as covered.

## Always in sync (enforced)

Adding a new white-label option to configuration **must** surface it in owner settings in same
commit. This enforced by `WhiteLabelCatalogParityTests`: it reflects over every white-label
options-record property và fails build unless property registered in
`Core/WhiteLabel/WhiteLabelCatalog` (hoặc explicitly listed in `IntentionallyExcluded` với reason).
Xem mandate 10 in `CLAUDE.md`.

## Notes

- Enabling SMTP on a deployment that started với **no** email configured needs a restart (sender
  type chosen at startup); host/credentials của một already-configured sender update live.
- Option **labels/descriptions** are technical config-knob identifiers shown as data; tab labels và
  all interactive chrome are fully localized.
