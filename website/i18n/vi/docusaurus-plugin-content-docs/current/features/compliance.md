---
description: "Retail FX/CFD/crypto brokerage mang nghĩa vụ pháp lý + giữ hồ sơ. Module implement bốn trụ cột tiêu chuẩn ngành: risk-disclosure consent…"
---

# Legal & compliance

Retail FX/CFD/crypto brokerage mang nghĩa vụ pháp lý + giữ hồ sơ. Module implement bốn trụ cột tiêu chuẩn ngành: **risk-disclosure consent**, **tamper-evident audit trail**, **MiFID/ESMA-style record-keeping**, **GDPR data rights**. Tất cả gated by `Compliance` feature flag.

## 1. Versioned legal documents + consent

- `LegalDocument` (aggregate) — versioned Terms of Service, CFD **Risk Disclosure**, hoặc Privacy Policy.
  Version drafted, rồi **published**; published versions **immutable** (edit throws), vì vậy exact text user
  agreed to luôn recoverable. Active document cho một type = its highest published version.
- `ConsentRecord` (aggregate) — immutable record rằng user accepted specific document version at a time, với originating IP.
- **Enforcement:** `RouteGroupBuilder/RouteHandlerBuilder.RequireConsent(type)` blocks action với `403`
  khi published document of that type exists và user not consented to its active version. Applied to
  **copy-profile creation** (`RiskDisclosure`). Nothing published → actions allowed — không có gì để consent to
  yet — vì vậy enabling module blocks nothing retroactively cho đến khi disclosure actually published.

## 2. Tamper-evident audit trail

`AuditLog` entries hash-chained: each row stores `PrevHash` và `Hash = SHA-256(prev | canonical fields)`.
`AuditChainInterceptor` applies chain transparently at `SaveChanges`, vì vậy existing audit call sites unchanged.
`IAuditTrailVerifier.VerifyAsync` re-walks chain, reports first row whose stored hash hoặc back-link no longer
matches — detects any edit or deletion of past record. Owner endpoint: `GET /api/compliance/audit/verify`.

## 3. Record-keeping (MiFID II / ESMA RTS)

Record-keeping satisfied by **immutable, hash-chained audit log** cộng **retained consent records** và
soft-deleted (không bao giờ hard-deleted) domain records. UTC timestamps from injected `TimeProvider`. Consent
records keep document version + IP; published legal documents never mutated. Retention = không purge các
tables này (append-only / soft-delete).

## 4. GDPR data rights

- `GET /api/compliance/export` — machine-readable export của caller's data (profile, consents, copy profiles, prop-firm challenges).
- `POST /api/compliance/erase` — right to erasure: `AppUser.Anonymize()` scrubs PII (email, MFA) và row
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
  Postgres), `AuditChainIntegrityTests.cs` (chain verifies intact, rồi detects SQL-level tamper),
  `ComplianceFlowTests.cs` (WebApplicationFactory, isolated DB: consent gate blocks copy creation cho đến khi risk
  disclosure accepted; GDPR export; audit verify).
- **E2E** — `E2ETests/ComplianceTests.cs`: Legal & Privacy page renders và GDPR export returns user's data in real browser.
