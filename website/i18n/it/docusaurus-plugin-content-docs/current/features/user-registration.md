---
description: "Registrazione utente self-service sicura, white-label-gated — una pagina di sign-up in-app e un'API di provisioning server-to-server, con attributi utente configurabili, gating admin-approval o email-verification, e guard anti-abuse. Disabilitato per default."
---

# Registrazione utente

Di default l'**owner/admin aggiunge gli utenti manualmente** (pagina Users → *Nuovo Utente*). Per deployment white-label
che necessitano di onboardare utenti a scala — o integrare l'app con un altro servizio — cMind include anche un
**percorso di registrazione self-service sicuro**. È **disabilitato per default**: un deployment stock è unchanged
e la pagina e l'API entrambe restituiscono 404 finché un deployment non opta in.

Ci sono due entry point che condividono un flusso domain:

1. **Pagina in-app** (`/register`) — una pagina di sign-up branded, mobile-first nella stessa shell di `/login`.
2. **API di Provisioning** (`POST /api/provision`) — un endpoint server-to-server per un servizio integrato per
   creare account, autenticato da un secret di provisioning per deployment.

## Cosa viene registrato — minimizzazione dati

cMind è tooling di **trading**: costruisce/run/backtesta cBot e mirror trades attraverso le *proprie*
credenziali cTrader Open API di ogni utente. **Non apre account di trading né custodisce soldi dei clienti**, quindi la
verifica identità KYC/AML è l'**obbligo del broker**, non di questa piattaforma. Il modulo di registrazione quindi
registra **solo una email per default** — il minimo necessario per fornire il servizio (GDPR Art. 5(1)(c)
minimizzazione dati; base giuridica = contratto). cMind deliberatamente non include campi di national-ID /
data di nascita / indirizzo.

Ogni altro attributo è **opt-in per deployment** via `App:Registration:Attributes`, ciascuno indipendentemente
`Off` / `Optional` / `Required`:

| Attributo | Note |
|---|---|
| `FullName`, `DisplayName`, `Company` | Testo libero, bounded in lunghezza. |
| `Country` | ISO 3166-1 alpha-2, validato contro un set di codici fixed. |
| `Phone` | Formato E.164 (`+14155552671`). |
| `Locale` | Forma BCP-47 (`en-US`), normalizzata. |
| `MarketingOptIn` | Checkbox separata, **non spuntata** — mai bundled con il consenso mandatory (CAN-SPAM). |
| `AgeConfirmation` | Solo una checkbox; **nessuna** data di nascita è archiviata. |

Gli attributi vivono nel value object `UserProfile` owned dall'aggregato `AppUser`, validato alla
costruzione. **Cancellazione GDPR** (`AppUser.Anonymize()`) scrubba il profilo e qualsiasi token di verifica.

**Consenso.** Quando `RequireTermsAcceptance` è attivo, l'utente deve accettare i documenti legali pubblicati
(Terms, Privacy, Risk Disclosure). Il consenso è registrato attraverso l'aggregato `ConsentRecord` esistente —
stamped con versione, timestamp, con IP di origine — lo stesso store usato altrove per la tenuta registri
grade MiFID/ESMA.

## Modalità di gating

Un account auto-registrato non può accedere finché non supera il suo gate (`App:Registration:Mode`):

- **`AdminApproval`** (default) — l'account è accodato; un owner/admin lo approva nella pagina **Users**
  (sezione *Pending approval*). Non necessita infrastruttura mail.
- **`EmailVerification`** — un link di verifica monouso, expiring è inviato via email; l'account si attiva quando
  il link è aperto. Richiede un transport email (`App:Email`). **Se nessun transport è configurato, questa modalità
  automaticamente downgrades a `AdminApproval`** all'avvio, quindi abilitare la registrazione non rompe mai silenziosamente.
- **`Open`** — l'account è attivo immediatamente (solo trusted/dev).

Gli utenti auto-registrati sono sempre creati come **`User`** (o `Viewer` se configurato) — il domain
**hard-rifiuta** di creare Owner/Admin attraverso la registrazione self-service.

## Sicurezza & anti-abuso

- **Anti-enumerazione.** Una email duplicata yield lo stesso neutro `202 Accepted` di un fresh sign-up e
  non crea niente — l'app non rivela mai se un indirizzo ha già un account.
- **Rate limiting.** Gli endpoint pubblici sono throttled per IP (più forte del limiter auth).
- **Politica password.** Lunghezza minima applicata; le password sono hashed (Argon2 via `IPasswordHasher`);
  i token di verifica sono archiviati solo come hash SHA-256 e sono monouso + expiring.
- **Email hygiene.** Allow-list opzionale di domini email e block-list di provider disposable.
- **CAPTCHA (opzionale).** reCAPTCHA / hCaptcha / Turnstile tramite il loro shared verify contract.
- **Login gate.** Un account pending è rifiutato all'accesso con una risposta neutra.

## API di Provisioning (integrazione)

Con `App:Registration:Api:Enabled` e un `Secret` impostato, un altro servizio può creare utenti:

```
POST /api/provision
X-Provision-Secret: <the configured secret>
{ "email": "user@example.com", "password": "…", "role": 2 }
```

Il secret è comparato in tempo costante. Gli account provisioned sono creati **attivi** (o invitati con
`MustChangePassword`) a seconda di `Api.ActivateImmediately` / `Api.InviteMustChangePassword`.

## Abilitarlo

La registrazione richiede **entrambi** il feature flag e l'interruttore master:

```jsonc
"App": {
  "Features": { "Registration": true },
  "Registration": {
    "Enabled": true,
    "Mode": "AdminApproval",           // or EmailVerification / Open
    "DefaultRole": "User",             // never Owner/Admin
    "RequireTermsAcceptance": true,
    "AllowedEmailDomains": [],          // empty = any
    "BlockDisposableEmail": true,
    "Attributes": { "FullName": "Optional", "Country": "Off" },
    "Api": { "Enabled": false, "Secret": "" }
  }
}
```

La sezione `App:Email` (SMTP `Host`, `Port`, `UseStartTls`, `Username`, `Password`, `FromAddress`,
`FromName`) configura il transport usato dalla modalità `EmailVerification`; lasciare `Host` unset per girare
senza mail (il sender no-op). Vedere [feature toggles](./feature-toggles.md) e
[white-label](./white-label.md) per come i deployment accendono le feature e fanno rebrand. Quando la
registrazione è abilitata, la pagina di login mostra un link **Crea account**.

## Testato

Unit (profile validation, `SelfRegister` role guard, activation transitions, token monouso, erasure),
integration (default-off 404, approval flow, email-verification downgrade, anti-enumeration, abuse
guards, required attributes, provisioning + bad secret), e E2E (default-off login non ha link di sign-up; la
pagina `/register` renderizza il suo branded closed state).
