---
description: "Vérification complète du travail de copie commerciale restant — tout ci-dessous **réellement exécuté**, pas seulement rédigé."
---

# Exécution de vérification de copie commerciale (2026-07-10)

Vérification complète du travail de copie commerciale restant — tout ci-dessous **réellement exécuté**, pas seulement rédigé.

## Live (comptes démo cTrader réels) — 8/8 pass
1:1 · 1:many · inverse · cross-cID · fermeture partielle · **limite en attente + annulation** · **trailing stop** · rafraîchissement de token.
Ajout de scénarios en direct `RunPendingAsync` / `RunTrailingAsync` (+ `LoadSpotPriceAsync`, `OpenPositionSnapshot.StopLoss/TrailingStopLoss`).

## Integration (Postgres réel, Testcontainers) — pass
- `CopyNodeAffinityTests` — revendication réelle atomique du superviseur : le premier nœud revendique tous les profils en cours, le second revendique **0** (pas double-copie) ; pause libère + reclamation.
- `TokenRotationSignatureTests` — la signature change uniquement lors d'une vraie rotation de token.

## In-cluster (kind + Helm) — pass
Installé `kind`/`kubectl`/`helm`, a exécuté `scripts/k8s-e2e.sh` contre un vrai cluster kind :
- **Job déterministe : 101 passé** in-cluster.
- **Job en direct : 8 passé** in-cluster (init-container `seed-secrets` copie Secret → emptyDir inscriptible, comptes démo réels).
- Job `Complete 1/1`, script exit 0.

## Bugs trouvés lors de la vérification (corrigés + re-vérifiés)
- **Événements en attente** : cTrader attache un *placeholder de Position non-ouvert* à la limite/arrêt au repos `ORDER_ACCEPTED`/`CANCELLED`. `SourceExecutionsAsync` classe maintenant le placement/annulation comme événement d'ordre avant la branche de position, mais laisse le remplissage limite/arrêt (par ex. fermeture déclenchée par stop-loss) tomber dans le chemin de fermeture.
- **Tokens de rafraîchissement à usage unique** : cTrader tourne le token de rafraîchissement à chaque rafraîchissement. Le cache en lecture seule qui ne peut pas persister s'auto-invalide. Le Job K8s en direct copie donc le Secret dans un **emptyDir inscriptible** ; Job par défaut à la suite déterministe. `SaveTokens` maintenant best-effort. Les symboles en direct forcés au FX (amendements traîné BTCUSD rejetés par le courtier).
- Le nommage d'image de script corrigé pour correspondre au split Helm `registry/repository` + `pullPolicy=Never`.

## Programme de mirroring avancé + cycle de vie du token + mise à l'échelle (2026-07-10) — les tests des tiers déterministes passent

Le programme de suivi ajoute le filtrage de type de commande, la copie d'expiration d'ordre en attente, le mirroring de glissement de plage de marché /
stop-limite, les bascules de copie SL/TP, l'échange de token gracieux en place (token unique valide par cID), le simulateur fidèle cTrader, le bail de nœud d'auto-guérison, le fichier unifié d'identifiants dev.

- **Unit — 210 passé** (`dotnet test tests/UnitTests`). Couverture nouvelle de copie : filtre de type de commande
  (ouvrir + en attente), miroir de glissement de plage de marché + prix de base, copie d'expiration activée/désactivée, glissement stop-limite,
  amend en attente, démarrage avec maître ouvert, déconnexion→maître-échangé→reconnexion resync
  (ouverture manquante + fermeture orphelin), échange de token en place (pas redémarrage), invalidation cross-cID,
  invariants de domaine, propriété de bail, bump de version de token.
- **Integration (Postgres réel, Testcontainers) — pass** : `CopyNodeAffinityTests` (revendication atomique,
  pas double-copie, libération de pause, **reclamation de bail expiré par un autre nœud**),
  `TokenRotationSignatureTests` (la signature change lors du bump de version de token),
  `OpenApiAuthorizationPersistenceTests` (TokenVersion persiste + s'incrémente au rafraîchissement).
- **E2E** (`tests/E2ETests`) : option de destination aller-retour affirme maintenant le filtre de type de commande,
  copie-expiration, copie-glissement aux côtés du cycle de vie complet.
- **Build** : propre sous `TreatWarningsAsErrors` ; Rider `get_file_problems` propre sur les fichiers modifiés.

Les scénarios en direct (comptes démo cTrader réels) pour stop en attente, plage de marché, expiration, démarrage avec ouverture,
rotation de token mid-run sont rédigés contre le même moteur ; s'exécutent avec
`secrets/dev-credentials.local.json` unifié par [dev-credentials.md](dev-credentials.md).

## Suivi connu
Le run en direct in-cluster a tourné le token à usage unique ; régénérez le cache local avec
`CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`
(cTrader a limité sa page OAuth juste après le run — réessayez quand ça s'efface).
