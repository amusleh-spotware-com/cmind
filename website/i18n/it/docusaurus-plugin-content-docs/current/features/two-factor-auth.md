---
description: "Autenticazione a due fattori TOTP opzionale con registrazione di app autenticatore, codici di backup monouso, e uno switch white-label per renderlo obbligatorio per tutti gli utenti."
---

# Autenticazione a due fattori (2FA)

Gli account possono essere protetti con **autenticazione a due fattori a password monouso basata su tempo (TOTP)** in cima alla
password. È **opt-in** dal profilo dell'utente per impostazione predefinita, e un deployment white-label può renderlo
**obbligatorio** per tutti. Qualsiasi app autenticatore RFC 6238 funziona — Google Authenticator, Microsoft
Authenticator, Authy, Aegis, FreeOTP — perché l'implementazione è standard (SHA-1, 6 cifre, passo di 30 secondi);
nessun componente server proprietario è coinvolto.

## Come funziona

- **Dominio.** L'MFA vive sull'aggregato `AppUser` (Access context). Un utente si iscrive tramite
  metodi intention-revealing — `BeginMfaEnrollment`, `ConfirmMfaEnrollment`, `ConsumeBackupCode`,
  `RegenerateBackupCodes`, `DisableMfa` — così gli invarianti (un segreto deve essere confermato prima di attivarsi;
  un codice di backup è monouso) sono applicati in un unico posto.
- **TOTP.** La generazione e la verifica si trovano dietro l'interfaccia Core `ITotpAuthenticator`, implementata in
  Infrastructure con la libreria **Otp.NET**. La verifica tollera ±1 time-step di skew di clock.
- **Segreto a riposo.** Il segreto dell'autenticatore è archiviato **crittografato** tramite `ISecretProtector`
  (`EncryptionPurposes.MfaSecret`) — mai in plaintext.
- **Codici di backup.** Dieci codici di recupero monouso vengono emessi all'iscrizione, mostrati **una volta**, e archiviati solo
  come hash SHA-256 (`MfaBackupCodes`). Ognuno funziona esattamente una volta; un codice speso è rifiutato dopo.

## Abilitarlo (profilo)

Nella pagina **Account** (`/account`) la sezione *Autenticazione a due fattori* mostra lo stato attuale:

1. **Abilita due fattori** apre un dialogo MudBlazor con un **codice QR** (renderizzato server-side come SVG tramite
   `Net.Codecrete.QrCodeGenerator`) più la chiave di configurazione manuale.
2. Scansionalo, inserisci il codice di 6 cifre per confermare — questo verifica il segreto in sospeso prima di attivarlo.
3. Il dialogo quindi mostra i **codici di backup**; salvali. 2FA è ora attivo.

La stessa sezione consente a un utente iscritto di **rigenerare i codici di backup** o **disattivare** 2FA — entrambi
richiedono la password dell'account per confermare.

## Accesso con 2FA

L'accesso è un flusso **a due step** una volta che 2FA è abilitato:

1. **Passo password** (`POST /api/auth/login`). Al successo il cookie auth **non** è ancora emesso; invece un
   cookie in sospeso breve (5 minuti), crittografato viene impostato e l'utente è inviato a `/login/2fa`.
2. **Passo di sfida** (`POST /api/auth/login/verify-2fa`). L'utente inserisce un codice TOTP **o** qualsiasi codice di backup inutilizzato.
   Al successo il cookie in sospeso viene eliminato e il vero cookie auth è emesso.

I tentativi di secondo fattore falliti contano verso il **lockout** dell'account esistente (`AuthLockout`), e gli endpoint
di autenticazione sono rate-limited.

## 2FA obbligatorio per un deployment white-label

Un rivenditore regolamentato può richiedere 2FA per **ogni** account:

```jsonc
// appsettings / environment
"App": { "Branding": { "RequireMfa": true } }   // App__Branding__RequireMfa=true
```

Quando `RequireMfa` è attivato e un utente senza 2FA accede, il passo password segnala
`mfaSetupRequired` e `MfaEnforcementMiddleware` reindirizza le navigazioni della pagina a `/account` fino al completamento
dell'iscrizione. Predefinisce a `false`, quindi un deployment non configurato mantiene 2FA facoltativo. Vedi
[White-label](white-label.md).

## Endpoint

| Metodo e rotta | Scopo |
| --- | --- |
| `POST /api/auth/login` | Passo password; restituisce `mfaRequired` (sfida) o accede |
| `POST /api/auth/login/verify-2fa` | Passo di secondo fattore (TOTP o codice di backup) |
| `GET /api/auth/mfa/status` | `MfaEnabled`, in sospeso, conteggio di codice di backup rimanente |
| `POST /api/auth/mfa/setup` | Inizia l'iscrizione — restituisce segreto, URI `otpauth://`, QR SVG |
| `POST /api/auth/mfa/confirm` | Conferma un codice, attiva, restituisce codici di backup |
| `POST /api/auth/mfa/disable` | Disattiva (password-confermato) |
| `POST /api/auth/mfa/backup-codes/regenerate` | Emetti un set fresco (password-confermato) |

## Test

- **Unità** — `UnitTests/Access/OtpNetTotpAuthenticatorTests.cs` (vettori RFC 6238),
  `AppUserMfaTests.cs` (iscrizione/transizione/invarianti monouso), `MfaBackupCodesTests.cs`.
- **Integrazione** — `IntegrationTests/MfaPersistenceTests.cs` (iscriviti → conferma → consuma, cancella a cascata)
  e `MfaFlowTests.cs` (accesso HTTP a due step completo con TOTP + codice di backup, e il gate di iscrizione obbligatorio).
- **E2E** — `E2ETests/MfaFlowTests.cs`: abilita dal profilo (QR + conferma + codici di backup) e completa un'entrata sfidante,
  su viewport desktop e mobile.
