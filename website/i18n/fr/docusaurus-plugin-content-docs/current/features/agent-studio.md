---
description: "Agent Studio — créez des agents de trading sans code, pilotés par un persona et un archétype, qui gèrent vos comptes en fonction de vos objectifs, sous le noyau d'autonomie et de sécurité (enveloppe de risque, disjoncteur, coupe-circuit, consentement à l'avertissement versionné)."
---

# Agent Studio

Agent Studio vous permet de créer un **agent de trading avec un caractère** — sans code — et de lui confier la gestion de vos comptes en fonction d'objectifs mesurables. Un agent est comme un cBot piloté par la personnalité : vous choisissez un archétype et une attitude, définissez les garde-fous, et il s'exécute sous le **noyau d'autonomie et de sécurité**.

Ouvrez **AI → Agent Studio** (`/agent-studio`).

## Créer un agent

Le dialogue **Nouvel agent** collecte, sans code :

- **Nom** et **archétype** — Scalper, Day Trader, Swing Trader, Position Trader, News Trader,
  Contrarien, Mean Reversion ou Breakout/Momentum. Chaque préréglage fixe une cadence et une posture cohérentes.
- **Attitude** — curseurs d'agressivité, de patience et de suivi de tendance.
- **Niveau d'autonomie** — **Advisory** (propose uniquement) ou **Approval-gated** (n'agit qu'après votre
  approbation par action). **Full Auto** (sans approbation par transaction) nécessite en outre une **enveloppe de risque**
  et l'acceptation de l'avertissement de risque avant de pouvoir s'armer.

Le persona se compile **déterministiquement** dans le prompt système de l'agent (aucun LLM ne le rédige), de sorte que la même configuration produise toujours les mêmes instructions — reproductibles et auditables.

## Le tableau de bord

Chaque agent apparaît dans un tableau de contrôle : **quel agent, son type, combien de comptes il gère, ses
objectifs, son état d'exécution et sa dernière action**, avec les commandes **Start / Stop / Kill**. Le coupe-circuit arrête un agent en cours d'exécution immédiatement.

## La sécurité est un invariant du domaine, pas un paramètre

Tout ce qui touche de l'argent passe par le **noyau d'autonomie et de sécurité** :

- **Enveloppe de risque** — limites par ordre (perte quotidienne maximale, exposition ouverte, taille de position, levier,
  pertes consécutives, ordres/heure, symboles autorisés). Chaque ordre est validé contre celle-ci avant envoi ;
  une violation est refusée, pas limitée. Requise avant qu'un agent puisse atteindre Full Auto.
- **Disjoncteur** — arrête déterministiquement tout nouveau risque sur une série de pertes, une violation de perte quotidienne,
  une **violation d'objectif de performance dur**, ou **l'indisponibilité du fournisseur IA** (un modèle en panne ouhallucinant n'ouvre jamais de nouvelles positions).
- **Consentement à l'avertissement versionné** — une acceptation unique et versionnée est requise pour armer Full Auto
  (consentement juridiquement requis, pas une approbation par transaction) ; incrémenter l'avertissement force le re-consentement.
- **Coupe-circuit** — un arrêt d'urgence idempotent sur chaque agent en cours d'exécution.

## Objectifs

Donnez à un agent des **objectifs mesurables** — p. ex. *garder le drawdown maximal sous 4%*, *facteur de profit d'au moins
1,5*, *taux de gain ≥ 55%*. Chaque cible est **Hard** (un garde-fou — une violation déclenche le disjoncteur) ou
**Soft** (oriente le raisonnement uniquement), évaluée comme On-track / At-risk / Breached.

## Le pipeline de décision

Une fois démarré, un agent exécute une **boucle supervisée 24/7** (`AgentRuntimeService`). À chaque tick, pour chaque
compte géré, il : lit l'**état déterministe du compte** (vérité terrain, jamais la mémoire du modèle) ;
interroge le moteur de décision pour un mouvement ; le passe à travers la **porte de sécurité** (`AgentDecisionProcessor`) —
niveau d'autonomie → disjoncteur → enveloppe de risque ; écrit un **`AgentDecisionRecord`** append-only ; et
s'arrête ou exécute selon ce que la porte décide. La boucle est **isolée par défaut** (l'échec d'un agent ne touche jamais
un autre ni l'hôte) et **sûre par défaut** : elle est inactive sauf si l'IA est configurée *et*
`App:Ai:AgentRuntimeEnabled` est défini, et elle n'ouvre jamais de nouveau risque tant que le fournisseur IA est indisponible.

- **Porte d'approbation** — l'ordre proposé par un agent **Approval-gated** est enregistré comme **Pending** et ne fait
  rien tant que le propriétaire ne l'approuve pas (`POST /api/agent-studio/{id}/decisions/{seq}/approve` ou
  `/reject`) ; **Full Auto** traverse l'enveloppe sans approbation par transaction ; **Advisory** ne fait que proposer.
- **Registre d'audit** — chaque décision est rejouable : raisonnement (XAI), les preuves citées, le verdict de la porte,
  l'intention d'ordre et s'il a été exécuté, sur `GET /api/agent-studio/{id}/decisions`.
- **Bureau d'études** — un débat multi-agent à la demande : les analystes Alpha/Sentiment/Technique/Risk donnent chacun
  leur avis et un Reviewer synthétise une proposition (`POST /api/agent-studio/{id}/debate`).
- **Mémoire** — l'agent se souvient de chaque décision et recall la mémoire récente dans son prochain prompt pour
  la continuité (`GET /api/agent-studio/{id}/memory`).

Ligne du tableau **Détails** de chaque agent ouvre le flux de décisions (avec Approve/Reject sur les ordres en attente),
sa mémoire et un onglet Run-debate.

## Périmètre

Livré : le cycle de vie complet de l'agent, la porte de sécurité déterministe, le runtime 24/7, la porte d'approbation
avec humain dans la boucle, le registre d'audit, et l'**intégration live cTrader Open API** — le stockage d'état du compte
(lit le solde réel, les positions et l'exposition ouverte en lots) et l'exécuteur d'ordres (passe de vrais ordres de marché,
lots → volume via la taille de lot du symbole), les deux résolvant les credentials OAuth de chaque compte géré et se dégradant
proprement lorsqu'un compte n'est pas lié. **Nécessite la clé API Anthropic** pour que le modèle génère des ordres
(jusqu'à ce, le moteur maintient) ; encore à venir : les rôles de débat multi-agent et la mémoire/couches de réflexion. Le runtime
est désactivé sauf si `App:Ai:AgentRuntimeEnabled` est défini, donc le trading live ne se produit que sur un opt-in explicite
et pleinement consenti.

## Comptes gérés et modification

Lors de la création d'un agent, vous choisissez le(s) compte(s) de trading qu'il gère (requis avant qu'il puisse démarrer).
Chaque agent peut être **modifié** par la suite (nom, tempérament, autonomie et comptes gérés) depuis l'icône crayon sur sa ligne du tableau.
Les contrôles de cycle de vie (détails, modifier, démarrer, arrêter, kill) sont des boutons icône,
chacun désactivé dans les états où l'action ne s'applique pas.
