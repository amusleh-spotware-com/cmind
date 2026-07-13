---
title: Zemljevid pokritosti poti napak
description: "Vsak scenarij napake ki ga mandat zahteva, preslikan v test(e) ki ga dejansko vadijo — tako da je vrzel vidna, ne domnevana."
---

# Zemljevid pokritosti poti napak

Testni mandat je ekspliciten: **pot napak štejejo** — sprememba ki lahko propade na padli
povezavi, zavrnjenem naročilu, desyncu, rotaciji žetona ali mrtvem vozlišču ladja s testom zanjo,
v istem commitu. Ta stran preslika vsak zahtevani scenarij v test(e) ki ga(e) vadijo, torej prava
vrzel je *vidna* kot domnevna. Ko dodajate pot napake, dodajte vrstico tukaj.

## Zahtevani scenariji → testi

| Scenarij | Plast(i) | Testi |
|---|---|---|
| **Pad povezave → ponovno povezovanje** | enota · stress · E2E | `OpenApiConnectionTests.Dropped_connection_reconnects_and_raises_reconnected`; `FakeTradingSession.Disconnect/ReconnectAsync` in `SyncTradingSession` (DST); `MiscUiTests` reconnect-modal states |
| **Zavrnitev naročila** | enota · stress | `CopyTransparencyTests.A_rejected_open_emits_a_Failed_execution_fact_with_the_reason`; `CopyCircuitBreakerTests`; DST `CopyDstWorld.FailOrders` / `RejectMarketRange` |
| **Desync / resync** | enota · stress | `CopyPartialFillTests.Resync_tops_up_a_broker_partial_fill…`; `CopyEngineHostTests.Reconnect_resync_closes_orphaned_destination_positions` (+ `…tolerates_a_position_not_found…`); `CopyAdvancedScenariosTests.Reconnect_resync_opens_missing_copies_and_closes_orphans_after_a_desync`; `CopyChaosDstTests` |
| **Rotacija / razveljavitev žetona** | enota · integracija · stress | `OpenApiAuthorizationTests.MarkRefreshFailed_*` (eskalacijsko okno); `FakeTradingSession.InvalidateToken`; `TokenRotationSignatureTests`, `LiveTokenBootstrapTests`, `OpenApiTokenRefreshPersistenceTests` (integracija); DST `RotateTokens` |
| **Smrt vozlišča → prevzem lease** | enota · integracija · stress | `NodeInstanceReclaimerTests` (enota + integracija); `CopyRulesDomainTests.Lease_is_held_only_by_the_claiming_node_until_it_expires`; `CopyHostWatchdogTests`, `CopyNodeAffinityTests`, `PropFirmTrackingLeaseTests` (integracija); `CopyLeaseReclaimStressTests` |
| **Napaka AI ponudnika (4xx/5xx/timeout/malformed)** | enota · integracija | `AnthropicAiClientTests.Fails_gracefully_on_error_status` / `…on_malformed_json` / `…on_empty_content`; `AiHttpResilienceTests`, `AiRecommendDisabledTests` (integracija) |
| **AI popolnoma onemogočen (brez ključa)** | enota · integracija · E2E | `AiFeatureServiceTests`; `AiRecommendDisabledTests`; `AiPagesTests` |
| **Prehodna napaka zbirke podatkov / migrate lock** | integracija | `DatabaseResilienceTests`; `MigrationLockTests` |
| **Neuspeh HTTP agenta vozlišča / ponovitev** | integracija | `NodeAgentHttpResilienceTests` |
| **Container samo-izhod uskladitev** | enota | `BacktestCompletionPollerTests`; `RunCompletionPoller` pokritost v `ContainerCommandHelpersTests` |
| **Prop-firm prelom** | enota · integracija | `PropFirmChallengeRulesTests`; `PropFirmAlertNotifierTests`; `PropFirmChallengePersistenceTests` |
| **Neveljaven vnos / avtentikacija zavrni (UI + blagovna znamka)** | enota · integracija · E2E | `LoginTests.Invalid_credentials_show_an_error`; `HexColorTests.Rejects_invalid_hex`; `BrandingOptionsValidatorTests` |

## Tanke točke — preveri preden domnevaš pokrito

Te so vredne eksplicitne kontrole (dodaj vrstico zgoraj ko potrjeno ali napolnjeno):

- **MCP tool auth zavrnitev** — `McpKeyAuthHandler` zavrne slab/odsoten ključ. Ni bil najden namenski test; dodaj integracijski test ki kliče MCP tool endpoint z manjkajočim/neveljavnim ključem in trdi 401.
- **CBot build napaka površinska** — napaka kompilacije mora pristati na instanci/UI kot `Failed` z izhodom builda. `CBotLifecycleTests` pokriva srečen pot; potrdi da je neuspešna veja trjena.
- **Live izvedba naročila** — end-to-end copy izvedba proti realnim cTrader poverilnicam ostaja gate (potrebuje poverilnice + gručo vozlišč); glej [Live copy trading](./live-copy-trading.md).

## Kako se to uveljavlja

Deterministični stress suite (DST, `tests/StressTests`) predvaja te napake na stisnjeni uri in mora ostati zelen — **nikoli ne oslabi DST scenarija da bi naredil da poteka; popravi kodo**. [FakeTradingSession](./fake-trading-session.md) je cTrader-veren simulator ki ga ti unit testi poganjajo; razširi ga za novo broker vedenje kot da bi sprostil trditev.
