---
description: "Retail FX/CFD/crypto brokerage carry legal + record-keeping duties. Module implementuje cztery industry-standard pillars: risk-disclosure consent…"
---

# Prawna & compliance

Retail FX/CFD/crypto brokerage carry legal + record-keeping duties. Module implementuje cztery industry-standard pillars: **risk-disclosure consent**, **tamper-evident audit trail**, **MiFID/ESMA-style record-keeping**, **GDPR data rights**. Wszystkie gated przez `Compliance` feature flag.

## 1. Wersjonowane dokumenty prawne + consent

- `LegalDocument` (aggregate) — wersjonowane Terms of Service, CFD **Risk Disclosure**, lub Privacy Policy.
  Version drafted, wtedy **published**; published versions **immutable** (edit throws), więc dokładna
  tekst użytkownika zawsze recoverable. Active document typu = jego highest published version.
- `ConsentRecord` (aggregate) — immutable record że użytkownik zaakceptował specific document version w time, z originating IP.
- **Enforcement:** `RouteGroupBuilder/RouteHandlerBuilder.RequireConsent(type)` blokuje action z `403`
  gdy published document tego typu istnieje i user nie consented do active version. Zastosowany do
  **copy-profile creation** (`RiskDisclosure`). Nic published → actions allowed — nic do consent do
  teraz — więc enabling module nic nie blokuje retroactively dopóki disclosure rzeczywiście nie published.

## 2. Tamper-evident audit trail

`AuditLog` entries hash-chained: każda rząd przechowuje `PrevHash` i `Hash = SHA-256(prev | canonical fields)`.
`AuditChainInterceptor` stosuje chain transparentnie przy `SaveChanges`, więc istniejące audit call sites unchanged.
`IAuditTrailVerifier.VerifyAsync` re-walks chain, reports pierwszy rząd którego stored hash lub back-link
już nie pasuje — detects każdą edit lub deletion past record. Owner endpoint: `GET /api/compliance/audit/verify`.

## 3. Record-keeping (MiFID II / ESMA RTS)

Record-keeping satisfied przez **immutable, hash-chained audit log** plus **retained consent records** i
soft-deleted (nigdy hard-deleted) domain records. UTC timestamps z injected `TimeProvider`. Consent
records keep document version + IP; published legal documents nigdy mutated. Retention = nie purging
te tabele (append-only / soft-delete).

## 4. GDPR data rights

- `GET /api/compliance/export` — machine-readable export caller's data (profile, consents, copy profiles, prop-firm challenges).
- `POST /api/compliance/erase` — prawo do erasure: `AppUser.Anonymize()` scrubs PII (email, MFA) i rząd
  soft-deleted, keeping referential/audit history coherent.

## API summary

| Method | Route | Role | Cel |
|--------|-------|------|---------|
| GET | `/api/compliance/documents/active` | User+ | active published documents |
| GET | `/api/compliance/consent/status` | User+ | które consents są outstanding |
| POST | `/api/compliance/consent` | User+ | accept active version dokumentu |
| GET | `/api/compliance/export` | User+ | GDPR data export |
| POST | `/api/compliance/erase` | User+ | GDPR erasure own account |
| POST | `/api/compliance/documents` | Owner | draft dokument |
| POST | `/api/compliance/documents/{id}/publish` | Owner | publish wersję |
| GET | `/api/compliance/audit/verify` | Owner | verify audit hash chain |

UI: `/settings/legal` (nav *Settings → Legal & Privacy*, gated przez `Compliance`) shows outstanding agreements z accept buttons + GDPR export/erase actions.

## Testy

- **Unit** — `UnitTests/Compliance/LegalDocumentTests.cs` (draft/publish/immutability, consent capture),
  `AuditChainTests.cs` (hash links, tamper detection, content sensitivity).
- **Integracja** — `IntegrationTests/CompliancePersistenceTests.cs` (active-version + consent queries na real
  Postgres), `AuditChainIntegrityTests.cs` (chain verifies intact, wtedy detects SQL-level tamper),
  `ComplianceFlowTests.cs` (WebApplicationFactory, isolated DB: consent gate blokuje copy creation dopóki risk
  disclosure accepted; GDPR export; audit verify).
- **E2E** — `E2ETests/ComplianceTests.cs`: Legal & Privacy page renderuje i GDPR export zwraca user's data w real browser.
