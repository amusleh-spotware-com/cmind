---
description: "Bezpečná, white-label-gated samoobslužná registrácia používateľov — on-app sign-up stránka a server-to-server provisioning API, s konfigurovateľnými atribútmi používateľa, admin-approval alebo email-verification gating a anti-abuse guards. Zakázané predvolene."
---

# User registration

Predvolene **owner/admin pridáva používateľov manuálne** (stránka Users → *New User*). Pre white-label deploymenty,
ktoré potrebujú onboardovať používateľov vo veľkom — alebo integrovať aplikáciu s inou službou — cMind tiež dodáva
**bezpečnú, samoobslužnú registráciu**. Je **zakázaná predvolene**: stock deployment je nezmenený
a stránka aj API obe vracajú 404, kým deployment neoptuje.

Existujú dva vstupné body zdieľajúce jeden doménový flow:

1. **On-app stránka** (`/register`) — branded, mobile-first sign-up stránka v rovnakej shell ako `/login`.
2. **Provisioning API** (`POST /api/provision`) — server-to-server endpoint pre integračnú službu na
   vytváranie účtov, autentizované per-deployment provisioning secret.

## Čo sa zaznamenáva — minimalizácia dát

cMind je **obchodný nástroj**: builduje/beží/backtestuje cBoty a zrkadlí obchody cez každého používateľa *vlastné*
cTrader Open API creds. **Neotvára obchodné účty ani necustoduje klientske peniaze**, takže KYC/AML
overenie identity je **brokerova** povinnosť, nie tejto platformy. Registračný formulár preto
zaznamenáva **iba email predvolene** — minimum potrebné na poskytnutie služby (GDPR Art. 5(1)(c) data
minimization; lawful basis = zmluva). cMind zámerne nedodáva **žiadne** národné-ID / dátum-narodenia /
adresné polia.

Každý iný atribút je **opt-in per deployment** cez `App:Registration:Attributes`, každý nezávisle
`Off` / `Optional` / `Required`:

| Atribút | Poznámky |
|---|---|
| `FullName`, `DisplayName`, `Company` | Voľný text, ohraničenej dĺžky. |
| `Country` | ISO 3166-1 alpha-2, validované proti fixnej sade kódov. |
| `Phone` | E.164 formát (`+14155552671`). |
| `Locale` | BCP-47 tvar (`en-US`), normalizované. |
| `MarketingOptIn` | Samostatný, **nezaškrtnutý** checkbox — nikdy nekombolvaný so súhlasom (CAN-SPAM). |
| `AgeConfirmation` | Iba checkbox; **žiadny** dátum narodenia nie je uložený. |

Atribúty žijú v `UserProfile` value object vlastnenom `AppUser` aggregate, validované pri
konštrukcii. **GDPR erasure** (`AppUser.Anonymize()`) vymaže profil a akékoľvek overovacie tokeny.

**Súhlas.** Keď `RequireTermsAcceptance` je zapnuté, používateľ musí prijať publikované právne dokumenty
(Terms, Privacy, Risk Disclosure). Súhlas je zaznamenávaný cez existujúci `ConsentRecord` aggregate —
verziovaný, timestamped, s origini IP — rovnaký store použitý elsewhere pre MiFID/ESMA-grade
record-keeping.

## Gating módy

Samo-registrovaný účet sa nemôže prihlásiť, kým nevymaže svoju gate (`App:Registration:Mode`):

- **`AdminApproval`** (predvolené) — účet je vo fronte; owner/admin ho schváli na stránke **Users**
  (*Pending approval* sekcia). Nevyžaduje mail infraštruktúru.
- **`EmailVerification`** — jednorazový, expirovaný overovací link je emailnutý; účet sa aktivuje keď
  je link otvorený. Vyžaduje email transport (`App:Email`). **Ak nie je transport nakonfigurovaný, tento mód**
  **automaticky degraduje na `AdminApproval`** pri štarte, takže enabling registrácie nikdy tichý nezlomí.
- **`Open`** — účet je aktívny okamžite (dôveryhodný/dev iba).

Samo-registrovaní používatelia sú vždy vytvorení ako **`User`** (alebo `Viewer` ak nakonfigurované) —
doména **tvrdoodmieta** raziť Owner/Admin cez samo-registráciu.

## Bezpečnosť & anti-abuse

- **Anti-enumeration.** Duplicate email vracia **rovnakú** neutrálnu `202 Accepted` ako čerstvý sign-up a
  nič nevytvára — aplikácia nikdy neprezrádza, či adresa už má účet.
- **Rate limiting.** Verejné endpoints sú throttleované per IP (prísnejšie ako auth limiter).
- **Password policy.** Minimálna dĺžka enforceovaná; heslá sú hashované (Argon2 cez `IPasswordHasher`);
  overovacie tokeny sú uložené iba ako SHA-256 hashe a sú jednorazové + expirované.
- **Email hygiene.** Voliteľný allow-list email domén a block-list disposal providerov.
- **CAPTCHA (voliteľná).** reCAPTCHA / hCaptcha / Turnstile cez ich zdieľaný verify kontrakt.
- **Login gate.** Čakajúci účet je odmietnutý pri login s neutrálnou odpoveďou.

## Provisioning API (integrácia)

S `App:Registration:Api:Enabled` a nastaveným `Secret`, iná služba môže vytvárať používateľov:

```
POST /api/provision
X-Provision-Secret: <the configured secret>
{ "email": "user@example.com", "password": "…", "role": 2 }
```

Secret je porovnaný v konštantnom čase. Provisioned účty sú vytvorené **aktívne** (alebo pozvané s
`MustChangePassword`) v závislosti od `Api.ActivateImmediately` / `Api.InviteMustChangePassword`.

## Enabling it

Registrácia vyžaduje **obe** — feature flag aj master switch:

```jsonc
"App": {
  "Features": { "Registration": true },
  "Registration": {
    "Enabled": true,
    "Mode": "AdminApproval",           // alebo EmailVerification / Open
    "DefaultRole": "User",             // nikdy Owner/Admin
    "RequireTermsAcceptance": true,
    "AllowedEmailDomains": [],          // prázdne = hocijaká
    "BlockDisposableEmail": true,
    "Attributes": { "FullName": "Optional", "Country": "Off" },
    "Api": { "Enabled": false, "Secret": "" }
  }
}
```

Sekcia `App:Email` (SMTP `Host`, `Port`, `UseStartTls`, `Username`, `Password`, `FromAddress`,
`FromName`) konfiguruje transport použitý `EmailVerification` módem; nechajte `Host` nenastavené pre
beh bez mailu (no-op sender). Pozrite [feature toggles](./feature-toggles.md) a [white-label](./white-label.md)
ako deploymenty zapínajú funkcie a pre-značkujú. Keď je registrácia povolená, login stránka zobrazuje odkaz **Create
account**.

## Testované

Jednotka (profile validácia, `SelfRegister` role guard, aktivačné prechody, single-use tokeny, erasure),
integrácia (disabled-by-default 404, approval flow, email-verification downgrade, anti-enumeration, abuse
guards, required attributes, provisioning + bad secret) a E2E (default-off login nemá sign-up link; `/register`
stránka renderuje jej branded closed state).
