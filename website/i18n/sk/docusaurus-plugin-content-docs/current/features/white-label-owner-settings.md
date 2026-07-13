---
id: white-label-owner-settings
title: White-label možnosti v Owner nastaveniach
sidebar_label: White-label owner nastavenia
---

# White-label možnosti v Owner nastaveniach

Každá white-label možnosť, ktorú deployment môže nastaviť cez konfiguráciu (`appsettings`/env) je **tiež
nastaviteľná za behu ownerom aplikácie**, z **Settings → Deployment**, bez redeployu. Owner
override **vyhráva nad konfiguráciou**; vymazanie ho vracia na deployment-configured (alebo
built-in default) hodnotu.

Toto zrkadlí ako white-label *deployment* konfiguruje produkt — rovnaké gombíky, rovnaký efekt —
takže operátor môže ladiť branding, gates a policy live a vidieť výsledok okamžite.

## Kde to žije

- **UI:** owner-only **Deployment** sekcia v settings dialógu a deep-linkovateľná stránka
  **`/settings/deployment`**. Možnosti sú zoskupené do **tabuľky per kategóriu** (Branding, Theme,
  Features, Registration, Accounts, Email, AI, Open API, Prop firm), mobile-first, s windowed
  dialógom na desktope a full-screen surface na telefónoch.
- **API:** `/api/whitelabel` (owner-only, nikdy nie je feature-gated):
  - `GET /api/whitelabel` — každá možnosť s jej effective hodnotou, provenienciou (`Config` / `Owner` /
    `Default`) a či je override nastavený. **Secrets sú maskované** (hodnota sa nikdy nevracia).
  - `PUT /api/whitelabel/{key}` `{ "value": "…" }` — nastaviť override (validované per option kind). Prázdna
    hodnota na **secret** ponechá existujúci secret.
  - `DELETE /api/whitelabel/{key}` — vymazať jeden override (vrátiť sa ku config).
  - `POST /api/whitelabel/reset` — vymazať **všetky** overridy (vrátiť deployment k pure config).

## Ako overridy naberajú účinnosť

Owner overridy sú uložené ako encrypted-where-needed `AppSetting` riadky a layered поверх bound
`AppOptions` decorated `IOptionsMonitor<AppOptions>`. Pretože každý konzument už číta options
cez ten monitor, override aplikuje **live** naprieč celou app — theme, page title, MFA
gate, AI-provider gates, broker allow-list, registration policy, email transport settings atď. aktualizujú
sa pri ďalšom čítaní (theme/branding re-render okamžite). Ak je databáza krátkodobo nedostupná,
layer **fails open** ku konfigurovanej baseline, takže override read nikdy nerozbije app.

**Feature flags** sú súčasťou rovnakej plochy ale sú perzistované cez existujúci feature-override
store (`IFeatureGate`), takže Features tab a standalone feature toggles sa nikdy nerozchádzajú.

**Secrets** (SMTP heslo, CAPTCHA secret, provisioning secret) sú encryptované at-rest
(`ISecretProtector`, purpose `whitelabel.secret`), write-only v UI a nikdy sa nevracia API.

## Delegované možnosti

**Zdieľaná Open API aplikácia** creds a **per-message-type rate limits** sú spravované v
**Open API** settings sekcii (pozrite copy-trading / Open API docs). Zobrazujú sa v Deployment
katalógu ako *delegované* entries (read-only tu, s odkazom), takže nič nie je duplikované a sync
záruka ich stále počíta ako pokryté.

## Vždy v sync (enforced)

Pridanie novej white-label možnosti do konfigurácie **musí** ju surfacovať v owner settings v rovnakom
commite. Toto je enforced `WhiteLabelCatalogParityTests`: reflektuje nad každou white-label
options-record property a failuje build, pokiaľ property nie je zaregistrovaná v
`Core/WhiteLabel/WhiteLabelCatalog` (alebo explicitne listed v `IntentionallyExcluded` s dôvodom).
Pozrite mandate 10 v `CLAUDE.md`.

## Poznámky

- Povolenie SMTP na deployment, ktorý štartoval **bez** nakonfigurovaného emailu, potrebuje restart (sender
  type je vybraný pri štarte); host/creds už nakonfigurovaného sendera sa aktualizujú live.
- **Labels/descriptions** možností sú technické config-knob identifikátory zobrazené ako data; tab
  labely a všetky interaktívne chrome sú plne lokalizované.
