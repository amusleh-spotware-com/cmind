---
description: "Maloprodaja FX/CFD/crypto posredovalstvo nositi pravni + vednost-vzdrževanje dolžnosti. Modul implementacija štiri industrija-standardni stebre: tveganje-razprava soglasje…"
---

# Pravno & skladnost

Maloprodaja FX/CFD/crypto posredovalstvo nositi pravni + vednost-vzdrževanje dolžnosti. Modul implementacija štiri industrija-standardni stebre: **tveganje-razprava soglasje**, **tampering-očitna revizija poteka**, **MiFID/ESMA-stil vednost-vzdrževanje**, **GDPR podatkov pravic**. Vsi vrata po `Compliance` značilnost zastava.

## 1. Različičen pravni dokumenti + soglasje

- `LegalDocument` (agregat) — različičen Pogoji servisa, CFD **Tveganje razprava**, ali Politika zasebnosti.
  Različičen osnutek, nato **objavljeni**; objavljeni različičkov **nepromenjiv** (urediti vrze), zato točno besedilo uporabnik
  se nikoli soglasil z vrniti. Aktivni dokument za a tip = njegovega najvišjega objavljeni različičkov.
- `ConsentRecord` (agregat) — nepromenjiv zaznamenovanje, da uporabnik sprejeto specifičnega dokument različičkov na a čas, z izvor IP.
- **Vsiljenja:** `RouteGroupBuilder/RouteHandlerBuilder.RequireConsent(type)` bloki delovanje z `403`
  ko objavljeni dokument tega tipa obstaja in uporabnik ni soglasil s svojega aktivni različičkov. Prijavljen do
  **kopiranje-profil ustvarjanja** (`RiskDisclosure`). Nič objavljeni → dejanja dovoljeni — nič soglasiti — da
  pri tem omogočiti modul bloki nič retroaktivno dokler razprava je resnično objavljeni.

## 2. Tampering-očitna revizija poteka

`AuditLog` vnosi heš-veriga: vsaka vrsta skladišče `PrevHash` in `Hash = SHA-256(prev | kanonski polja)`.
`AuditChainInterceptor` povezati veriga prosojno na `SaveChanges`, zato obstoječi revizija klica spletim nespremenjeno.
`IAuditTrailVerifier.VerifyAsync` ponovno-sprehodi veriga, poročila prva vrsta čigar shranjen heš ali nazaj-povezava ne
več ujemajo — zazna vsak urediti ali izbris past zaznamenovanje. Lastnik končna točka: `GET /api/compliance/audit/verify`.

## 3. Vednost-vzdrževanje (MiFID II / ESMA RTS)

Vednost-vzdrževanje zadovoljen z **nepromenjiv, heš-veriga revizija dnevnik** plus **zadržano soglasje zaznamenovanje** in
mehka-izbris (nikoli trdo-izbris) domeni zaznamenovanje. UTC časovne žigi od injiciran `TimeProvider`. Soglasje
zaznamenovanje obdržati dokument različičkov + IP; objavljeni pravni dokumenti nikoli mutiran. Zadržavanje = ne čistite te
tabele (dodajanje-samo / mehka-izbris).

## 4. GDPR podatkov pravic

- `GET /api/compliance/export` — stroj-berljivo izvoz klicatelja podatkov (profil, consents, kopiranje profile, prop-firma izzivi).
- `POST /api/compliance/erase` — pravica do izbrisa: `AppUser.Anonymize()` skrbi PII (e-pošta, MFA) in vrsta
  mehka-izbris, vzdrževanje referenčni/revizija zgodovina skladna.

## API povzetek

| Način | Pot | Vloga | Namen |
|--------|-------|------|---------|
| GET | `/api/compliance/documents/active` | User+ | aktivni objavljeni dokumenti |
| GET | `/api/compliance/consent/status` | User+ | kateri consents so izstopne |
| POST | `/api/compliance/consent` | User+ | sprejmi aktivni različičkov od a dokument |
| GET | `/api/compliance/export` | User+ | GDPR podatkov izvoz |
| POST | `/api/compliance/erase` | User+ | GDPR izbrisa od lastne račun |
| POST | `/api/compliance/documents` | Owner | osnutek a dokument |
| POST | `/api/compliance/documents/{id}/publish` | Owner | objavi a različičkov |
| GET | `/api/compliance/audit/verify` | Owner | preveri revizija heš veriga |

UI: `/settings/legal` (nav *Nastavitve → Pravno in zasebnost*, vrata po `Compliance`) prikaže izstopni dogovore s sprejmi gumbi + GDPR izvoz/izbrisa dejanja.

## Testi

- **Enota** — `UnitTests/Compliance/LegalDocumentTests.cs` (osnutek/objavi/nepromenjivost, soglasje zaznamenovanje),
  `AuditChainTests.cs` (heš povezave, tampering zaznamenovanje, vsebino občutljivosti).
- **Integracija** — `IntegrationTests/CompliancePersistenceTests.cs` (aktivni-različičkov + soglasje poizvedbe na resnično
  Postgres), `AuditChainIntegrityTests.cs` (veriga preveri nepoškodovana, nato zazna SQL-nivo tampering),
  `ComplianceFlowTests.cs` (WebApplicationFactory, osamljena DB: soglasje vrata bloki kopiranje ustvarjanja dokler tveganje
  razprava sprejeto; GDPR izvoz; revizija preveri).
- **E2E** — `E2ETests/ComplianceTests.cs`: Pravno in zasebnost stran upodobi in GDPR izvoz vrne uporabnik podatkov v realni brskalnik.
