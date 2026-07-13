---
id: white-label-owner-settings
title: White-label opcije u Vlasnikovim podesavanjima
sidebar_label: White-label vlasnicka podesavanja
---

# White-label opcije u Vlasnikovim podesavanjima

Svaka white-label opcija koju deployment moze podesiti kroz konfiguraciju (`appsettings`/env) is **also
settable at runtime by the app owner**, iz **Settings → Deployment**, bez ponovnog deployment-a. An owner
override **wins over configuration**; brisanje ga vraca opciju na deployment-ovo konfigurisano (or
built-in default) value.

This mirrors how a white-label *deployment* configures the product — the same knobs, the same effect —
so an operator can tune branding, gates and policy live and see the result immediately.

## Where it lives

- **UI:** the owner-only **Deployment** section in the settings dialog, and the deep-linkable page
  **`/settings/deployment`**. Options are grouped into a **tab per category** (Branding, Theme,
  Features, Registration, Accounts, Email, AI, Open API, Prop firm), mobile-first, with a windowed
  dialog on desktop and a full-screen surface on phones.
- **API:** `/api/whitelabel` (owner-only, never feature-gated):
  - `GET /api/whitelabel` — every option with its effective value, provenance (`Config` / `Owner` /
    `Default`) and whether an override is set. **Secrets are masked** (value never returned).
  - `PUT /api/whitelabel/{key}` `{ "value": "…" }` — set an override (validated per option kind). A blank
    value on a **secret** keeps the existing secret.
  - `DELETE /api/whitelabel/{key}` — clear one override (revert to config).
  - `POST /api/whitelabel/reset` — clear **all** overrides (revert the deployment to pure config).

## How overrides take effect

Owner overrides are stored as encrypted-where-needed `AppSetting` rows and layered on top of the bound
`AppOptions` by a decorated `IOptionsMonitor<AppOptions>`. Because every consumer already reads options
through that monitor, an override applies **live** across the whole app — the theme, page title, MFA
gate, AI-provider gates, broker allow-list, registration policy, email transport settings, etc. update
on the next read (the theme/branding re-render immediately). If the database is briefly unavailable the
layer **fails open** to the configured baseline, so an override read can never break the app.

**Feature flags** are part of the same surface but are persisted through the existing feature-override
store (`IFeatureGate`), so the Features tab and the standalone feature toggles never diverge.

**Secrets** (SMTP password, CAPTCHA secret, provisioning secret) are encrypted at rest
(`ISecretProtector`, purpose `whitelabel.secret`), write-only in the UI, and never returned by the API.

## Delegated options

The **shared Open API application** credentials and **per-message-type rate limits** are managed on the
**Open API** settings section (see the copy-trading / Open API docs). They appear in the Deployment
catalog as *delegated* entries (read-only here, with a link) so nothing is duplicated and the sync
guarantee still counts them as covered.

## Always in sync (enforced)

Adding a new white-label option to configuration **must** surface it in owner settings in the same
commit. This is enforced by `WhiteLabelCatalogParityTests`: it reflects over every white-label
options-record property and fails the build unless the property is registered in
`Core/WhiteLabel/WhiteLabelCatalog` (or explicitly listed in `IntentionallyExcluded` with a reason).
See mandate 10 in `CLAUDE.md`.

## Notes

- Enabling SMTP on a deployment that started with **no** email configured needs a restart (the sender
  type is chosen at startup); host/credentials of an already-configured sender update live.
- Option **labels/descriptions** are technical config-knob identifiers shown as data; the tab labels and
  all interactive chrome are fully localized.
