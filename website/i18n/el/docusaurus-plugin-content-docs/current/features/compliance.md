---
description: "Retail FX/CFD/crypto brokerage carry legal + record-keeping duties. Module implement four industry-standard pillars: risk-disclosure consent…"
---

# Legal & compliance

Τα Retail FX/CFD/crypto brokerage έχουν legal + record-keeping duties. Το Module υλοποιεί τέσσερις industry-standard pillars: **risk-disclosure consent**, **tamper-evident audit trail**, **MiFID/ESMA-style record-keeping**, **GDPR data rights**. Όλα gated από `Compliance` feature flag.

## 1. Versioned legal documents + consent

- `LegalDocument` (aggregate) — versioned Terms of Service, CFD **Risk Disclosure**, ή Privacy Policy.
  Η Version drafted, τότε **published**; published versions **immutable** (edit throws), ώστε exact text που ο user
  συμφώνησε πάντα recoverable. Active document για type = highest published version.
- `ConsentRecord` (aggregate) — immutable record ότι ο user δέχθηκε specific document version σε χρόνο, με originating IP.
- **Enforcement:** `RouteGroupBuilder/RouteHandlerBuilder.RequireConsent(type)` blocks action με `403`
  όταν published document του type υπάρχει και ο user δεν έχει συμφωνήσει στο active version. Applied σε
  **copy-profile creation** (`RiskDisclosure`). Τίποτα published → actions allowed — τίποτα να συμφωνήσει ακόμα — ώστε enable module δεν blocks τίποτα retroactively έως published disclosure.

## 2. Tamper-evident audit trail

Τα `AuditLog` entries hash-chained: κάθε row αποθηκεύει `PrevHash` και `Hash = SHA-256(prev | canonical fields)`.
Το `AuditChainInterceptor` εφαρμόζει chain transparently κατά `SaveChanges`, ώστε υπάρχοντα audit call sites unchanged.
`IAuditTrailVerifier.VerifyAsync` re-walks chain, reports πρώτη row της οποίας stored hash ή back-link δεν
ταιριάζει — detects οποιαδήποτε edit ή deletion του past record. Owner endpoint: `GET /api/compliance/audit/verify`.

## 3. Record-keeping (MiFID II / ESMA RTS)

Το Record-keeping ικανοποιείται από **immutable, hash-chained audit log** plus **retained consent records** και
soft-deleted (ποτέ hard-deleted) domain records. UTC timestamps από injected `TimeProvider`. Consent
records κρατούν document version + IP; published legal documents ποτέ δεν mutated. Retention = όχι purging αυτών
tables (append-only / soft-delete).

## 4. GDPR data rights

- `GET /api/compliance/export` — machine-readable export του caller's data (profile, consents, copy profiles, prop-firm challenges).
- `POST /api/compliance/erase` — right to erasure: `AppUser.Anonymize()` καθαρίζει PII (email, MFA) και row
  soft-deleted, keeping referential/audit history coherent.

## API summary

| Method | Route | Role | Purpose |
|--------|-------|------|---------|
| GET | `/api/compliance/documents/active` | User+ | active published documents |
| GET | `/api/compliance/consent/status` | User+ | ποιες συγκαταθέσεις είναι outstanding |
| POST | `/api/compliance/consent` | User+ | δεχτείτε το active version ενός document |
| GET | `/api/compliance/export` | User+ | GDPR data export |
| POST | `/api/compliance/erase` | User+ | GDPR erasure του δικού σας account |
| POST | `/api/compliance/documents` | Owner | draft ένα document |
| POST | `/api/compliance/documents/{id}/publish` | Owner | publish ένα version |
| GET | `/api/compliance/audit/verify` | Owner | verify το audit hash chain |

UI: `/settings/legal` (nav *Settings → Legal & Privacy*, gated από `Compliance`) εμφανίζει outstanding agreements με accept buttons + GDPR export/erase actions.

## Tests

- **Unit** — `UnitTests/Compliance/LegalDocumentTests.cs` (draft/publish/immutability, consent capture),
  `AuditChainTests.cs` (hash links, tamper detection, content sensitivity).
- **Integration** — `IntegrationTests/CompliancePersistenceTests.cs` (active-version + consent queries σε real
  Postgres), `AuditChainIntegrityTests.cs` (chain verifies intact, τότε detects SQL-level tamper),
  `ComplianceFlowTests.cs` (WebApplicationFactory, isolated DB: consent gate blocks copy creation έως risk
  disclosure δεχθεί; GDPR export; audit verify).
- **E2E** — `E2ETests/ComplianceTests.cs`: Legal & Privacy page renders και GDPR export επιστρέφει το data του user σε real browser.
