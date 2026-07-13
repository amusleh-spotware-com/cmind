---
description: "Biztonságos, fehér címkés-kapu önkiszolgáló felhasználó regisztráció — egy alkalmazás-oldali bejelentkezés oldal és egy szerver-a-szerver biztosítási API, konfigurálható felhasználó tulajdonságok, admin-jóváhagyás vagy e-mail-ellenőrzés kapu, és visszaélés-ellenes őrségek. Alapértelmezés szerint letiltva."
---

# Felhasználó regisztráció

Alapértelmezés szerint a **tulajdonos/admin kézi felhasználókat ad hozzá** (Felhasználók oldal → *Új felhasználó*). Fehér címkés telepítésekhez, amelyeknek nagy mértékben kell felhasználókat felvenni — vagy az alkalmazást egy másik szolgáltatóhoz integrálni — cMind egy **biztonságos, önkiszolgáló regisztrációs** útvonalat is szállít. Ez **alapértelmezés szerint letiltva**: egy tőzsdei telepítés megváltozatlan és az oldal és az API egyaránt 404-t adnak vissza amíg egy telepítés be nem opt-in.

Két bejegyzési pont van, amely egy domain áramlást oszt meg:

1. **Alkalmazás-oldali oldal** (`/register`) — egy márka nézete, mobilbarát bejelentkezés oldal az ugyanazon héjban mint a `/login`.
2. **Biztosítási API** (`POST /api/provision`) — egy szerver-a-szerver végpont egy integrált szolgáltató számára fiókok létrehozásához, egy telepítés-biztosítási titkot hitelesítve.

## Mit rögzítünk — adatminimalizálás

A cMind egy **kereskedelem eszköz**: felépít/futtat/backtest cBotokat és tükröz kereskedelmeket az egyes felhasználó *saját* cTrader Open API hitelesítési adatain. Ez **nem nyit kereskedelem fiókokat vagy letétkezelés kliens pénzt**, így KYC/AML identitás ellenőrzés a **broker** kötelezettség, nem ezt a platformot. A regisztrációs forma ezért rögzít **csak egy emailt alapértelmezés szerint** — a szükséges minimum a szolgáltatás biztosításához (GDPR Art. 5(1)(c) adatminimalizálás; jogszerű alap = szerződés). A cMind szándékosan szállít **nincs** nemzeti-ID / születési nap / címzet mezők.

Minden más tulajdonság egy **opt-in telepítés-nként** az `App:Registration:Attributes`-n keresztül, mindegyik függetlenül `Off` / `Optional` / `Required`:

| Tulajdonság | Megjegyzések |
|---|---|
| `FullName`, `DisplayName`, `Company` | Szabad szöveg, hossz-korlátozott. |
| `Country` | ISO 3166-1 alfa-2, validálva egy fix kódlista ellen. |
| `Phone` | E.164 formátum (`+14155552671`). |
| `Locale` | BCP-47 forma (`en-US`), normalizálva. |
| `MarketingOptIn` | Külön, **bepipálatlan** jelölőnégyzet — soha nincs megjelenítve a kötelező beleegyezés (CAN-SPAM). |
| `AgeConfirmation` | Csak egy jelölőnégyzet; **nincs** születési nap tárolva. |

A tulajdonságok az `UserProfile` érték objektumban élnek az `AppUser` aggregátum által birtokolva, validálva a construccióban. **GDPR törlés** (`AppUser.Anonymize()`) megtisztít a profilt és bármilyen ellenőrzési tokeneket.

**Beleegyezés.** Amikor a `RequireTermsAcceptance` bekapcsolt, a felhasználó el kell, hogy fogadja a közzétett jogi dokumentumokat (Feltételek, Adatvédelem, Kockázat nyilatkozat). Az elfogadás az meglévő `ConsentRecord` aggregátumon keresztül rögzített — verzió-bélyegzett, időbélyegzett, eredető IP-vel — ugyanez a boltban használva másutt MiFID/ESMA-fokú nyilvántartás-tartáshoz.

## Kapu módok

Egy önregisztrált fiók nem tud bejelentkezni amíg nem tisztázza kapu (`App:Registration:Mode`):

