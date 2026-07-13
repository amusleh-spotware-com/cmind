---
title: Failure-path coverage map
description: Každý failure scenár, ktorý mandát vyžaduje, mapovaný na test(y), ktoré ho skutočne cvičia — takže medzera je viditeľná, nie predpokladaná.
---

# Failure-path coverage map

Testovací mandát je explicitný: **failure paths sa počítajú** — zmena, ktorá môže zlyhať na dropped
connection, rejected order, desync, token rotation alebo dead node, sa dodáva s testom na to,
v rovnakom commite. Táto stránka mapuje každý požadovaný scenár na test(y), ktoré ho cvičia, takže reálna
medzera je *viditeľná* namiesto predpokladanej. Keď pridáte failure path, pridajte riadok sem.

## Požadované scenáre → testy

| Scenár | Tier(s) | Testy |
|---|---|---|
| **Connection drop → reconnect** | jednotka · stress · E2E | `OpenApiConnectionTests.Dropped_connection_reconnects_and_raises_reconnected`; `FakeTradingSession.Disconnect/ReconnectAsync` a `SyncTradingSession` (DST); `MiscUiTests` reconnect-modal states |
| **Order rejection** | jednotka · stress | `CopyTransparencyTests.A_rejected_open_emits_a_Failed_execution_fact_with_the_reason`; `CopyCircuitBreakerTests`; DST `CopyDstWorld.FailOrders` / `RejectMarketRange` |
| **Desync / resync** | jednotka · stress | `CopyPartialFillTests.Resync_tops_up_a_broker_partial_fill…`; `CopyEngineHostTests.Reconnect_resync_closes_orphaned_destination_positions` (+ `…tolerates_a_position_not_found…`); `CopyAdvancedScenariosTests.Reconnect_resync_opens_missing_copies_and_closes_orphans_after_a_desync`; `CopyChaosDstTests` |
| **Token rotation / invalidation** | jednotka · integrácia · stress | `OpenApiAuthorizationTests.MarkRefreshFailed_*` (escalation window); `FakeTradingSession.InvalidateToken`; `TokenRotationSignatureTests`, `LiveTokenBootstrapTests`, `OpenApiTokenRefreshPersistenceTests` (integrácia); DST `RotateTokens` |
| **Node death → lease reclaim** | jednotka · integrácia · stress | `NodeInstanceReclaimerTests` (jednotka + integrácia); `CopyRulesDomainTests.Lease_is_held_only_by_the_claiming_node_until_it_expires`; `CopyHostWatchdogTests`, `CopyNodeAffinityTests`, `PropFirmTrackingLeaseTests` (integrácia); `CopyLeaseReclaimStressTests` |
| **AI provider error (4xx/5xx/timeout/malformed)** | jednotka · integrácia | `AnthropicAiClientTests.Fails_gracefully_on_error_status` / `…on_malformed_json` / `…on_empty_content`; `AiHttpResilienceTests`, `AiRecommendDisabledTests` (integrácia) |
| **AI fully disabled (no key)** | jednotka · integrácia · E2E | `AiFeatureServiceTests`; `AiRecommendDisabledTests`; `AiPagesTests` |
| **Database transient failure / migration lock** | integrácia | `DatabaseResilienceTests`; `MigrationLockTests` |
| **Node HTTP agent failure / retry** | integrácia | `NodeAgentHttpResilienceTests` |
| **Container self-exit reconciliation** | jednotka | `BacktestCompletionPollerTests`; `RunCompletionPoller` coverage in `ContainerCommandHelpersTests` |
| **Prop-firm breach** | jednotka · integrácia | `PropFirmChallengeRulesTests`; `PropFirmAlertNotifierTests`; `PropFirmChallengePersistenceTests` |
| **Invalid input / auth reject (UI + branding)** | jednotka · integrácia · E2E | `LoginTests.Invalid_credentials_show_an_error`; `HexColorTests.Rejects_invalid_hex`; `BrandingOptionsValidatorTests` |

## Thin spots — overiť pred predpokladaním pokrytia

Tieto stoja za explicitnú kontrolu (pridajte riadok vyššie akonáhle potvrdené alebo vyplnené):

- **MCP tool auth rejection** — `McpKeyAuthHandler` odmieta bad/absent key. Žiadny dedikovaný test nebol
  nájdený; pridajte integračný test, ktorý volá MCP tool endpoint s chýbajúcim/invalidným key a assertuje 401.
- **CBot build failure surfacing** — compile error musí pristáť na inštancii/UI ako `Failed` s build output. `CBotLifecycleTests`
  pokrýva happy path; potvrďte, že failure branch je assertovaný.
- **Live order execution** — end-to-end copy execution proti reálnym cTrader creds zostáva gated
  (potrebuje creds + node cluster); pozrite [Live copy trading](./live-copy-trading.md).

## Ako je toto enforced

Deterministic stress suite (DST, `tests/StressTests`) replayuje tieto failures na compressed
clock a musí zostať zelená — **nikdy neoslabujte DST scenár pre passing; opravte kód**. [FakeTradingSession](./fake-trading-session.md)
je cTrader-faithful simulátor, ktorý tieto jednotkové testy hnajú; rozširujte ho pre nové broker
správanie namiesto relaxácie assertion.
