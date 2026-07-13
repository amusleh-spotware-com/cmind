---
description: "Les courtiers de forex/CFD/crypto au détail portent des responsabilités légales + de tenue de dossiers. Le module implémente quatre piliers standards de l'industrie : consentement de divulgation des risques…"
---

# Légal & conformité

Les courtiers de forex/CFD/crypto au détail portent des responsabilités légales + de tenue de dossiers. Le module implémente quatre piliers standards de l'industrie : **consentement de divulgation des risques**, **piste d'audit infalsifiable**, **tenue de dossiers style MiFID/ESMA**, **droits de données RGPD**. Tous contrôlés par le drapeau de feature `Compliance`.

## 1. Documents juridiques versionnés + consentement

- `LegalDocument` (agrégat) — Conditions de service versionnées, **Divulgation des risques** de CFD, ou Politique de confidentialité.
  Version rédigée, puis **publiée** ; les versions publiées sont **immuables** (l'édition lève), donc le texte exact auquel l'utilisateur
  a consenti est toujours récupérable. Le document actif pour un type = sa version publiée la plus élevée.
- `ConsentRecord` (agrégat) — Enregistrement immuable qu'un utilisateur a accepté une version de document spécifique à un moment, avec IP d'origine.
- **Application :** `RouteGroupBuilder/RouteHandlerBuilder.RequireConsent(type)` bloque l'action avec `403`
  quand un document publié de ce type existe et l'utilisateur n'a pas consenti à sa version active. Appliqué à
  la **création de profil de copie** (`RiskDisclosure`). Rien de publié → actions autorisées — rien à consentir
  pour l'instant — donc l'activation du module ne bloque rien rétroactivement jusqu'à ce que la divulgation soit réellement publiée.

## 2. Piste d'audit infalsifiable

Les entrées `AuditLog` sont chaînées par hachage : chaque ligne stocke `PrevHash` et `Hash = SHA-256(prev | champs canoniques)`.
`AuditChainInterceptor` applique la chaîne de façon transparente à `SaveChanges`, donc les sites d'appel d'audit existants ne changent pas.
`IAuditTrailVerifier.VerifyAsync` réexamine la chaîne, signale la première ligne dont le hachage stocké ou le rétro-lien ne correspond plus
— détecte toute édition ou suppression du record passé. Endpoint propriétaire : `GET /api/compliance/audit/verify`.

## 3. Tenue de dossiers (MiFID II / ESMA RTS)

La tenue de dossiers est satisfaite par le **journal d'audit immuable, chaîné par hachage** plus les **dossiers de consentement conservés** et
les records de domaine supprimés de façon logicielle (jamais supprimés physiquement). Les horodatages UTC viennent de l'injection `TimeProvider`. Les dossiers de consentement conservent la version du document + IP ; les documents juridiques publiés ne sont jamais mutés. La rétention = ne pas purger ces
tables (append-only / soft-delete).

## 4. Droits de données RGPD

- `GET /api/compliance/export` — export lisible par machine des données de l'appelant (profil, consentements, profils de copie, défis prop-firm).
- `POST /api/compliance/erase` — droit à l'oubli : `AppUser.Anonymize()` efface les PII (e-mail, 2FA) et la ligne est
  supprimée de façon logicielle, gardant l'historique référentiel/audit cohérent.

## Résumé API

| Méthode | Route | Rôle | Objectif |
|---------|-------|------|---------|
| GET | `/api/compliance/documents/active` | User+ | documents actifs publiés |
| GET | `/api/compliance/consent/status` | User+ | consentements en attente |
| POST | `/api/compliance/consent` | User+ | accepter la version active d'un document |
| GET | `/api/compliance/export` | User+ | export RGPD |
| POST | `/api/compliance/erase` | User+ | suppression RGPD du compte propre |
| POST | `/api/compliance/documents` | Owner | rédiger un document |
| POST | `/api/compliance/documents/{id}/publish` | Owner | publier une version |
| GET | `/api/compliance/audit/verify` | Owner | vérifier la chaîne de hachage d'audit |

UI : `/settings/legal` (nav *Paramètres → Legal & Privacy*, contrôlé par `Compliance`) affiche les accords en attente avec boutons d'acceptation + actions d'export/suppression RGPD.

## Tests

- **Unit** — `UnitTests/Compliance/LegalDocumentTests.cs` (brouillon/publication/immuabilité, capture de consentement),
  `AuditChainTests.cs` (liens de hachage, détection de falsification, sensibilité du contenu).
- **Integration** — `IntegrationTests/CompliancePersistenceTests.cs` (requêtes de version active + consentement sur
  Postgres réel), `AuditChainIntegrityTests.cs` (la chaîne vérifie intacte, puis détecte la falsification au niveau SQL),
  `ComplianceFlowTests.cs` (WebApplicationFactory, DB isolée : la porte de consentement bloque la création de copie jusqu'à ce que la
  divulgation des risques soit acceptée ; export RGPD ; vérification d'audit).
- **E2E** — `E2ETests/ComplianceTests.cs` : la page Legal & Privacy s'affiche et l'export RGPD retourne les données de l'utilisateur dans le navigateur réel.
