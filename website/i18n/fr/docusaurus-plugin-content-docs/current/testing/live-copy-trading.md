---
description: "Suite de test de copie commerciale reproduisible complète. Deux couches :"
---

# Suite de test de copie commerciale (déterministe + en direct)

Suite de test de copie commerciale reproduisible complète. Deux couches :

1. **Tests déterministes** (xUnit, pas de réseau) — maths de copie + logique du moteur. Rapide, CI, pas de secrets. Couvrez chaque mode de gestion de l'argent, chaque filtre/option, résilience du moteur.
2. **Tests E2E en direct** (vrais comptes démo cTrader) — production `CopyEngineHost` plaçant + copiant de vrais ordres entre de vrais comptes. Entièrement automatisé, réexécutable comme test unitaire : lisez les identifiants mis en cache à partir de fichiers gitignorés locaux, auto-rafraîchissez le token d'accès, sautez proprement quand les secrets sont absent (CI reste vert).

Ne fonctionne jamais contre un compte financé en direct — chaque compte est **démo**, chaque test en direct ferme les positions qu'il ouvre.

## Disposition

```
tests/UnitTests/CopyTrading/
  CopySizingCalculatorTests.cs   — chaque mode de dimensionnement + arrondi + lot min/max
  CopyDecisionEngineTests.cs     — filtre de direction/inverse/glissement/délai/symbole/size-zéro
  CopyEngineHostTests.cs         — logique de copie de l'hôte contre une session fausse en mémoire
  FakeTradingSession.cs          — IOpenApiTradingSession déterministe (enregistrements d'ordres/fermetures/amends)
  OpenApiConnectionTests.cs      — connexion / reconnexion / backoff / faute fatale (résilience)

tests/IntegrationTests/CopyLive/
  LiveCopySecrets.cs             — charge les secrets gitignorés, enregistre les tokens rafraîchis
  LiveTokenBootstrapTests.cs     — une seule tentative : décrypter les tokens de la BD de l'app dans le cache de token
  LiveCopyFixture.cs             — tourne le token d'accès, expose la liste des comptes démo
  LiveCopyScenario.cs            — exécute un vrai scénario de copie de bout en bout (ouverture → copie → vérification → nettoyage)
  CopyTradingLiveTests.cs        — les scénarios en direct (1:1, 1:many, inverse, …)
```

## Secrets (locaux, gitignorés — jamais commis)

Tous les identifiants sous `<repo>/secrets/` (déjà dans `.gitignore`). Dev écrit **les deux premiers fichiers uniquement** ; le troisième (tokens) auto-produit par l'onboarding.

`secrets/openapi-test-app.local.json` — app Open API :

```json
{ "ClientId": "2175_…", "ClientSecret": "…" }
```

`secrets/openapi-cids.local.json` — identifiants de connexion cID à autoriser (un ou plusieurs) :

```json
{ "Cids": [
  { "Cid": "amusleh",  "Username": "amusleh",  "Password": "…" },
  { "Cid": "afhacker", "Username": "afhacker", "Password": "…" }
] }
```

`secrets/openapi-tokens.local.json` — **écrit par onboarding**, multi-cID, rafraîchi à chaque exécution :

```json
{ "Cids": [
  { "Cid": "amusleh", "RefreshToken": "…", "AccessToken": "…", "IsLive": false,
    "Accounts": [ { "CtidTraderAccountId": 25172589, "TraderLogin": 3635817, "IsLive": false }, … ] }
] }
```

Le token de rafraîchissement **n'expire jamais**, donc après le onboarding unique, les tests en direct fonctionnent indéfiniment : chaque exécution échange le token de rafraîchissement de chaque cID pour un token d'accès frais (rotation) — pas de navigateur, pas d'invites.

## Onboarding unique (entièrement automatisé — pas d'interaction dev au-delà de la sauvegarde d'identifiants)

