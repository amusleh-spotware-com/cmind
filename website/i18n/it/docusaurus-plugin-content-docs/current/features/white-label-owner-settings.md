---
id: white-label-owner-settings
title: Opzioni white-label nelle Impostazioni Owner
sidebar_label: Impostazioni white-label owner
---

# Opzioni white-label nelle Impostazioni Owner

Ogni opzione white-label che un deployment può impostare attraverso configurazione (`appsettings`/env) è
**anche impostabile a runtime dall'owner dell'app**, da **Settings → Deployment**, senza redeploy. Un owner
override **vince sulla configurazione**; cancellarlo ripristina l'opzione al valore configured (o
built-in default) del deployment.

Questo specchia come un deployment white-label *configura* il prodotto — gli stessi knob, lo stesso effetto —
così un operatore può tune branding, gate e policy live e vedere il risultato immediatamente.

## Dove vive

- **UI:** la sezione owner-only **Deployment** nel dialogo impostazioni, e la pagina deep-linkable
  **`/settings/deployment`**. Le opzioni sono raggruppate in **tab per categoria** (Branding, Theme,
  Features, Registration, Accounts, Email, AI, Open API, Prop firm), mobile-first, con un dialogo windowed
  su desktop e una surface full-screen sui telefoni.
- **API:** `/api/whitelabel` (owner-only, mai feature-gated):
  - `GET /api/whitelabel` — ogni opzione con il suo valore effective, provenienza (`Config` / `Owner` /
    `Default`) e se un override è impostato. **I secret sono masked** (valore mai restituito).
  - `PUT /api/whitelabel/{key}` `{ "value": "…" }` — imposta un override (validato per tipo di opzione). Un
    valore blank su un **secret** mantiene il secret existente.
  - `DELETE /api/whitelabel/{key}` — cancella un override (ripristina a config).
  - `POST /api/whitelabel/reset` — cancella **tutti** gli override (ripristina il deployment a pure config).

## Come gli override hanno effetto

Gli owner override sono memorizzati come righe `AppSetting` encrypted-where-needed e layered sopra il bound
`AppOptions` da un `IOptionsMonitor<AppOptions>` decorato. Perché ogni consumatore già legge options
attraverso quel monitor, un override applica **live** attraverso l'intera app — il tema, il page title, il MFA
gate, gli AI-provider gate, il broker allow-list, la policy di registrazione, le impostazioni email transport, ecc.
aggiornano sulla prossima lettura (il tema/branding re-renderizza immediatamente). Se il database è brevemente
unavailable il layer **fails open** al baseline configurato, così una override read può mai rompere l'app.

**I feature flag** sono parte della stessa superficie ma sono persistiti attraverso l'existing feature-override
store (`IFeatureGate`), così il Features tab e i toggle feature standalone non divergono mai.

**I secret** (SMTP password, CAPTCHA secret, provisioning secret) sono crittografati at rest
(`ISecretProtector`, purpose `whitelabel.secret`), write-only nell'UI, e mai restituiti dall'API.

## Opzioni delegate

Le credenziali della **shared Open API application** e i **per-message-type rate limit** sono gestiti nella
sezione impostazioni **Open API** (vedere i docs copy-trading / Open API). Appear nel Deployment
catalog come voci *delegate* (read-only qui, con un link) così niente è duplicato e la garanzia di sync
conta ancora come coperta.

## Sempre in sync (applicato)

Aggiungere una nuova opzione white-label alla configurazione **deve** surfacearla nelle owner settings
nella stessa commit. Questo è applicato da `WhiteLabelCatalogParityTests`: riflette su ogni proprietà
del white-label options-record e falla il build a meno che la proprietà non sia registrata in
`Core/WhiteLabel/WhiteLabelCatalog` (o esplicitamente elencata in `IntentionallyExcluded` con una ragione).
Vedere mandate 10 in `CLAUDE.md`.

## Note

- Abilitare SMTP su un deployment che è partito con **nessuna** email configurata necessita un restart (il
  sender type è scelto all'avvio); host/credentials di un sender già configurato aggiornano live.
- Le **labels/descriptions** delle opzioni sono identificatori tecnici del config-knob mostrati come dati; le
  tab labels e tutto l'interactive chrome sono fully localized.
