---
description: "Varen, white-label-zaprt samopostrežna registracija uporabnikov — stran za vpis v aplikaciji in API za strežnik-strežnik provisioniranje, s konfigurabilnimi atributi uporabnika, odobritvijo administratorja ali vrati avtentikacije e-pošte ter zaščito pred zlorabo. Privzeto onemogočeno."
---

# Registracija uporabnikov

Privzeto **lastnik/admin dodaja uporabnike ročno** (stran Uporabniki → *Nov uporabnik*). Za white-label namestitve
ki morajo vključevati uporabnike v velikem obsegu — ali integrirati aplikacijo z drugo storitvijo — cMind prav tako pošilja
**varno, samopostrežno pot registracije**. Je **onemogočena privzeto**: stock namestitev je nespremenjena
in stran ter API vrneta 404 dokler namestitev ne vključi.

Obstajata dve vstopni točki ki delita en tok domene:

1. **Stran v aplikaciji** (`/register`) — blagovno znamčena, mobilno-prva stran za vpis v isti lupini kot `/login`.
2. **API za provisioniranje** (`POST /api/provision`) — končna točka strežnik-strežnik za integrirajočo storitev za
   ustvarjanje računov, avtenticirana s skrivnostjo provisioniranja na namestitev.

## Kaj se zabeleži — minimizacija podatkov

cMind je **trgovalno orodje**: gradi/poganja/backtesta cBote in zrcali posle prek
*lastnih* poverilnic cTrader Open API vsakega uporabnika. **Ne odpira trgovalnih računov in ne hrani denarja strank**, torej KYC/AML
preverjanje identitete je **obveznost brokera**, ne te platforme. Obrazec za registracijo zato
zabeleži **privzeto samo e-pošto** — minimum potreben za nudenje storitve (GDPR Člen 5(1)(c) minimizacija podatkov; zakonska podlaga = pogodba). cMind namenoma ne pošilja
polj za nacionalno ID / datum rojstva /
naslov.

Vsak drugi atribut je **opt-in na namestitev** prek `App:Registration:Attributes`, vsak neodvisno
`Off` / `Optional` / `Required`:

| Atribut | Opombe |
|---|---|
| `FullName`, `DisplayName`, `Company` | Prosto besedilo, omejeno dolžino. |
| `Country` | ISO 3166-1 alpha-2, validirano proti fiksnemu naboru kod. |
| `Phone` | E.164 format (`+14155552671`). |
| `Locale` | BCP-47 oblika (`en-US`), normalizirano. |
| `MarketingOptIn` | Ločeno, **neoznačeno** potrditveno polje — nikoli združeno z obveznim soglasjem (CAN-SPAM). |
| `AgeConfirmation` | Samo potrditveno polje; **noben** datum rojstva ni shranjen. |

Atributi živijo v vrednostnem objektu `UserProfile` ki ga ima agregat `AppUser`, validirano ob
konstrukciji. **GDPR izbris** (`AppUser.Anonymize()`) očisti profil in katerokoli verifikacijsko žetona.

**Soglasje.** Ko je `RequireTermsAcceptance` vključeno, mora uporabnik sprejeti objavljene pravne dokumente
(Pogoji, Zasebnost, Razkritje tveganja). Sprejetje je zabeleženo prek obstoječega agregata `ConsentRecord` —
z žigom verzije, časovnim žigom, z izvornim IP — isto shrambo uporabljeno drugod za
evidenco MiFID/ESMA-razreda.

## Načini vrat

Samoregistriran račun se ne more prijaviti dokler ne izpolni svojih vrat (`App:Registration:Mode`):

- **`AdminApproval`** (privzeto) — račun je v čakalni vrsti; lastnik/admin ga odobri na strani **Uporabniki**
  (*Odobritev na čakanju* odsek). Ne potrebuje nobene infrastrukture pošte.
