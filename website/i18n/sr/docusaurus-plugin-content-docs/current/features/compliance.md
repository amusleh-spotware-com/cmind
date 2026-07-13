---
description: "Retail FX/CFD/kripto broker nosi pravne i evidencijske obaveze. Modul implementira četiri industrijska stuba: pristanak na otkrivanje rizika, nepromenjljiv revizijski trag, MiFID/ESMA evidencijsku evidenciju, GDPR prava na podatke."
---

# Pravna usklađenost

Retail FX/CFD/kripto broker nosi pravne i evidencijske obaveze. Modul implementira četiri industrijska stuba: **pristanak na otkrivanje rizika**, **nepromenjljiv revizijski trag**, **MiFID/ESMA vođenje evidencije**, **GDPR prava na podatke**. Sve kontrolisano `Compliance` zastavicom funkcije.

## 1. Verzionirani pravni dokumenti + pristanak

- `LegalDocument` (agregat) — verzionisani Uslovi korišćenja, CFD **Obelodanjivanje rizika**, ili Politika privatnosti.
  Verzija nacrta, zatim se **objavljuje**; objavljene verzije su **nepromenjljive** (izmena baca grešku), tako da je tačan tekst koji je korisnik
  prihvatio uvek povarljiv. Aktivni dokument za tip = njegova najnovija objavljena verzija.
- `ConsentRecord` (agregat) — nepromenjljiv zapis da je korisnik prihvatio određenu verziju dokumenta u vremenu, sa origirajućom IP adresom.
- **Primena:** `RouteGroupBuilder/RouteHandlerBuilder.RequireConsent(type)` blokira akciju sa `403`
  kada objavljeni dokument tog tipa postoji i korisnik nije pristao na njegovu aktivnu verziju. Primenjuje se na
  **kreiranje profila za kopiranje** (`RiskDisclosure`). Ništa nije objavljeno → akcije dozvoljene — nema ničega na šta se
  mora pristati — tako da omogućavanje modula ne blokira ništa retroaktivno dok se obelodanjivanje stvarno ne objavi.

## 2. Nepromenjljiv revizijski trag

`AuditLog` unosi su povezani hash-om: svaki red čuva `PrevHash` i `Hash = SHA-256(prev | kanonska polja)`.
`AuditChainInterceptor` primenjuje lanac transparentno na `SaveChanges`, tako da postojeća mesta poziva revizije ostaju nepromenjena.
`IAuditTrailVerifier.VerifyAsync` ponovo prelazi lanac, prijavljuje prvi red čiji sačuvani hash ili povratna veza se više
ne poklapaju — otkriva bilo koju izmenu ili brisanje prošlog zapisa. Owner endpoint: `GET /api/compliance/audit/verify`.

## 3. Vođenje evidencije (MiFID II / ESMA RTS)

Vođenje evidencije je zadovoljeno **nepromenjljivim, hash-povezanim revizijskim logom** plus **zadržanim zapisima pristanka** i
mekano-obrisanim (nikad tvrdo-obrisanim) domen rekordima. UTC timestamps iz ubrizganog `TimeProvider`. Zapisi pristanka čuvaju
 verziju dokumenta + IP; objavljeni pravni dokumenti se nikad ne mutiraju. Zadržavanje = ne čišćenje ovih
tabela (append-only / mekano-brisanje).

## 4. GDPR prava na podatke

- `GET /api/compliance/export` — mašinski čitljiv izvoz podataka pozivaoca (profil, pristanci, profili za kopiranje, prop-firm izazovi).
- `POST /api/compliance/erase` — pravo na brisanje: `AppUser.Anonymize()` briše PII (email, MFA) i red
  mekano-obriše, čuvajući referencijalnu/ revizijsku istoriju koherentnom.

## Rezime API-ja

| Metod | Ruta | Uloga | Svrha |
|--------|-------|------|---------|
| GET | `/api/compliance/documents/active` | User+ | aktivni objavljeni dokumenti |
| GET | `/api/compliance/consent/status` | User+ | koji pristanci su izostali |
| POST | `/api/compliance/consent` | User+ | prihvati aktivnu verziju dokumenta |
| GET | `/api/compliance/export` | User+ | GDPR izvoz podataka |
| POST | `/api/compliance/erase` | User+ | GDPR brisanje sopstvenog naloga |
| POST | `/api/compliance/documents` | Owner | nacrtuj dokument |
| POST | `/api/compliance/documents/{id}/publish` | Owner | objavi verziju |
| GET | `/api/compliance/audit/verify` | Owner | verifikuj hash lanac revizije |

UI: `/settings/legal` (nav *Settings → Legal & Privacy*, kontrolisano `Compliance`) prikazuje izostale ugovore sa dugmadima za prihvatanje + GDPR izvoz/brisanje akcije.

## Testovi

- **Unit** — `UnitTests/Compliance/LegalDocumentTests.cs` (nacrt/objava/nepromenjljivost, hvatanje pristanka),
  `AuditChainTests.cs` (hash linkovi, detekcija manipulacije, osetljivost sadržaja).
- **Integration** — `IntegrationTests/CompliancePersistenceTests.cs` (upiti aktivne verzije + pristanka na realnom
  Postgres), `AuditChainIntegrityTests.cs` (lanac verifikuje netaknut, zatim detektuje SQL-nivo manipulaciju),
  `ComplianceFlowTests.cs` (WebApplicationFactory, izolovani DB: consent gate blokira kreiranje kopije dok se rizik
  obelodanjivanje ne prihvati; GDPR izvoz; audit verifikacija).
- **E2E** — `E2ETests/ComplianceTests.cs`: Stranica Pravna & Privatnost se prikazuje i GDPR izvoz vraća korisnikove podatke u realnom browser-u.
