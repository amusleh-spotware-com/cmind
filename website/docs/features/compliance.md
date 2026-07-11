# Legal & compliance

Retail FX/CFD/crypto brokerage carry legal + record-keeping duties. Module implement four industry-standard pillars: **risk-disclosure consent**, **tamper-evident audit trail**, **MiFID/ESMA-style record-keeping**, **GDPR data rights**. All gated by `Compliance` feature flag.

## 1. Versioned legal documents + consent

- `LegalDocument` (aggregate) — versioned Terms of Service, CFD **Risk Disclosure**, or Privacy Policy.
  Version drafted, then **published**; published versions **immutable** (edit throws), so exact text user
  agreed to always recoverable. Active document for a type = its highest published version.
- `ConsentRecord` (aggregate) — immutable record that user accepted specific document version at a time, with originating IP.
- **Enforcement:** `RouteGroupBuilder/RouteHandlerBuilder.RequireConsent(type)` blocks action with `403`
  when published document of that type exists and user not consented to its active version. Applied to
  **copy-profile creation** (`RiskDisclosure`). Nothing published → actions allowed — nothing to consent to
  yet — so enabling module blocks nothing retroactively until disclosure actually published.

## 2. Tamper-evident audit trail

`AuditLog` entries hash-chained: each row stores `PrevHash` and `Hash = SHA-256(prev | canonical fields)`.
`AuditChainInterceptor` applies chain transparently at `SaveChanges`, so existing audit call sites unchanged.
`IAuditTrailVerifier.VerifyAsync` re-walks chain, reports first row whose stored hash or back-link no longer
matches — detects any edit or deletion of past record. Owner endpoint: `GET /api/compliance/audit/verify`.

## 3. Record-keeping (MiFID II / ESMA RTS)

Record-keeping satisfied by **immutable, hash-chained audit log** plus **retained consent records** and
soft-deleted (never hard-deleted) domain records. UTC timestamps from injected `TimeProvider`. Consent
records keep document version + IP; published legal documents never mutated. Retention = not purging these
tables (append-only / soft-delete).

## 4. GDPR data rights

- `GET /api/compliance/export` — machine-readable export of caller's data (profile, consents, copy profiles, prop-firm challenges).
- `POST /api/compliance/erase` — right to erasure: `AppUser.Anonymize()` scrubs PII (email, MFA) and row
  soft-deleted, keeping referential/audit history coherent.

## API summary

| Method | Route | Role | Purpose |
|--------|-------|------|---------|
| GET | `/api/compliance/documents/active` | User+ | active published documents |
| GET | `/api/compliance/consent/status` | User+ | which consents are outstanding |
| POST | `/api/compliance/consent` | User+ | accept the active version of a document |
| GET | `/api/compliance/export` | User+ | GDPR data export |
| POST | `/api/compliance/erase` | User+ | GDPR erasure of own account |
| POST | `/api/compliance/documents` | Owner | draft a document |
| POST | `/api/compliance/documents/{id}/publish` | Owner | publish a version |
| GET | `/api/compliance/audit/verify` | Owner | verify the audit hash chain |

UI: `/settings/legal` (nav *Settings → Legal & Privacy*, gated by `Compliance`) shows outstanding agreements with accept buttons + GDPR export/erase actions.

## Tests

- **Unit** — `UnitTests/Compliance/LegalDocumentTests.cs` (draft/publish/immutability, consent capture),
  `AuditChainTests.cs` (hash links, tamper detection, content sensitivity).
- **Integration** — `IntegrationTests/CompliancePersistenceTests.cs` (active-version + consent queries on real
  Postgres), `AuditChainIntegrityTests.cs` (chain verifies intact, then detects SQL-level tamper),
  `ComplianceFlowTests.cs` (WebApplicationFactory, isolated DB: consent gate blocks copy creation until risk
  disclosure accepted; GDPR export; audit verify).
- **E2E** — `E2ETests/ComplianceTests.cs`: Legal & Privacy page renders and GDPR export returns user's data in real browser.