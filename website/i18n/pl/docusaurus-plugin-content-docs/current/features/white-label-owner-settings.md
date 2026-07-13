---
id: white-label-owner-settings
title: White-label opcje w Owner settings
sidebar_label: White-label owner settings
---

# White-label opcje w Owner settings

Każda white-label opcja deployment może ustawić poprzez configuration (`appsettings`/env) jest
**również settable na runtime przez app owner**, z **Settings → Deployment**, bez redeploy. Owner
override **wins przez configuration**; clearing to reverts opcję do deployment'a configured (albo
built-in default) wartości.

To mirrors jak white-label *deployment* configures produkt — te same knobs, ten sam effect —
więc operator może tune branding, gates i policy live i widzieć rezultat natychmiast.

## Gdzie to żyje

- **UI:** owner-only **Deployment** sekcja w settings dialog, i deep-linkable strona
  **`/settings/deployment`**. Opcje są grouped do **tab per category** (Branding, Theme,
  Features, Registration, Accounts, Email, AI, Open API, Prop firm), mobile-first, z windowed
  dialog na desktop i full-screen surface na phones.
- **API:** `/api/whitelabel` (owner-only, nigdy feature-gated):
  - `GET /api/whitelabel` — każda opcja z jej effective wartością, provenance (`Config` / `Owner` /
    `Default`) i czy override jest set. **Sekrety są masked** (wartość nigdy nie zwrócona).
  - `PUT /api/whitelabel/{key}` `{ "value": "…" }` — set override (validated per opcja kind).
    Blank wartość na **secret** keeps existing secret.
  - `DELETE /api/whitelabel/{key}` — clear jeden override (revert do config).
  - `POST /api/whitelabel/reset` — clear **wszystkie** overrides (revert deployment do pure config).

## Jak overrides bierze efekt

Owner overrides są stored jako encrypted-where-needed `AppSetting` rows i layered na top
bound `AppOptions` poprzez decorated `IOptionsMonitor<AppOptions>`. Ponieważ każdy consumer
już czyta opcje poprzez tę monitor, override applies **live** across całej app — theme,
page title, MFA gate, AI-provider gates, broker allow-list, registration policy, email
transport settings, itd. update na następny read (theme/branding re-render natychmiast).
Jeśli database jest briefly unavailable warstwa **fails open** do configured baseline,
więc override read nigdy nie może break app.

**Feature flags** są część tego samego surface ale są persisted poprzez existing feature-override
store (`IFeatureGate`), więc Features tab i standalone feature toggles nigdy nie diverge.

**Sekrety** (SMTP password, CAPTCHA secret, provisioning secret) są encrypted na rest
(`ISecretProtector`, purpose `whitelabel.secret`), write-only w UI, i nigdy nie zwrócona
przez API.

## Delegated opcje

**Shared Open API application** credentials i **per-message-type rate limits** są managed
na **Open API** settings sekcja (zobacz copy-trading / Open API docs). Pojawiają się w
Deployment katalog jako *delegated* entries (read-only tutaj, z link) więc nic nie jest
duplicated i sync gwarancja ciągle counts je jako covered.

## Zawsze w sync (enforced)

Dodawanie nowej white-label opcji do configuration **musi** surface je w owner settings w
tym samym commit. To jest enforced przez `WhiteLabelCatalogParityTests`: to reflects nad
każdą white-label options-record property i fails build chyba nie property jest registered
w `Core/WhiteLabel/WhiteLabelCatalog` (albo explicitly listed w `IntentionallyExcluded`
z reasonem). Zobacz mandate 10 w `CLAUDE.md`.

## Notatki

- Enabling SMTP na deployment który started z **brak** email configured potrzebuje restart
  (sender type jest chosen na startup); host/credentials z already-configured sender update live.
- Opcja **labels/descriptions** są technical config-knob identifiers pokazane jako data;
  tab labels i wszystkie interactive chrome są fully lokalizowane.
