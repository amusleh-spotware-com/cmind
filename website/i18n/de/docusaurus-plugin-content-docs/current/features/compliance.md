---
description: "Retail-FX/CFD/Krypto-Makler tragen legale + Aufbewahrungspflichten. Modul implementiert vier branchenstandardisierte Säulen: Risikobeschaffenheit-Zustimmung…"
---

# Legal & Compliance

Retail-FX/CFD/Krypto-Makler tragen legale + Aufbewahrungspflichten. Modul implementiert vier branchenstandardisierte Säulen: **Risikobeschaffenheit-Zustimmung**, **manipulationssicheres Audit-Trail**, **MiFID/ESMA-Stil-Aufbewahrung**, **GDPR-Datenrechte**. Alle gated durch Feature-Flag `Compliance`.

## 1. Versionierte Rechtsdokumente + Zustimmung

- `LegalDocument` (Aggregate) — versioniert Terms of Service, CFD **Risk Disclosure** oder Datenschutzrichtlinie. Version entwürfe, dann **veröffentlicht**; veröffentlichte Versionen **unveränderbar** (Bearbeitung wirft), daher exakter Text Benutzer stimmte zu immer wiederherstellbar. Aktives Dokument für einen Typ = seine höchste veröffentlichte Version.
- `ConsentRecord` (Aggregate) — unveränderbar Datensatz, dass Benutzer bestimmte Dokumentversion zu einem Zeitpunkt mit herrschender IP akzeptiert hat.
- **Erzwingung:** `RouteGroupBuilder/RouteHandlerBuilder.RequireConsent(type)` blockiert Aktion mit `403` wenn veröffentlichtes Dokument dieses Typs vorhanden und Benutzer nicht zu seiner aktiven Version zugestimmt. Angewendet auf **Copy-Profil-Erstellung** (`RiskDisclosure`). Nichts veröffentlicht → Aktionen erlaubt — nichts zum Zustimmen noch — daher das Aktivieren des Moduls blockiert nichts rückwirkend bis die Disclosure tatsächlich veröffentlicht ist.

## 2. Manipulationssicheres Audit-Trail

`AuditLog`-Einträge Hash-verkettet: jede Reihe speichert `PrevHash` und `Hash = SHA-256(prev | canonical fields)`. `AuditChainInterceptor` wendet Kette transparent bei `SaveChanges` an, daher sind vorhandene Audit-Call-Seiten unverändert. `IAuditTrailVerifier.VerifyAsync` re-geht Kette, berichtet erste Reihe deren gespeicherter Hash oder Rück-Link nicht mehr übereinstimmt — erkennt jede Bearbeitung oder Löschung von Vergangenheitsdatensatz. Owner-Endpoint: `GET /api/compliance/audit/verify`.

## 3. Aufbewahrung (MiFID II / ESMA RTS)

Aufbewahrung erfüllt durch **unveränderbar, Hash-verkettet Audit-Log** plus **beibehaltene Zustimmungsdatensätze** und soft-gelöscht (niemals hart-gelöscht) Domain-Datensätze. UTC-Zeitstempel von injiziertem `TimeProvider`. Zustimmungsdatensätze behalten Dokumentversion + IP; veröffentlichte Rechtsdokumente niemals mutiert. Aufbewahrung = diese Tabellen nicht löschen (nur-anhängen / soft-löschen).

## 4. GDPR-Datenrechte

- `GET /api/compliance/export` — maschinenlesbarer Export der Anrufer-Daten (Profil, Zustimmungen, Copy-Profile, Prop-Firm-Herausforderungen).
- `POST /api/compliance/erase` — Recht auf Vergessenwerden: `AppUser.Anonymize()` entfernt PII (Email, MFA) und Reihe soft-gelöscht, hält Referenz/Audit-Verlauf kohärent.

## API-Zusammenfassung

| Methode | Route | Rolle | Zweck |
|--------|-------|------|---------|
| GET | `/api/compliance/documents/active` | User+ | aktive veröffentlichte Dokumente |
| GET | `/api/compliance/consent/status` | User+ | welche Zustimmungen ausstehend sind |
| POST | `/api/compliance/consent` | User+ | akzeptieren die aktive Version eines Dokuments |
| GET | `/api/compliance/export` | User+ | GDPR-Datenexport |
| POST | `/api/compliance/erase` | User+ | GDPR-Löschung des eigenen Kontos |
| POST | `/api/compliance/documents` | Owner | ein Dokument entwurf |
| POST | `/api/compliance/documents/{id}/publish` | Owner | veröffentlichen eine Version |
| GET | `/api/compliance/audit/verify` | Owner | überprüfen die Audit-Hash-Kette |

UI: `/settings/legal` (Nav *Einstellungen → Legal & Datenschutz*, gated durch `Compliance`) zeigt ausstehende Vereinbarungen mit Accept-Schaltflächen + GDPR Export/Löschen Aktionen.

## Tests

- **Unit** — `UnitTests/Compliance/LegalDocumentTests.cs` (Entwurf/Veröffentlichung/Unveränderbarkeit, Zustimmung erfassen), `AuditChainTests.cs` (Hash-Links, Manipulationserkennung, Inhaltsempfindlichkeit).
- **Integration** — `IntegrationTests/CompliancePersistenceTests.cs` (aktive-Version + Zustimmungs-Abfragen auf echtem Postgres), `AuditChainIntegrityTests.cs` (Kette überprüft intakt, dann erkennt SQL-Level Manipulation), `ComplianceFlowTests.cs` (WebApplicationFactory, isolierte DB: Zustimmungs-Gate blockiert Copy-Erstellung bis Risk Disclosure akzeptiert; GDPR Export; Audit überprüfen).
- **E2E** — `E2ETests/ComplianceTests.cs`: Legal & Datenschutz-Seite rendert und GDPR Export gibt des Benutzers Daten in echtem Browser zurück.