L'onboarding conduit la vraie connexion cTrader ID en navigateur headless à partir des identifiants cID enregistrés, capture le rappel OAuth sur l'écouteur HTTPS local à la redirection enregistrée de l'app (`https://localhost:7080/openapi/callback`), échange le code pour les tokens, charge la liste des comptes, écrit le cache de token multi-cID. Exécutez une seule fois par machine (ou lors de l'ajout de cID) :

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

Autorise chaque cID en `openapi-cids.local.json`, écrit `openapi-tokens.local.json`. Après cela, les tests en direct de copie n'ont besoin de rien d'autre. (Le compte cTrader ID du cID ne doit pas avoir 2FA/captcha à la connexion pour que l'automation complète.)

**Bootstrap alternatif** (si les comptes sont déjà autorisés dans l'app en cours) : décryptez les tokens stockés directement à partir du volume Postgres de l'app au lieu de ré-autoriser :

```bash
docker run -d --name cmind-pg-extract -e POSTGRES_PASSWORD=appdev \
  -v app-pg-data:/var/lib/postgresql/data -p 5544:5432 postgres:17-alpine
CMIND_VOLUME_CONN="Host=127.0.0.1;Port=5544;Database=appdb;Username=postgres;Password=appdev" \
  dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveTokenBootstrapTests
docker rm -f cmind-pg-extract
```

## Sécurité — démo uniquement

Les tests en direct échangent **uniquement des comptes démo** : le fixture filtre le cache de token vers les comptes avec `IsLive == false` et se connecte à la gateway démo, donc l'ordre ne peut jamais atterrir sur un compte en direct/financé même si le compte en direct est autorisé. Chaque position qu'un test ouvre est fermée au nettoyage.

## Exécution

```bash
# Tests de copie déterministes uniquement (rapide, pas de secrets, CI-sûr)
dotnet test tests/UnitTests --filter FullyQualifiedName~CopyTrading

# Tests de copie en direct contre les vrais comptes démo (nécessite les deux fichiers d'identifiants)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests

# Tout
dotnet test
```

Sans fichiers d'identifiants, les tests en direct impriment raison de saut + pass comme no-ops, donc suite sûre à exécuter n'importe où.

## Couverture

### Gestion de l'argent / dimensionnement (déterministe — `CopySizingCalculatorTests`)
FixedLot · LotMultiplier · NotionalMultiplier (taille de contrat / devise) · ProportionalBalance ·
ProportionalEquity · ProportionalFreeMargin · AutoProportional · FixedRiskPercent · FixedLeverage ·
mettre à l'échelle **vers le haut** et **vers le bas** pour asymétrie solde/effet de levier/capacité (la « règle d'or ») · arrondi step lot · saut lot min vs force-to-min · plafond max-lot · plus serré-des bornes-vs-spec min & max · saut zéro balance maître.

### Filtres de décision (déterministe — `CopyDecisionEngineTests`)
Whitelist / blacklist symbole / permettre · LongOnly / ShortOnly · l'inverse retourne le côté effectif ·
glissement sur limite saut + exactement-à-limite permis · signal obsolète (délai max) saut · size-zéro saut ·
réconciliation reconnexion (dedup ouverture manquante, fermeture orpheline).

### Hôte du moteur de copie (déterministe — `CopyEngineHostTests`, session en mémoire)
Le reflet ouvrir reflète un ordre de marché (côté / volume / étiquette) · **inverse** retourne le côté et **échange SL/TP** ·
**mappeur de symbole** résout le symbole de destination · **échec d'ordre sur un esclave continue toujours à copier sur les
autres** · fermeture source ferme la copie reflétée · reconnexion resync ferme les copies orphelines.

### Résilience de connexion (déterministe — `OpenApiConnectionTests`)
Atteint Connected après auth d'app · connexion largué reconnecte et re-auth · erreur auth fatale faut ·
backoff exponentiel.

### En direct, vrais comptes démo cTrader (`CopyTradingLiveTests`)
Rafraîchissement de token + listage de compte · la copie **1:1** exécute · la copie **1:many** reflète sur chaque esclave ·
**inverse** tourne l'achat maître en vente esclave · la copie **cross-cID** (maître sous un cID reflète sur esclave sous un autre, chacun s'authentifiant avec son propre token). Chaque ouvre une vraie position lot min sur maître, attend le moteur la refléter (assorti par étiquette id-position-source sur esclave), affirme, ferme tout. Marché fermé rapporté **Inconclusive**, pas échouant.

