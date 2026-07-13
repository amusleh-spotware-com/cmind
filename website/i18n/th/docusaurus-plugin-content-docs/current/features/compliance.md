---
description: "Retail FX/CFD/crypto brokerage carry legal + record-keeping duties module implement four industry-standard pillars: risk-disclosure consent tamper-evident audit trail MiFID/ESMA-style record-keeping GDPR data rights ทั้งหมด gated โดย Compliance feature flag"
---

# Legal & compliance

retail FX/CFD/crypto brokerage carry legal + record-keeping duties module implement four industry-standard pillars: **risk-disclosure consent** **tamper-evident audit trail** **MiFID/ESMA-style record-keeping** **GDPR data rights** ทั้งหมด gated โดย `Compliance` feature flag

## 1. versioned legal documents + consent

- `LegalDocument` (aggregate) — versioned terms ของ service CFD **Risk Disclosure** หรือ privacy policy version drafted แล้ว **published**; published versions **immutable** (edit throws) ดังนั้น exact text user agreed เป็น always recoverable active document สำหรับ type = highest published version ของมัน
- `ConsentRecord` (aggregate) — immutable record ที่ user accepted specific document version ที่ time ด้วย originating IP
- **Enforcement:** `RouteGroupBuilder/RouteHandlerBuilder.RequireConsent(type)` blocks action ด้วย `403` เมื่อ published document ของ type นั้น exists และ user ไม่ consented เป็น active version ของมัน applied เป็น **copy-profile creation** (`RiskDisclosure`) ไม่มีอะไร published → actions allowed — ไม่มีอะไร เพื่อ consent เป็น yet — ดังนั้น enabling module blocks ไม่มีอะไร retroactively จนกว่า disclosure actually published

## 2. Tamper-evident audit trail

`AuditLog` entries hash-chained: ทุก ๆ row stores `PrevHash` และ `Hash = SHA-256(prev | canonical fields)` `AuditChainInterceptor` applies chain transparently ที่ `SaveChanges` ดังนั้น existing audit call sites unchanged `IAuditTrailVerifier.VerifyAsync` re-walks chain reports first row whose stored hash หรือ back-link ไม่มา longer matches — detects any edit หรือ deletion ของ past record owner endpoint: `GET /api/compliance/audit/verify`

## 3. Record-keeping (MiFID II / ESMA RTS)

record-keeping satisfied โดย **immutable hash-chained audit log** บวก **retained consent records** และ soft-deleted (ไม่เคย hard-deleted) domain records UTC timestamps จาก injected `TimeProvider` consent records keep document version + IP; published legal documents ไม่เคย mutated retention = ไม่ purging เหล่านั้น tables (append-only / soft-delete)

## 4. GDPR data rights

- `GET /api/compliance/export` — machine-readable export ของ caller's data (profile consents copy profiles prop-farm challenges)
- `POST /api/compliance/erase` — right เป็น erasure: `AppUser.Anonymize()` scrubs PII (email MFA) และ row soft-deleted keeping referential/audit history coherent

## API summary

| Method | Route | Role | Purpose |
|--------|-------|------|---------|
| GET | `/api/compliance/documents/active` | user+ | active published documents |
| GET | `/api/compliance/consent/status` | user+ | which consents outstanding |
| POST | `/api/compliance/consent` | user+ | accept active version ของ document |
| GET | `/api/compliance/export` | user+ | GDPR data export |
| POST | `/api/compliance/erase` | user+ | GDPR erasure ของ own account |
| POST | `/api/compliance/documents` | owner | draft document |
| POST | `/api/compliance/documents/{id}/publish` | owner | publish version |
| GET | `/api/compliance/audit/verify` | owner | verify audit hash chain |

UI: `/settings/legal` (nav *settings → legal & privacy* gated โดย `Compliance`) แสดง outstanding agreements ด้วย accept buttons + GDPR export/erase actions

## Tests

- **Unit** — `UnitTests/Compliance/LegalDocumentTests.cs` (draft/publish/immutability consent capture) `AuditChainTests.cs` (hash links tamper detection content sensitivity)
- **Integration** — `IntegrationTests/CompliancePersistenceTests.cs` (active-version + consent queries บน real postgres) `AuditChainIntegrityTests.cs` (chain verifies intact แล้ว detects SQL-level tamper) `ComplianceFlowTests.cs` (WebApplicationFactory isolated DB: consent gate blocks copy creation จนกว่า risk disclosure accepted; GDPR export; audit verify)
- **E2E** — `E2ETests/ComplianceTests.cs`: legal & privacy page renders และ GDPR export returns user's data ใน real browser
