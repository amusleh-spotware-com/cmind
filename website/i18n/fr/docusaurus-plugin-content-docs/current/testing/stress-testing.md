---
description: "Suite de stress. Frappe des parties d'app dont l'échec coûte de l'argent aux utilisateurs — principalement copie commerciale — avec des charges hostiles, aléatoires, injectées de failles. Affirme le système…"
---

# Stress testing

Suite de stress. Frappe des parties d'app dont l'échec coûte de l'argent aux utilisateurs — principalement **copie commerciale** — avec des charges hostiles, aléatoires, injectées de failles. Affirme le système reste correct. Vit dans `tests/StressTests`, s'exécute dans une normale `dotnet test` porte verte.

## Approche — Deterministic Simulation Testing (DST)

Meilleure façon de stresser les systèmes financiers distribués = **deterministic simulation testing**, par TigerBeetle, FoundationDB, Antithesis : exécutez la logique réelle contre le monde *simulé*, conduisez avec une charge de travail aléatoire **ensemencée** + failles injectées, affirmez les invariants à la quiescence. Tous ensemencés + déterministes → tout échec se reproduit exact de la graine. Combiné avec :

- **Chaos-engineering fault injection** (style Netflix Chaos Monkey) — chutes de connexion, rejets d'ordre, rotation de token, mort du nœud.
- **Invariants basés sur les propriétés** — pas d'assertion des séquences exactes d'appels ; affirmez les propriétés qui doivent tenir peu importe comment les événements s'entrelacent (convergence, pas d'orphelins, au-plus-un titulaire de bail).

L'app livre déjà un modèle DST parfait : `FakeTradingSession`, session API Open fidèle cTrader en mémoire. La suite de stress la réutilise (liée, source de vérité unique) pas mock, donc le courtier simulé se comporte comme réel.

## Ce qu'elle couvre

### Copie commerciale (foyer primaire)

Conduite via `CopyDstWorld` (`tests/StressTests/CopyTrading/`), exécute le `CopyEngineHost` en direct contre la session fausse, émet une charge de travail source consistent-membership :

| Scénario | Stress |
|---|---|
| `Mass_fan_out…` | 1 source → 80 destinations, 150 ouvertures puis fermetures ; fan-out + drain complet |
| `High_frequency_open_close…` | 300 rapides interverrouillées ouverture/fermeture ; pas positions fuitées |
| `Partial_close_and_scale_in_storm…` | churn fermeture partielle + scale-in ; stabilité ensemble d'étiquettes |
| `Connection_flap_storm…` | déconnexion/reconnexion socket répétée + désync mid-flight ; convergence resync |
| `Order_rejection_cascade…` | un sous-ensemble rejette chaque ordre ; destinations saines non affectées, puis auto-guérison via resync |
| `Token_rotation_storm…` | échanges de token rapides en place lors d'un orage d'ordre |
| `Randomized_chaos_workload…` (10 graines) | **le DST core** — chaque type d'événement + chaque faute entrelacée imprévisiblement |
| `CopyLeaseReclaimStressTests` | mort du nœud + reclaim du bail sur un cluster à échelle (domaine pur, `FakeTimeProvider`) |

**Invariant de convergence.** Au repos, chaque destination saine reflète exactement l'ensemble des positions source toujours ouvertes — pas d'orphelins, aucun manquant. Affiremé sur l'ensemble d'étiquettes (scale-in ouvre légitimement la deuxième position de destination du profil sous le même étiquette source, donc les doublons d'étiquettes attendus). La destination actuelle rejetant les ordres est autorisée à rester en arrière, réconciliée une fois guérie.

**Invariant de bail.** Dans le cluster où les nœuds meurent + reviennent sur horaire ensemencé, au plus un nœud tient un bail valide sur un profil ; le bail du nœud mort expire exactement à l'expiration, se faire réclamer ; le cluster sain se règle avec chaque profil détenu par exactement un nœud. Reflète le prédicat de revendication du `CopyEngineSupervisor` contre les méthodes de bail de domaine `CopyProfile`.

### Thread-safety du harnais

`FakeTradingSession` monothread ; la charge de travail de stress la mute du thread de test tandis que l'hôte lit/écrit à partir de sa boucle. `SyncTradingSession` l'enveloppe, rend chaque opération de session atomique sur une porte (sans tenir la porte à travers le rappel reconnexion — inverserait l'ordre de verrou vs le `_stateGate` de l'hôte et deadlock). Le simulateur lui-même laissé intouché.

## Bugs trouvés

- **Course de resync de démarrage en `CopyEngineHost`.** `OnReconnected` câblé avant le chargement de référence initial + premier resync, qui s'exécutait sans `_stateGate`. Le basculement de socket pendant le démarrage exécutait le second resync simultanément, corrompant les dicts d'état non-concurrents de l'hôte (`_symbolDetails`, `_sourceVolumes`). Corrigé : exécuter le chargement de démarrage + le premier resync sous la porte. Course de production, pas artifact de test — la charge de travail chaos DST l'a surfacée.

## Exécution

```bash
dotnet test tests/StressTests/StressTests.csproj
```

La suite est **sérialisée** (`[assembly: CollectionBehavior(DisableTestParallelization = true)]`) : chaque test tourne en direct la boucle de fond de l'hôte, conduit à la quiescence sous l'horloge murale, donc l'exécution en parallèle affame les tâches de l'hôte et rend les timeouts de convergence flaky. Les charges de travail sont dimensionnées pour finir en secondes de sorte que la suite reste dans la porte verte par défaut. L'échec imprime sa graine ; re-exécutez cette graine pour reproduire l'entrelacement exact.

## Extension

- Nouveau comportement de copie → ajoutez l'op source à `CopyDstWorld` (gardez la membership book source cohérente avec le flux d'événements) + cas pondéré en `CopyChaosDstTests`. S'il peut créer ou retirer une position de destination, assurez-vous que l'invariant de convergence tient toujours.
- Nouvelle faute → ajoutez l'injecteur à `CopyDstWorld` (déléguez à la surface de contrôle de `FakeTradingSession` via `SyncTradingSession`) + exercez en un scénario nommé plus mélange de chaos.
- Gardez le simulateur fidèle cTrader (voir le mandat root `CLAUDE.md`) ; ne l'affaiblissez jamais pour faire passer un test de stress.