- **`EmailVerification`** — enojna-raba, potekajoča povezava za preverjanje je poslana po e-pošti; račun se aktivira ko
  je povezava odprta. Zahteva e-poštni transport (`App:Email`). **Če noben transport ni konfiguriran, ta način
  avtomatsko degradira v `AdminApproval`** ob zagonu, torej vključitev registracije nikoli tiho ne pokvari.
- **`Open`** — račun je aktiven takoj (zaupano/samo za razvoj).

Samoregistrirani uporabniki so vedno ustvarjeni kot **`User`** (ali `Viewer` če konfigurirano) — domena
**strogo odreče** kovanje Owner/Admin prek samoregistracije.

## Varnost in zaščita pred zlorabo

- **Anti-enumeracija.** Podvojena e-pošta vrne **isto** nevtralno `202 Accepted` kot svež vpis in nič ne ustvari — aplikacija nikoli ne razkrije ali naslov že ima račun.
- **Omejevanje hitrosti.** Javne končne točke so omejene na IP (strožje kot avtentikacijski omejevalnik).
- **Politika gesel.** Minimalna dolžina uveljavljana; gesla so razpršena (Argon2 prek `IPasswordHasher`);
  verifikacijski žetoni so shranjeni samo kot SHA-256 razpršilke in so enojna-raba + potekajoči.
- **Higiena e-pošte.** Izbirni belo-list dovoljenih e-poštnih domen in črna-lista ponudnikovDisposition.
- **CAPTCHA (izbirno).** reCAPTCHA / hCaptcha / Turnstile prek njihove skupne pogodbe za preverjanje.
- **Vrata prijave.** Račun na čakanju je zavrnjen pri prijavi z nevtralnim odgovorom.

## API za provisioniranje (integracija)

Z `App:Registration:Api:Enabled` in nastavljeno `Secret`, lahko druga storitev ustvarja uporabnike:

```
POST /api/provision
X-Provision-Secret: <the configured secret>
{ "email": "user@example.com", "password": "…", "role": 2 }
```

Skrivnost je primerjana v konstantnem času. Provisionirani računi so ustvarjeni **aktivni** (ali povabljeni z
`MustChangePassword`) odvisno od `Api.ActivateImmediately` / `Api.InviteMustChangePassword`.

## Vključitev

Registracija zahteva **obe** zastavico funkcije in glavno stikalo:

```jsonc
"App": {
  "Features": { "Registration": true },
  "Registration": {
    "Enabled": true,
    "Mode": "AdminApproval",           // ali EmailVerification / Open
    "DefaultRole": "User",             // nikoli Owner/Admin
    "RequireTermsAcceptance": true,
    "AllowedEmailDomains": [],          // prazen = katerikoli
    "BlockDisposableEmail": true,
    "Attributes": { "FullName": "Optional", "Country": "Off" },
    "Api": { "Enabled": false, "Secret": "" }
  }
}
```

Odsek `App:Email` (SMTP `Host`, `Port`, `UseStartTls`, `Username`, `Password`, `FromAddress`,
`FromName`) konfigurira transport ki ga uporablja `EmailVerification` način; pustite `Host` nenastavljen da tečete brez
pošte (no-op pošiljatelj). Glej [funkcijske zastavice](./feature-toggles.md) in [white-label](./white-label.md) za
kako namestitve vključujejo funkcije in blagovno znamko. Ko je registracija omogočena, stran za prijavo prikaže povezavo **Ustvari račun**.

## Testirano

Enote (validacija profila, `SelfRegister` straž vlog, prehodi aktivacije, enojna-raba žetona, izbris),
integracija (privzeto-off 404, potek odobritve, degradacija e-poštnega preverjanja, anti-enumeracija, zaščita pred zlorabo,
zahtevani atributi, provisioniranje + napačna skrivnost), in E2E (privzeto-off prijava nima povezave za vpis; stran
`/register` upodobi svojo blagovno zaprto stanje).
