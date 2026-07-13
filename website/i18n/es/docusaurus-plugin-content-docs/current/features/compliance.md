---
description: "Los corredores minoristas de FX/CFD/cripto cuentan con deberes legales + de mantenimiento de registros. El módulo implementa cuatro pilares estándar de la industria: consentimiento de divulgación de riesgo…"
---

# Legal y cumplimiento

Los corredores minoristas de FX/CFD/cripto cuentan con deberes legales + de mantenimiento de registros. El módulo implementa cuatro pilares estándar de la industria: **consentimiento de divulgación de riesgo**, **pista de auditoría a prueba de manipulación**, **mantenimiento de registros de estilo MiFID/ESMA**, **derechos de datos GDPR**. Todo gated por bandera de característica `Compliance`.

## 1. Documentos legales versionados + consentimiento

- `LegalDocument` (agregado) — Términos de Servicio versionados, **Divulgación de Riesgo** de CFD, o Política de Privacidad.
  Versión redactada, luego **publicada**; versiones publicadas **inmutables** (editar lanza excepción), por lo que el texto exacto con el que el usuario estuvo de acuerdo siempre es recuperable. Documento activo para un tipo = su versión publicada más alta.
- `ConsentRecord` (agregado) — registro inmutable de que el usuario aceptó una versión específica de documento en un momento, con IP de origen.
- **Cumplimiento:** `RouteGroupBuilder/RouteHandlerBuilder.RequireConsent(type)` bloquea la acción con `403` cuando un documento publicado de ese tipo existe y el usuario no ha consentido a su versión activa. Aplicado a **creación de perfil de copia** (`RiskDisclosure`). Nada publicado → acciones permitidas — nada a consentir aún — por lo que habilitar el módulo no bloquea nada retroactivamente hasta que la divulgación se publique realmente.

## 2. Pista de auditoría a prueba de manipulación

Entradas de `AuditLog` encadenadas por hash: cada fila almacena `PrevHash` e `Hash = SHA-256(prev | campos canónicos)`. `AuditChainInterceptor` aplica la cadena transparentemente en `SaveChanges`, por lo que los sitios de llamadas de auditoría existentes no cambian. `IAuditTrailVerifier.VerifyAsync` re-camina la cadena, reporta la primera fila cuyo hash almacenado o enlace hacia atrás ya no coinciden — detecta cualquier edición o eliminación de registro pasado. Endpoint del propietario: `GET /api/compliance/audit/verify`.

## 3. Mantenimiento de registros (MiFID II / ESMA RTS)

Mantenimiento de registros satisfecho por **registro de auditoría inmutable encadenado por hash** más **registros de consentimiento retenidos** y registros de dominio eliminados suavemente (nunca eliminados definitivamente). Marcas de tiempo UTC de `TimeProvider` inyectado. Los registros de consentimiento mantienen versión del documento + IP; documentos legales publicados nunca mutados. Retención = no purgar estas tablas (append-only / soft-delete).

## 4. Derechos de datos GDPR

- `GET /api/compliance/export` — exportación legible por máquina de datos del llamador (perfil, consentimientos, perfiles de copia, desafíos de propietario de fondo).
- `POST /api/compliance/erase` — derecho a eliminación: `AppUser.Anonymize()` limpia PII (correo electrónico, MFA) y fila eliminada suavemente, manteniendo coherencia de historial referencial/auditoría.

## Resumen de API

| Método | Ruta | Rol | Propósito |
|--------|------|-----|----------|
| GET | `/api/compliance/documents/active` | User+ | documentos publicados activos |
| GET | `/api/compliance/consent/status` | User+ | qué consentimientos están pendientes |
| POST | `/api/compliance/consent` | User+ | aceptar la versión activa de un documento |
| GET | `/api/compliance/export` | User+ | exportación de datos GDPR |
| POST | `/api/compliance/erase` | User+ | eliminación GDPR de cuenta propia |
| POST | `/api/compliance/documents` | Owner | redactar un documento |
| POST | `/api/compliance/documents/{id}/publish` | Owner | publicar una versión |
| GET | `/api/compliance/audit/verify` | Owner | verificar la cadena de hash de auditoría |

UI: `/settings/legal` (nav *Configuración → Legal y Privacidad*, gated por `Compliance`) muestra acuerdos pendientes con botones de aceptación + acciones de exportación/eliminación GDPR.

## Pruebas

- **Unit** — `UnitTests/Compliance/LegalDocumentTests.cs` (redacción/publicación/inmutabilidad, captura de consentimiento), `AuditChainTests.cs` (enlaces de hash, detección de manipulación, sensibilidad de contenido).
- **Integration** — `IntegrationTests/CompliancePersistenceTests.cs` (consultas de versión activa + consentimiento en Postgres real), `AuditChainIntegrityTests.cs` (cadena verifica intacta, luego detecta manipulación de nivel SQL), `ComplianceFlowTests.cs` (WebApplicationFactory, DB aislada: puerta de consentimiento bloquea creación de copia hasta que se acepte divulgación de riesgo; exportación GDPR; verificación de auditoría).
- **E2E** — `E2ETests/ComplianceTests.cs`: página Legal y Privacidad se procesa y la exportación GDPR devuelve datos del usuario en navegador real.