## Logging & auditabilité

Chaque opération de copie commerciale enregistrée via événements structurés générés à la source (`Core/Logging/LogMessages.cs`, ids d'événement 1043–1055), piste complète auditable :

| Événement | Id | Signification |
|-------|----|---------|
| CopyHostStarted | 1046 | le moteur d'un profil est venu (compte source + destinatio) |
| CopySourceOpen | 1047 | maître a ouvert une position (symbole / côté / lots) |
| CopyOrderPlaced | 1048 | ordre de copie envoyé à un esclave (symbole / côté / volume / id source) |
| CopySkipped | 1049 | une copie a été sautée et pourquoi (glissement / direction / filtre_symbole / size_zéro / …) |
| CopyProtectionApplied | 1050 | SL/TP appliqué à une copie esclave |
| CopyOpenFailed | 1051 | copie-ouverture esclave échoué (isolée — autres esclaves continuent) |
| CopySourceClose / CopyPositionClosed | 1052 / 1053 | maître fermé → copie esclave fermée |
| CopyCloseFailed | 1054 | copie-fermeture esclave échoué |
| CopyResync | 1055 | réconciliation reconnexion (compte ouverture source, orphelins fermés) |
| CopyPartialClose | 1056 | maître fermeture partielle reflétée — tranche proportionnelle fermée sur un esclave |
| CopyScaleIn | 1057 | maître scale-in reflété (opt-in) — volume ajouté copié à un esclave |
| CopyPendingOrderPlaced | 1058 | limite/arrêt en attente reflété à un esclave (opt-in) |
| CopyPendingOrderCancelled | 1059 | source en attente annulée → esclave en attente annulée |
| CopyTrailingApplied | 1060 | trailing stop appliqué à une copie esclave (opt-in) |
| CopyStopLossAmended | 1061 | move SL source ré-amendé la copie esclave |
| CopyHostTokenRotated | 1062 | superviseur redémarré un hôte en cours après rotation de son token d'accès |

Les logs émis au format Serilog JSON compact (props structurées : `ProfileId`, `DestinationCtid`, `SourcePositionId`, `Symbol`, `Side`, `Volume`, …), expédiés vers OTLP quand `OTEL_EXPORTER_OTLP_ENDPOINT` défini. **Entièrement configurable** par catégorie via config standard — par ex. hausse/baisse verbiage du moteur de copie sans toucher le code :

```jsonc
// appsettings.json — remplacements de niveau Serilog
"Serilog": { "MinimumLevel": { "Override": {
  "CopyEngine": "Information",              // piste d'audit CopyEngineHost
  "Nodes.CopyTrading": "Information"        // superviseur / rafraîchissement de token
} } }
```

Le test d'hôte `Audit_log_records_every_trading_operation` affirme la piste se déclenche pour ouverture, ordre, protection, fermeture.

## Cas limites (validés contre comment les vraies plateforme de copie/MAM échouent)

Glissement & latence, suffixe symbole/asymétrie, trades dupliqués sur reconnexion, asymétrie effect de levier & dimensionnement marge-sûr, dépôt-devise/différences taille de contrat, lot min/max & arrondi, ordres rejetés, filtres direction, nettoyage orphelin après déconnexion — tous couverts ci-dessus. Sources :
[asymétrie effect de levier](https://copygram.app/blog/education/the-truth-about-leverage-mismatches-copying-high-leverage-low-leverage-accounts) ·
[copie cross-broker](https://www.mt4copier.com/cross-broker-trade-copying-efficient-forex-replication/) ·
[pièges copier](https://www.mt4copier.com/copy-trading-pitfalls-every-account-manager-must-avoid/) ·
[glissement & latence](https://copygram.app/blog/education/understanding-slippage-latency-copy-trading) ·
[pourquoi la copie échoue](https://xtsupport.zendesk.com/hc/en-us/articles/51566808595993-Why-Copy-Trading-Fails-Causes-Prevention-Guide) ·
[paramètres de risque](https://www.mt4copier.com/risk-parameters/).

## Couverture du mirroring avancé (fermeture partielle · ordres en attente · SL-trailing)

L'hôte reflet plus que marché ouverture/fermeture. Chaque comportement = drapeau opt-in par-destination sur `CopyDestination` (`MirrorPartialClose` par défaut on, `MirrorScaleIn`/`CopyPendingOrders`/`CopyTrailingStop` par défaut off), gardé par les méthodes d'intention, persisté jsonb (migration `CopyAdvancedMirroringAndNodeAffinity`).

| Comportement | Test déterministe (`CopyEngineHostTests`) | Test en direct |
|-----------|--------------------------------------------|-----------|
| Fermeture partielle → tranche proportionnelle | `Partial_close_mirrors_a_proportional_slice_on_the_slave` (1.0→0.4 ferme 60%) + chemin désactivé | `Partial_close_shrinks_the_slave_copy_proportionally` ✅ |
| Scale-in | `Scale_in_is_ignored_by_default_and_mirrored_when_enabled` | — |
| Limite/arrêt en attente placé | `Pending_order_is_placed_on_the_slave_when_enabled` (Theory : Limit+Stop) + chemin désactivé | `Pending_limit_order_is_mirrored_and_cancel_propagates` ✅ |
| Annulation en attente | `Source_pending_cancel_cancels_the_slave_pending` | (même test en direct — annule sur maître, affirme l'esclave annule) ✅ |
| En attente rempli pas double-ouverture | `Filled_pending_does_not_double_open` (dedupe ordre-id → position-id) | — |
| Trailing stop | `Trailing_stop_is_applied_to_the_copy_when_enabled` | `Trailing_stop_is_mirrored_onto_the_slave_copy` ✅ |
| Move SL source ré-amend | `Source_stop_loss_move_re_amends_the_copy` | — |
| Événements d'audit se déclenchent | `Advanced_mirroring_audit_events_fire` (1056/1058/1059) | — |

Tous les tests en direct ci-dessus **vérifiés vert contre vrais comptes démo cTrader** (1:1, 1:many, inverse, cross-cID, fermeture partielle, en attente+annulation, trailing).

Ajouts de câble en `OpenApiTradingSession` : `SendPendingOrderAsync`, `CancelOrderAsync`, `ReconcilePendingOrdersAsync`, drapeau trailing sur `AmendPositionSltpAsync`, champs ordre/en attente sur `ExecutionEvent`, `LoadSpotPriceAsync` (abonnement spot → offre/demande, utilisé par les tests en attente/trailing en direct pour placer les ordres en attente loin du marché), `StopLoss`/`TrailingStopLoss` sur `OpenPositionSnapshot` (l'état traîné de la copie observable via réconciliation). Les copies de destination restent étiquetées par **id de position source** (copies en attente par id d'ordre **source**) donc le resync de reconnexion reste basé-id, jamais duplique le trade.

**Gotcha d'événement cTrader (vérifiée en direct) :** l'événement d'exécution `ORDER_ACCEPTED`/`ORDER_CANCELLED` de la commande en attente au repos porte un **placeholder de Position non-ouvert** plus l'`Order`. Le flux doit le classer comme événement d'ordre **avant** la branche de position (gatée sur position pas `OPEN`), sinon placement en attente mal-lu comme fermeture de position. `SourceExecutionsAsync` fait cela ; le manquer silencieusement abandonne tous les mirroring en attente.

## Rotation de token + affinité de nœud

- **Rotation dans les hôtes en cours.** `CopyEngineSupervisor` enregistre la signature du token sur chaque hôte en cours et, chaque réconciliation, reconstruit le plan de BD (récemment tourne par `OpenApiTokenRefreshService`). Changement de signature redémarre l'hôte (`CopyHostTokenRotated`, 1062) ; le `ResyncAsync` du nouvel hôte reconstruit l'état sans dupliquer les trades. Force la rotation mid-run via `IOpenApiTokenClient.RefreshAsync` pour vérifier l'hôte en direct continue la copie.
- **Affinité de nœud (pas double-copie).** Le nœud local Web et le worker `CopyAgent` exécutent un superviseur. Chaque profil en cours demandé par exactement un nœud (`CopyProfile.AssignedNode`, revendication `ExecuteUpdate` atomique clée hors `CopyOptions.NodeName`, défaut nom de machine). Le superviseur hôte uniquement les profils qu'il possède ; arrêt/pause libère la revendication. Couverture :
  - Domaine (unit) : `AssignToNode_makes_profile_hosted_by_only_that_node`,
    `Stopping_a_profile_releases_its_node_assignment`, `NodeIdentity_rejects_blank`.
  - **Integration (Postgres réel, Testcontainers)** : `CopyNodeAffinityTests` conduit le `ClaimUnassignedProfilesAsync` réel du superviseur — affirme le premier nœud réclame tous les 3 profils en cours, le second réclame **0** (pas double-hôte), pause→redémarrage libère la revendication pour un autre nœud.
  - Détection de rotation (`TokenRotationSignatureTests`) : la `TokenSignature` du superviseur change quand le token source ou destination tourne, stable sinon (l'hôte en cours redémarre uniquement sur vraie rotation).

### Tokens de rafraîchissement à usage unique (important)

Les **tokens de rafraîchissement cTrader sont à usage unique** — chaque rafraîchissement retourne un *nouveau* token de rafraîchissement, invalide l'ancien. Le fixture en direct rafraîchit au démarrage, persiste le token tourne vers `secrets/openapi-tokens.local.json`. Conséquences :
- Si run rafraîchit mais **ne peut pas persister** le nouveau token (par ex. mount en lecture seule), token mis en cache mort, le prochain run échoue `ACCESS_DENIED`. Régénérez avec onboarding headless :
  `CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`.
- `LiveCopySecrets.SaveTokens` avale les échecs d'écriture de sorte que le cache en lecture seule n'écrase pas le run, mais la suite en direct **en-cluster** a toujours besoin du cache **inscriptible** (K8s Job copie Secret dans emptyDir — voir doc déploiement).

## Exécution de la suite dans un cluster Kubernetes

Toute la suite s'exécute in-cluster contre l'app déployée Helm, donc la régression est détectée in-cluster aussi localement. Voir [`docs/deployment/kubernetes.md`](../deployment/kubernetes.md#in-cluster-test-suite).

```bash
scripts/k8s-e2e.sh                                   # cluster kind, suite déterministe (pas de secrets)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # en direct
```

`Dockerfile.tests` construire image runner ; Helm `tests-job.yaml` (gatée `tests.enabled=false`) l'exécute contre Postgres + Web in-cluster. **Par défaut = suite de copie déterministe** (pas de secrets, pas de tokens qui tournent). Pour la suite en direct, définissez `tests.copySecret` vers Secret tenant gitignored `openapi-*.local.json` ; init-container le copie dans un **emptyDir inscriptible** à `/app/secrets` (requis — les tokens de rafraîchissement à usage unique doivent être persistables). Les tests en direct nécessitent uniquement Web + Postgres + cache de token — pas d'agents de nœud privilégiés. Le script affirme Job sort 0 et les logs contiennent `Passed!`.

**Vérifié ici (Docker, pas de cluster) :** l'image de test exécute la suite déterministe (`101 passé`) et, avec le mount `secrets/` inscriptible, la suite en direct **complète** (`8 passé`) — chemin exacte Job moins Kubernetes. `kind`/`kubectl`/`helm` indisponible en env d'authoring, donc le run de cluster `k8s-e2e.sh` complet est l'étape unique non exécutée ici.

## Matrice d'option en direct + chaos (LiveCopyMatrix / LiveCopyChaos)

Deux suites en direct conduites par les données construites sur `LiveCopyScenario` / `LiveCopyFixture`, homologue en direct à suite de stress DST déterministe :

- **`LiveCopyMatrix`** — matrice d'option `[Theory]`/`[MemberData]` : une vraie ouverture maître par rangée contre les comptes démo, chacune avec destination configurée différemment, affirmant le résultat doré. Rangées : `one_to_one`, `half_multiplier`, `reverse` (côté opposé), `manage_only` (n'ouvre rien), `trading_hours_closed` (fenêtre exclut maintenant → pas copie), `source_label_block` (filtre étiquette → pas copie), `lot_sanity_block` (plafond → pas copie).
- **`LiveCopyChaos`** — moteur de copie contre start hostile : le maître tient déjà la position avant que l'hôte démarre, donc la convergence ne peut venir que du resync de démarrage (`Sync-Open-on-Start`), plus cas négatif avec bascule off. Le chaos de socket-flap / rotation de token / rejet déterministe couverts exhaustivement par suite DST ; ceci affirme le chemin resync en direct.

Les deux **sautent proprement** sans identifiants, rapportent **Inconclusive** sur le marché fermé, chaque rangée nettoie les positions qu'elle ouvre (comptes démo uniquement).

**Robustesse du fixture :** `LiveCopyFixture` dégrade maintenant vers saut propre quand les tokens mis en cache ne peuvent pas rafraîchir (les tokens de rafraîchissement cTrader sont à usage unique — cache obsolète ou run hors ligne ne faut plus la collection complète ; re-run onboarding OAuth pour rafraîchir le cache).

## Exécution vous-même des suites en direct + cluster (AUCUNE excuse — tout est auto-serviceable)

Tout ci-dessous disponible dans cet environnement. Ne **pas** réclamer « identifiants indisponibles », « token obsolète », ou « kind/helm pas installé » — configurez-les et exécutez-les.

### 1. Obtenir un token Open API frais (OAuth headless, pas d'interaction navigateur)

Les tokens de rafraîchissement cTrader sont à usage unique, donc le token mis en cache SERA obsolète. Ré-le-créer vous-même à partir du username/mot de passe cID enregistrés (`secrets/openapi-cids.local.json` + `secrets/openapi-test-app.local.json`, ou unifié `secrets/dev-credentials.local.json`). Le test d'onboarding conduit OAuth **headless Edge** via Playwright, écrit `secrets/openapi-tokens.local.json` :

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

~13s ; autorise chaque cID, met en cache les tokens frais. Re-exécutez chaque fois que la suite en direct rapporte fixture indisponible dû à échec de rafraîchissement.

### 2. Exécuter les suites de copie en direct (vrais comptes démo cTrader)

```bash
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests   # mirroring core (8)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyMatrix          # matrice d'option (7)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyChaos           # chaos resync (2)
```

Placez + nettoyez de vrais ordres DEMO (jamais comptes en direct), rapportez **Inconclusive** sur le marché fermé. Vérifiée vert de bout en bout.

### 3. Bootstrap tokens depuis un volume d'app en cours (alternatif)

Si app run + cID lié in-app, extrayez le token de rafraîchissement dernière de l'app directement du volume Postgres `app-pg-data` au lieu de ré-autoriser — voir `LiveTokenBootstrapTests`, définissez `CMIND_VOLUME_CONN`.

### 4. E2E cluster Kubernetes

`kind`, `helm`, Docker disponible (installer kind/helm via `go install`/release binaries ou `choco install kind kubernetes-helm` si pas en PATH). Script unique construire+charger les images, déployer le chart, exécuter le Job de test in-cluster, affirmer sortie 0 :

```bash
scripts/k8s-e2e.sh                                 # suite de copie déterministe (pas de secrets)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh   # en direct in-cluster
```

Voir [../deployment/kubernetes.md](../deployment/kubernetes.md).
