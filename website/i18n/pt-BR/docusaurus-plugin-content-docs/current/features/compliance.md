---
description: "A corretagem de varejo FX/CFD/crypto carrega deveres legais + manutenção de registros. Módulo implementa quatro pilares padrão da indústria: consentimento de divulgação de risco…"
---

# Legal e conformidade

A corretagem de varejo FX/CFD/crypto carrega deveres legais + manutenção de registros. Módulo implementa quatro pilares padrão da indústria: **consentimento de divulgação de risco**, **trilha de auditoria à prova de adulteração**, **manutenção de registros ao estilo MiFID/ESMA**, **direitos de dados GDPR**. Todos fechados pela sinalização de recurso `Compliance`.

## 1. Documentos legais versionados + consentimento

- `LegalDocument` (agregado) — Termos de Serviço versionados, **Divulgação de Risco** de CFD ou Política de Privacidade.
  Versão rascunhada, depois **publicada**; versões publicadas **imutáveis** (editar lança), então o texto exato que o usuário concordou é sempre recuperável. Documento ativo para um tipo = sua versão publicada mais alta.
- `ConsentRecord` (agregado) — registro imutável que o usuário aceitou versão de documento específica em um momento, com IP originário.
- **Aplicação:** `RouteGroupBuilder/RouteHandlerBuilder.RequireConsent(type)` bloqueia ação com `403` quando documento publicado desse tipo existe e usuário não consentiu com sua versão ativa. Aplicado à **criação de perfil de cópia** (`RiskDisclosure`). Nada publicado → ações permitidas — nada para consentir ainda — para que habilitar módulo não bloqueie nada retroativamente até divulgação realmente publicada.

## 2. Trilha de auditoria à prova de adulteração

Entradas `AuditLog` encadeadas por hash: cada linha armazena `PrevHash` e `Hash = SHA-256(prev | campos canônicos)`. `AuditChainInterceptor` aplica corrente transparentemente em `SaveChanges`, para que locais de chamada de auditoria existentes inalterados. `IAuditTrailVerifier.VerifyAsync` re-anda corrente, relata primeira linha cuja hash armazenada ou back-link não corresponde mais — detecta qualquer edição ou exclusão de registro passado. Ponto final do proprietário: `GET /api/compliance/audit/verify`.

## 3. Manutenção de registros (MiFID II / ESMA RTS)

A manutenção de registros é satisfeita por **registro de auditoria imutável, encadeado por hash** além **registros de consentimento retidos** e soft-deleted (nunca hard-deleted) registros de domínio. Carimbos de tempo UTC de `TimeProvider` injetado. Registros de consentimento mantêm versão de documento + IP; documentos legais publicados nunca mutados. Retenção = não limpando essas tabelas (apenas anexar / soft-delete).

## 4. Direitos de dados GDPR

- `GET /api/compliance/export` — exportação legível por máquina dos dados do chamador (perfil, consentimentos, perfis de cópia, desafios de prop-firm).
- `POST /api/compliance/erase` — direito ao esquecimento: `AppUser.Anonymize()` limpa PII (email, MFA) e linha soft-deleted, mantendo histórico referencial/auditoria coerente.

## Resumo da API

| Método | Rota | Papel | Propósito |
|--------|-------|------|---------|
| GET | `/api/compliance/documents/active` | Usuário+ | documentos publicados ativos |
| GET | `/api/compliance/consent/status` | Usuário+ | quais consentimentos estão pendentes |
| POST | `/api/compliance/consent` | Usuário+ | aceitar a versão ativa de um documento |
| GET | `/api/compliance/export` | Usuário+ | exportação de dados GDPR |
| POST | `/api/compliance/erase` | Usuário+ | apagamento GDPR da própria conta |
| POST | `/api/compliance/documents` | Proprietário | rascunhar um documento |
| POST | `/api/compliance/documents/{id}/publish` | Proprietário | publicar uma versão |
| GET | `/api/compliance/audit/verify` | Proprietário | verificar a corrente de hash de auditoria |

Interface: `/settings/legal` (nav *Settings → Legal & Privacy*, fechado por `Compliance`) mostra acordos pendentes com botões de aceitação + ações de exportação/apagamento GDPR.

## Testes

- **Unidade** — `UnitTests/Compliance/LegalDocumentTests.cs` (rascunho/publicação/imutabilidade, captura de consentimento), `AuditChainTests.cs` (links de hash, detecção de adulteração, sensibilidade de conteúdo).
- **Integração** — `IntegrationTests/CompliancePersistenceTests.cs` (consultas de versão ativa + consentimento no Postgres real), `AuditChainIntegrityTests.cs` (corrente verifica intacta, depois detecta adulteração em nível SQL), `ComplianceFlowTests.cs` (WebApplicationFactory, DB isolado: portão de consentimento bloqueia criação de cópia até divulgação de risco aceita; exportação GDPR; auditoria verifica).
- **E2E** — `E2ETests/ComplianceTests.cs`: página Legal & Privacy renderiza e exportação GDPR retorna dados do usuário em navegador real.
