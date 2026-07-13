---
description: "L'API cTrader Open permet un token d'accès valide par cTrader ID (cID) à la fois. Le moment où un nouveau token est émis — un rafraîchissement planifié, ou une…"
---

# Cycle de vie du token Open API

L'API cTrader Open permet un **token d'accès valide par cTrader ID (cID) à la fois**. Le moment où un
nouveau token est émis — un rafraîchissement planifié, ou une ré-autorisation quand l'utilisateur lie un autre
compte sur le même cID — le token d'accès précédent est invalidé. Un moteur de copie s'exécutant sur un
nœud distant tient ce token maintenant mort, donc le nouveau token doit l'atteindre sans lâcher la
connexion en direct.

## Modèle

- **`OpenApiAuthorization`** est l'agrégat qui tient l'accès chiffré cID + les tokens de
  rafraîchissement. Un index unique sur `(UserId, CtidUserId)` applique **exactement une autorisation par cID
  par utilisateur**.
- **`TokenVersion`** — un compteur monotone qui s'incrémente chaque fois que le token tourne (`Refresh()`,
  qui couvre aussi le chemin de ré-auth quand un autre compte est lié sur le même cID). C'est le
  marqueur de version pour la règle du token unique valide et c'est ce qu'un hôte en cours utilise pour détecter un
  changement même si deux chaînes de token arrivent à se heurter.
- Les tokens sont chiffrés au repos via `ISecretProtector` (`EncryptionPurposes.OpenApiAccessToken` /
  `OpenApiRefreshToken`). Ils ne sont jamais enregistrés ou stockés en texte brut.

## Propagation (échange gracieux en place)

1. Un token tourne → le nouveau token + `TokenVersion` incrementé sont persistés.
2. Le `CopyEngineSupervisor` sur le nœud d'hébergement relit le plan à chaque cycle de réconciliation et
   calcule une **signature de token** (tokens d'accès + versions). Un changement signifie une rotation.
3. Au lieu de déchirer l'hôte et redémarrer (ce qui lâcherait le flux d'exécution du maître), le superviseur
   **pousse le nouveau token à l'hôte en cours**.
4. L'hôte ré-authentifie le compte affecté **sur le socket existant**
   (`ProtoOAAccountAuthReq` à nouveau) via `SwapAccessTokenAsync`, puis fait une légère réconciliation. L'
   ancien token meurt ; le flux de copie n'arrête jamais.

C'est ce qui rend le cas cross-cID sûr : un utilisateur ajoutant un deuxième compte du même cID
mid-run invalide l'ancien token, et le profil de copie en cours continue sur le nouveau.

## Rafraîchir

`OpenApiTokenRefreshService` (arrière-plan) rafraîchit de manière proactive les autorisations avant l'expiration ;
`OpenApiAuthorization.IsExpiring(threshold, now)` le contrôle. cTrader tourne le **token de rafraîchissement**
sur chaque rafraîchissement, donc le nouveau token de rafraîchissement est persisté immédiatement ; un cache
en lecture seule qui ne peut pas persister s'auto-invaliderait (relevant pour l'Job de test en cluster, qui
monte une copie inscriptible du secret).

### Escalade d'échec

Un rafraîchissement échoué n'est pas silencieux. `OpenApiAuthorization.MarkRefreshFailed(reason, now, criticalWindow)`
enregistre `RefreshFailedAt`, incrémente `ConsecutiveRefreshFailures`, et lève toujours
`AccessTokenRefreshFailed` (avertissement). Quand le token est maintenant dans `App:OpenApi:TokenRefreshCriticalWindow`
(défaut 6h) de l'expiration et le rafraîchissement échoue toujours, il escalade **une fois** avec un
événement de domaine `AccessTokenRefreshCritical` + log `Critical` de sorte que le propriétaire peut ré-autoriser avant
les opérations de copie/prop-firm perdent le token. Le compteur d'échec et la latch d'escalade se réinitialisent sur le
`Refresh` réussi suivant. Le service continue à réessayer chaque `TokenRefreshInterval`, donc une panne du fournisseur/maintenance
s'auto-guérit quand l'endpoint de rafraîchissement revient.

## Alerte d'invalidation & auto-recovery (M1)

Une ré-autorisation partielle/nouveau-compte sur un cID invalide le token qu'un hôte de copie en cours tient toujours. Quand un
appel de trading rejette avec `OpenApiErrorKind.TokenInvalid`, l'hôte lève une **alerte `CopyTokenInvalidated`** distincte (log 1078) — pas un échec générique — donc le canal de notification sait qu'un
token a besoin d'attention. La recovery est automatique : le superviseur relit l'autorisation à chaque cycle et,
quand le token rafraîchi change la signature de token, le pousse dans l'hôte en cours pour un **échange
en place** — la copie reprend sans ajout manuel. Un profil `NotLinkable` (token/auth temporairement
non résolvable) est de même réévalué chaque cycle du superviseur et hébergé au moment où son plan se reconstruit.

## Chien de garde du liveness de l'hôte (M2)

Le superviseur regarde la tâche d'exécution de chaque profil hébergé. Si un hôte s'échappe ou échoue tandis que son profil
est toujours assigné à ce nœud, le chien de garde annule et **redémarre** la prochaine fois (log
`CopyHostRestarted`), donc un hôte coincé s'auto-guérit au lieu de nécessiter un redémarrage manuel — et la
défaillance d'un profil n'immobilise jamais les autres (isolation par profil).

## Tests

- **Unit** — `TokenVersion` s'incrémente sur `Refresh` ; l'hôte effectue un échange en place sans redémarrage ;
  l'invalidation cross-cID échange les tokens source et destination ; **un token de destination invalidé lève
  `CopyTokenInvalidated` et auto-récupère au prochain push de token** (M1) ; la décision du chien de garde `IsHostDead`
  redémarre un hôte achevé/échoué et laisse un profil réassigné seul (M2).
- **Integration** — `TokenVersion` persiste + s'incrémente via EF sur Postgres réel ; la signature de token
  change au changement de version même si la chaîne est inchangée.
