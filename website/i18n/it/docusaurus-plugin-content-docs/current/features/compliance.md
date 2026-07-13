---
description: "Il brokerage retail FX/CFD/crypto ha obblighi legali e di tenuta registri. Il modulo implementa quattro pilastri standard di settore: consenso risk-disclosure…"
---

# Legale & compliance

Il brokerage retail FX/CFD/crypto porta obblighi legali e di tenuta registri. Il modulo implementa quattro
pilastri standard di settore: **consenso risk-disclosure**, **audit trail tamper-evident**,
**tenuta registri stile MiFID/ESMA**, **diritti GDPR**. Tutti gated da feature flag `Compliance`.

## 1. Documenti legali versionati + consenso

- `LegalDocument` (aggregate) — Terms of Service versionati, CFD **Risk Disclosure**, o Privacy Policy.
  Version draftata, poi **published**; versioni published **immutable** (edit lancia), così il testo
  esatto che l'utente ha accettato è sempre recuperabile. Documento attivo per un tipo = la sua versione
  published più alta.
- `ConsentRecord` (aggregate) — record immutabile che l'utente ha accettato una specifica versione documento
  a un orario, con IP di origine.
- **Applicazione:** `RouteGroupBuilder/RouteHandlerBuilder.RequireConsent(type)` blocca l'azione con `403`
  quando esiste un documento published di quel tipo e l'utente non ha acconsentito alla sua versione attiva.
  Applicato alla **creazione copy-profile** (`RiskDisclosure`). Niente published → azioni permesse — niente
  a cui acconsentire ancora — quindi abilitare il modulo non blocca nulla retroattivamente finché il
  disclosure non è effettivamente pubblicato.

## 2. Audit trail tamper-evident

`AuditLog` entries hash-chained: ogni riga memorizza `PrevHash` e `Hash = SHA-256(prev | canonical fields)`.
`AuditChainInterceptor` applica la catena transparentemente a `SaveChanges`, così i call site audit
esistenti unchanged. `IAuditTrailVerifier.VerifyAsync` re-walks la catena, riporta la prima riga il cui
stored hash o back-link non corrisponde più — rileva qualsiasi edit o cancellazione di record passato.
Endpoint owner: `GET /api/compliance/audit/verify`.

## 3. Tenuta registri (MiFID II / ESMA RTS)

La tenuta registri soddisfatta dal **log audit immutabile, hash-chained** più **record di consenso
trattenuti** e record di dominio soft-deleted (mai hard-deleted). Timestamp UTC da `TimeProvider` iniettato.
I record di consenso tengono versione documento + IP; i documenti legali published non sono mai mutati.
Retention = non purgare queste tabelle (append-only / soft-delete).

## 4. Diritti GDPR

- `GET /api/compliance/export` — export machine-readable dei dati del chiamante (profile, consents,
  copy profiles, prop-firm challenges).
- `POST /api/compliance/erase` — diritto alla cancellazione: `AppUser.Anonymize()` scrubba PII
  (email, MFA) e la riga è soft-deleted, mantenendo la storia referenziale/audit coerente.

## API summary

| Method | Route | Role | Scopo |
|--------|-------|------|---------|
| GET | `/api/compliance/documents/active` | User+ | documenti published attivi |
| GET | `/api/compliance/consent/status` | User+ | quali consensi sono outstanding |
| POST | `/api/compliance/consent` | User+ | accetta la versione attiva di un documento |
| GET | `/api/compliance/export` | User+ | export dati GDPR |
| POST | `/api/compliance/erase` | User+ | cancellazione GDPR del proprio account |
| POST | `/api/compliance/documents` | Owner | drafta un documento |
| POST | `/api/compliance/documents/{id}/publish` | Owner | pubblica una versione |
| GET | `/api/compliance/audit/verify` | Owner | verifica la catena hash audit |

UI: `/settings/legal` (nav *Settings → Legal & Privacy*, gated da `Compliance`) mostra gli accordi
outstanding con pulsanti di accettazione + azioni GDPR export/erase.

## Test

- **Unit** — `UnitTests/Compliance/LegalDocumentTests.cs` (draft/publish/immutability, consent capture),
  `AuditChainTests.cs` (hash links, tamper detection, content sensitivity).
- **Integration** — `IntegrationTests/CompliancePersistenceTests.cs` (active-version + consent queries su
  Postgres reale), `AuditChainIntegrityTests.cs` (catena verifica intatta, poi rileva SQL-level tamper),
  `ComplianceFlowTests.cs` (WebApplicationFactory, DB isolated: consent gate blocca copy creation finché
  risk disclosure accettato; GDPR export; audit verify).
- **E2E** — `E2ETests/ComplianceTests.cs`: la pagina Legal & Privacy renderizza e GDPR export
  restituisce i dati dell'utente in browser reale.
