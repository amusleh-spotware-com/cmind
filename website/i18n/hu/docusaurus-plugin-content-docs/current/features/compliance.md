---
description: "Retail FX/CFD/kripto brókerkeds jogi es nyilvántartási kötelezettségekkel jár. A modul négy iparági standard pillért implementál: kockázat-közzététel elfogadás, hamisításbiztos audit nyomvonal, MiFID/ESMA-stílusú nyilvántartás, GDPR adategyek."
---

# Jogi es megfeleloség

Retail FX/CFD/kripto brókerkeds jogi es nyilvántartási kötelezettségekkel jár. A modul négy iparági standard pillért implementál: **kockázat-közzététel elfogadás**, **hamisításbiztos audit nyomvonal**, **MiFID/ESMA-stílusú nyilvántartás**, **GDPR adategyek**. Mind gate-elt a `Compliance` feature flag-gel.

## 1. Verzionált jogi dokumentumok + elfogadás

- `LegalDocument` (agregatum) - verzionált ÁSZF, CFD **Kockázat Közzététel**, vagy Adatvédelmi szabályzat. Verzió tervezve, majd **publikálva**; publikált verziók **immutabilisak** (szerkesztés dob), igy a felhasználó altal elfogadott pontos szöveg mindig visszanyerhető. Aktív dokumentum egy tipushoz = annak legmagasabb publikált verziója.
- `ConsentRecord` (agregatum) - immutable rekord, hogy a felhasználó elfogadott egy specifikus dokumentum verziót egy idobelyeggel, az eredeti IP-vel.
- **Kényszerítés:** `RouteGroupBuilder/RouteHandlerBuilder.RequireConsent(type)` blokkolja a műveletet `403`-mal, amikor egy publikált dokumentum létezik az adott tipushoz és a felhasználó nem fogadta el annak aktív verzióját. Alkalmazva a **másolási profil létrehozásra** (`RiskDisclosure`). Semmi publikálva → műveletek engedélyezettek - nincs még mit elfogadni - szóval a modul bekapcsolása nem blokkol semmit visszamenőleg, amíg a közzététel ténylegesen meg nem jelenik.

## 2. Hamisításbiztos audit nyomvonal

`AuditLog` bejegyzések hash-láncban: mindegyik sor tárolja a `PrevHash`-t és a `Hash = SHA-256(prev | kanonikus mezők)`. `AuditChainInterceptor` alkalmazza a láncot transzparensen a `SaveChanges`-nál, igy a meglévő audit hívási helyek változatlanok. `IAuditTrailVerifier.VerifyAsync` újra járja a láncot, jelenti az első sort, amelynek tárolt hash vagy visszamutató link már nem egyezik - bármely szerkesztést vagy törlést detektál a múltbeli rekordban. Tulajdonos vegpont: `GET /api/compliance/audit/verify`.

## 3. Nyilvántartás (MiFID II / ESMA RTS)

A nyilvántartási követelmény a **immutable, hash-láncolt audit log** plus **megtartott elfogadási rekordok** és soft-törölt (soha nem hard-törölt) domain rekordok által teljesül. UTC idobelyegek az injektált `TimeProvider`-ből. Az elfogadási rekordok a dokumentum verziót + IP-t tartják; a publikált jogi dokumentumok sosem mutálódtak. Megtartás = ezeknek a tábláknak a nem-purgézása (append-only / soft-delete).

## 4. GDPR adategyek

- `GET /api/compliance/export` - gép olvasható export a hívó adatairól (profil, elfogadások, másolási profilok, prop-firm kihavások).
- `POST /api/compliance/erase` - törlési jog: `AppUser.Anonymize()` kitörli a PII-t (email, MFA) és a sort soft-törli, megtartva a referenciális/audit történetet koherensen.

## API osssefoglalo

| Metodus | Utvonal | Szerep | Cel |
|--------|--------|--------|-----|
| GET | `/api/compliance/documents/active` | User+ | aktív publikált dokumentumok |
| GET | `/api/compliance/consent/status` | User+ | mely elfogadások elavultak |
| POST | `/api/compliance/consent` | User+ | elfogadja a dokumentum aktív verzióját |
| GET | `/api/compliance/export` | User+ | GDPR adatexport |
| POST | `/api/compliance/erase` | User+ | GDPR törlés a sajat fiokjáról |
| POST | `/api/compliance/documents` | Owner | dokumentumot tervez |
| POST | `/api/compliance/documents/{id}/publish` | Owner | publikál egy verziót |
| GET | `/api/compliance/audit/verify` | Owner | ellenőrzi az audit hash láncot |

UI: `/settings/legal` (nav *Beallitasok - Jogi es Adatvedelem*, gate-elve `Compliance`-tal) mutatja a függőben lévő megállapodásokat elfogadás gombokkal + GDPR export/törlés műveletekkel.

## Tesztek

- **Unit** - `UnitTests/Compliance/LegalDocumentTests.cs` (tervezet/publikálás/immutabilitas, elfogadás rögzítés), `AuditChainTests.cs` (hash linkek, hamisítás detekció, tartalom érzékenység).
- **Integráció** - `IntegrationTests/CompliancePersistenceTests.cs` (aktív-verzió + elfogadás lekérdezések valódi Postgres-en), `AuditChainIntegrityTests.cs` (lánc ellenőrzi sértetlen, aztán SQL-szintű hamisítást detektál), `ComplianceFlowTests.cs` (WebApplicationFactory, izolált DB: elfogadás gate blokkol másolás létrehozást amíg kockázat közzététel nem elfogadott; GDPR export; audit verify).
- **E2E** - `E2ETests/ComplianceTests.cs`: Jogi és Adatvédelem oldal renderel és a GDPR export visszaadja a felhasználó adatait a valódi böngészőben.
