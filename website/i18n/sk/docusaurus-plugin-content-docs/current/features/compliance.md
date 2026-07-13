---
description: "Retail FX/CFD/crypto brokerage carry legal + record-keeping duties. Module implement štyri industry-standard pillars: risk-disclosure consent..."
---

# Legal & compliance

Retail FX/CFD/crypto brokerage carry legal + record-keeping duties. Module implement štyri industry-standard pillars: **risk-disclosure consent**, **tamper-evident audit trail**, **MiFID/ESMA-style record-keeping**, **GDPR data rights**. Všetko gated by `Compliance` feature flag.

## 1. Versioned legal documents + consent

- `LegalDocument` (aggregate) — versioned Terms of Service, CFD **Risk Disclosure** alebo Privacy Policy.
  Version drafted, potom **published**; published versions **immutable** (edit throws), takže exact text user
  souhlasil s vždy recoverable. Active document pre typ = jeho highest published version.
- `ConsentRecord` (aggregate) — immutable record, že user accepted specific document version v čase, s originating IP.
- **Enforcement:** `RouteGroupBuilder/RouteHandlerBuilder.RequireConsent(type)` blocks action s `403`
  keď published document toho type existuje a user nie consented na jej active version. Applied na
  **copy-profile creation** (`RiskDisclosure`). Nič published → actions allowed — nic na consent
  yet — takže enabling module blocks nic retroactively, kým disclosure skutočne published.

## 2. Tamper-evident audit trail

`AuditLog` entries hash-chained: každý row stores `PrevHash` a `Hash = SHA-256(prev | canonical fields)`.
`AuditChainInterceptor` applies chain transparently na `SaveChanges`, takže existing audit call sites unchanged.
`IAuditTrailVerifier.VerifyAsync` re-walks chain, reports prvý row, ktorého stored hash alebo back-link žiadny longer
matches — detects akúkoľvek edit alebo deletion z past record. Owner endpoint: `GET /api/compliance/audit/verify`.

## 3. Record-keeping (MiFID II / ESMA RTS)

Record-keeping satisfied by **immutable, hash-chained audit log** plus **retained consent records** a
soft-deleted (nikdy hard-deleted) domain records. UTC timestamps z injected `TimeProvider`. Consent
records keep document version + IP; published legal documents nikdy mutated. Retention = žádne purging tých
tables (append-only / soft-delete).

## 4. GDPR data rights

- `GET /api/compliance/export` — machine-readable export z caller dáta (profile, consents, copy profiles, prop-firm challenges).
- `POST /api/compliance/erase` — právo na erasure: `AppUser.Anonymize()` scrubs PII (email, MFA) a row
  soft-deleted, keeping referential/audit history coherent.

## API summary
