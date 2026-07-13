---
description: "Retail FX/CFD/crypto brokerage nýt právní + record-keeping povinnosti. Module implementace čtyři industry-standard pilíře: risk-disclosure souhlas…"
---

# Právní & compliance

Retail FX/CFD/crypto brokerage nýt právní + record-keeping povinnosti. Module implementace čtyř industry-standard pilířů: **risk-disclosure souhlas**, **tamper-evident audit trail**, **MiFID/ESMA-style record-keeping**, **GDPR data rights**. Vše gated by `Compliance` feature flag.

## 1. Verzované právní dokumenty + souhlas

- `LegalDocument` (agregát) — verzované Terms of Service, CFD **Risk Disclosure**, nebo Privacy Policy. Verze navrhnuta, pak **publikována**; publikované verze **immutable** (edit hází), takže přesný text uživatel souhlasil vždy obnovitelný. Aktivní dokument pro typ = jeho nejvyšší publikovaná verze.
- `ConsentRecord` (agregát) — immutable záznam, že uživatel přijal specifickou verzi dokumentu v čase, s origin IP.
- **Vynucování:** `RouteGroupBuilder/RouteHandlerBuilder.RequireConsent(type)` blokuje akci s `403` když publikovaný dokument typu existuje a uživatel nesouhlasil s jeho aktivní verzí. Aplikován na **copy-profile creation** (`RiskDisclosure`). Nic publikované → akce povoleny — nic co souhlasit ještě — takže umožnění module blokuje nic retroaktivně dokud disclosure opravdu publikované.

## 2. Tamper-evident audit trail

`AuditLog` vstupy hash-chained: každý řádek ukládá `PrevHash` a `Hash = SHA-256(prev | canonical fields)`. `AuditChainInterceptor` aplikuje řetězec transparentně na `SaveChanges`, takže existující audit call sites beze změny. `IAuditTrailVerifier.VerifyAsync` re-walks řetězec, hlásí první řádek jehož uložení hash nebo zpět-link už neodpovídá — detekuje jakýkoliv edit nebo mazání minulého záznamu. Owner endpoint: `GET /api/compliance/audit/verify`.

## 3. Record-keeping (MiFID II / ESMA RTS)

Record-keeping spokojen **immutable, hash-chained audit log** plus **retained consent records** a soft-deleted (nikdy hard-deleted) doménové záznamy. UTC timestamps z injected `TimeProvider`. Consent záznamy keep document verze + IP; publikované právní dokumenty nikdy mutovány. Retence = ne purge tyto tabulky (append-only / soft-delete).

## 4. GDPR data práva

- `GET /api/compliance/export` — machine-readable export caller's dat (profil, souhlasy, copy profily, prop-firm výzvy).
- `POST /api/compliance/erase` — právo na vymazání: `AppUser.Anonymize()` scrub PII (email, MFA) a řádek soft-deleted, keeping referential/audit historii koherentní.

## API shrnutí

| Metoda | Trasa | Role | Účel |
|--------|-------|------|---------|
| GET | `/api/compliance/documents/active` | User+ | aktivní publikované dokumenty |
| GET | `/api/compliance/consent/status` | User+ | které souhlasy jsou outstanding |
| POST | `/api/compliance/consent` | User+ | přijmout aktivní verzi dokumentu |
| GET | `/api/compliance/export` | User+ | GDPR data export |
| POST | `/api/compliance/erase` | User+ | GDPR mazání vlastního účtu |
| POST | `/api/compliance/documents` | Owner | návrh dokumentu |
| POST | `/api/compliance/documents/{id}/publish` | Owner | publikovat verzi |
| GET | `/api/compliance/audit/verify` | Owner | ověř audit hash řetěz |

UI: `/settings/legal` (nav *Settings → Legal & Privacy*, gated by `Compliance`) ukazuje outstanding dohod s accept tlačítka + GDPR export/erase akce.

## Testy

- **Unit** — `UnitTests/Compliance/LegalDocumentTests.cs` (draft/publish/immutability, consent capture), `AuditChainTests.cs` (hash linky, tamper detekce, content sensitivity).
- **Integration** — `IntegrationTests/CompliancePersistenceTests.cs` (active-version + consent dotazy na reálném Postgres), `AuditChainIntegrityTests.cs` (chain ověřuje intact, pak detekuje SQL-level tamper), `ComplianceFlowTests.cs` (WebApplicationFactory, izolovaná DB: consent gate blokuje copy creation dokud risk disclosure přijato; GDPR export; audit verify).
- **E2E** — `E2ETests/ComplianceTests.cs`: Legal & Privacy stránka renders a GDPR export vrátí user's data v reálném prohlížeči.
