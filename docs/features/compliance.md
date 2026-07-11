# Legal & compliance

Retail FX/CFD/crypto brokerages carry legal and record-keeping obligations. This module implements four
industry-standard pillars: **risk-disclosure consent**, a **tamper-evident audit trail**, **MiFID/ESMA-style
record-keeping**, and **GDPR data rights**. All of it is gated by the `Compliance` feature flag.

## 1. Versioned legal documents + consent

- `LegalDocument` (aggregate) — a versioned Terms of Service, CFD **Risk Disclosure**, or Privacy Policy.
  A version is drafted, then **published**; published versions are **immutable** (editing throws), so the exact
  text a user agreed to is always recoverable. The active document for a type is its highest published version.
- `ConsentRecord` (aggregate) — an immutable record that a user accepted a specific document version at a time,
  with the originating IP.
- **Enforcement:** `RouteGroupBuilder/RouteHandlerBuilder.RequireConsent(type)` blocks an action with `403`
  when a published document of that type exists and the user has not consented to its active version. It is
  applied to **copy-profile creation** (`RiskDisclosure`). When nothing is published, actions are allowed —
  there is nothing to consent to yet — so enabling the module does not retroactively block anything until a
  disclosure is actually published.

## 2. Tamper-evident audit trail

`AuditLog` entries are hash-chained: each row stores `PrevHash` and `Hash = SHA-256(prev | canonical fields)`.
`AuditChainInterceptor` applies the chain transparently at `SaveChanges`, so existing audit call sites need no
change. `IAuditTrailVerifier.VerifyAsync` re-walks the chain and reports the first row whose stored hash or
back-link no longer matches — detecting any edit or deletion of a past record. Owner endpoint:
`GET /api/compliance/audit/verify`.

## 3. Record-keeping (MiFID II / ESMA RTS)

Record-keeping is satisfied by the **immutable, hash-chained audit log** plus **retained consent records** and
soft-deleted (never hard-deleted) domain records. UTC timestamps come from the injected `TimeProvider`. Consent
records keep the document version and IP; published legal documents are never mutated. Retention is a matter of
not purging these tables (they are append-only / soft-delete).

## 4. GDPR data rights

- `GET /api/compliance/export` — a machine-readable export of the caller's data (profile, consents, copy
  profiles, prop-firm challenges).
- `POST /api/compliance/erase` — right to erasure: `AppUser.Anonymize()` scrubs PII (email, MFA) and the row is
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

UI: `/settings/legal` (nav *Settings → Legal & Privacy*, gated by `Compliance`) shows outstanding agreements with accept
buttons and the GDPR export/erase actions.

## Tests

- **Unit** — `UnitTests/Compliance/LegalDocumentTests.cs` (draft/publish/immutability, consent capture),
  `AuditChainTests.cs` (hash links, tamper detection, content sensitivity).
- **Integration** — `IntegrationTests/CompliancePersistenceTests.cs` (active-version + consent queries on real
  Postgres), `AuditChainIntegrityTests.cs` (chain verifies intact, then detects a SQL-level tamper),
  `ComplianceFlowTests.cs` (WebApplicationFactory, isolated DB: consent gate blocks copy creation until the
  risk disclosure is accepted; GDPR export; audit verify).
- **E2E** — `E2ETests/ComplianceTests.cs`: the Legal & Privacy page renders and GDPR export returns the user's
  data in a real browser.