- **`AdminApproval`** (alapértelmezés) — a fiók sorkában áll; egy tulajdonos/admin jóváhagyja a **Felhasználók** oldalon (*Függőben jóváhagyás* szakasz). Nincs szükséges levél infrastruktúrára.
- **`EmailVerification`** — egy egyszeri, lejáró ellenőrzési link emailt kapott; a fiók aktiválódik amikor az link megnyitódik. Igényel egy levél szállítót (`App:Email`). **Ha nem konfigurált szállító ez a mód automatikusan csökken az `AdminApproval`-hoz** indítás alatt, így az regisztráció engedélyezés soha csendes nem szakít.
- **`Open`** — a fiók azonnali aktív (megbízott/dev csak).

Az önregisztrált felhasználók mindig létrehozottak mint **`User`** (vagy `Viewer` ha konfigurált) — a domain **kemény-visszautasít** az Owner/Admin süllyedése az önregisztráción keresztül.

## Biztonság és visszaélés-ellenes

- **Anti-felsorolás.** A duplikált e-mail az adja **ugyanaz** semleges `202 Accepted` mint egy friss bejelentkezés és semmit nem hoz létre — az alkalmazás soha nem nyilatkozik meg hogy egy cím már rendelkezik fiókkal.
- **Sebességkorlát.** A nyilvános végpontok szabályozottak per IP (nehezebb az auth limiternél).
- **Jelszó politika.** Minimális hossz erőltetett; jelszavak háttárolódnak (Argon2 az `IPasswordHasher` segítségével); az ellenőrzési tokenek tárolódnak csak SHA-256 titkosítottként és egy-felhasználatú + lejáró.
- **E-mail higiénia.** Opcionális engedélyezett listája e-mail tartomány és egy jednorazowy-szállító blokkold-lista.
- **CAPTCHA (opcionális).** reCAPTCHA / hCaptcha / Turnstile az azonos ellenőrzési szerződés segítségével.
- **Bejelentkezés kapu.** Egy függőben fiók visszautasít bejelentkezés a semleges válasz.

## Biztosítási API (integráció)

Az `App:Registration:Api:Enabled` és egy `Secret` beállítva egy másik szolgáltatás lehet felhasználók létrehozása:

```
POST /api/provision
X-Provision-Secret: <the configured secret>
{ "email": "user@example.com", "password": "…", "role": 2 }
```

A titok az összehasonlított az állandó idő. Biztosított fiókok létrehozottak **aktív** (vagy hívva az `MustChangePassword`) attól függően `Api.ActivateImmediately` / `Api.InviteMustChangePassword`.

## Engedélyezés

A regisztráció igényli az **mindkettő** a funkció zaszló és a fő kapcsoló:

```jsonc
"App": {
  "Features": { "Registration": true },
  "Registration": {
    "Enabled": true,
    "Mode": "AdminApproval",           // vagy EmailVerification / Open
    "DefaultRole": "User",             // soha Owner/Admin
    "RequireTermsAcceptance": true,
    "AllowedEmailDomains": [],          // üres = bármi
    "BlockDisposableEmail": true,
    "Attributes": { "FullName": "Optional", "Country": "Off" },
    "Api": { "Enabled": false, "Secret": "" }
  }
}
```

Az `App:Email` szakasz (SMTP `Host`, `Port`, `UseStartTls`, `Username`, `Password`, `FromAddress`, `FromName`) konfigurál a szállítót használva az `EmailVerification` mód; hagyjon `Host` beállítva futni posta nélkül (nincs műveletek szállító). Lásd [funkció kapcsoló](./feature-toggles.md) és [fehér címke](./white-label.md) mennyi telepítéseket kapcsol be a jellemzőket és rebrand. Amikor regisztráció engedélyezve, a bejelentkezés oldal mutatok egy **Fiók létrehozása** link.

## Tesztelt

Egység (profil validáció, `SelfRegister` szerep őr, aktiváció átmenet, egy-felhasználatú tokenek, törlés), integráció (letiltva-alapértelmezés 404, jóváhagyás áramlás, e-mail-ellenőrzés csökkenés, anti-felsorolás, visszaélés őrségek, szükséges tulajdonságok, biztosítás + rossz titok), és E2E (alapértelmezés-ki bejelentkezés nincs bejelentkezés link; a `/register` oldal rendereli az márka zárt állapot).
