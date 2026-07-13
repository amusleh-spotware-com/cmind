---
id: white-label-owner-settings
title: White-label možnosti v nastavení Owner
sidebar_label: White-label owner nastavení
---

# White-label možnosti v Owner nastavení

Každá white-label volba kterou deployment může nastavit přes konfiguraci (`appsettings`/env) je **also
settable at runtime by the app owner**, z **Settings → Deployment**, bez redeploye. Owner
override **wins over configuration**; clearing it reverts the option to the deployment's configured (or
built-in default) value.

Toto zrcadlí jak white-label *deployment* konfiguruje produkt — stejné knoby, stejný efekt —
takže operátor může ladit branding, gates a policy live a vidět výsledek okamžitě.

## Kde to žije

- **UI:** owner-only **Deployment** section in settings dialog, and the deep-linkable page
  **`/settings/deployment`**. Možnosti jsou seskupeny do **tab per kategorii** (Branding, Theme,
  Features, Registration, Accounts, Email, AI, Open API, Prop firm), mobile-first, with a windowed
  dialog on desktop and a full-screen surface on phones.
- **API:** `/api/whitelabel` (owner-only, never feature-gated):
  - `GET /api/whitelabel` — každá volba s its effective value, provenance (`Config` / `Owner` /
    `Default`) and whether an override is set. **Secrets are masked** (value never returned).
  - `PUT /api/whitelabel/{key}` `{ "value": "…" }` — set an override (validated per option kind). A blank
    value on a **secret** keeps the existing secret.
  - `DELETE /api/whitelabel/{key}` — clear one override (revert to config).
  - `POST /api/whitelabel/reset` — clear **all** overrides (revert the deployment to pure config).

## Jak overrides berou efekt

Owner overrides are stored as encrypted-where-needed `AppSetting` rows and layered on top of the bound
`AppOptions` by a decorated `IOptionsMonitor<AppOptions>`. Protože každý consumer už čte options
přes that monitor, an override applies **live** across the whole app — theme, page title, MFA
gate, AI-provider gates, broker allow-list, registration policy, email transport settings, etc. update
on next read (theme/branding re-render immediately). Pokud je databáze krátkodobě nedostupná, vrstva
**fails open** to the configured baseline, takže override read can never break the app.

**Feature flags** are part of the same surface but are persisted through the existing feature-override
store (`IFeatureGate`), takže Features tab and the standalone feature toggles never diverge.

**Secrets** (SMTP password, CAPTCHA secret, provisioning secret) are encrypted at rest
(`ISecretProtector`, purpose `whitelabel.secret`), write-only in the UI, and never returned by the API.

## Delegované options

**Sdílená Open API aplikace** credentials a **per-message-type rate limity** are managed on the
**Open API** settings section (viz copy-trading / Open API docs). Zobrazují se v Deployment
catalog jako *delegated* entries (read-only here, with a link) takže nothing is duplicated and the sync
záruka still counts them as covered.

## Vždy v sync (enforced)

Přidání nové white-label volby do konfigurace **must** surface it in owner settings in the same
commit. Toto is enforced by `WhiteLabelCatalogParityTests`: it reflects over every white-label
options-record property and fails the build unless the property is registered in
`Core/WhiteLabel/WhiteLabelCatalog` (or explicitly listed in `IntentionallyExcluded` with a reason).
Viz mandate 10 in `CLAUDE.md`.

## Poznámky

- Enabling SMTP on a deployment that started with **no** email configured potřebuje restart (sender
  type is chosen at startup); host/credentials of an already-configured sender update live.
- Volba **labels/descriptions** are technical config-knob identifiers shown as data; tab labels and
  veškerý interactive chrome are fully localized.
