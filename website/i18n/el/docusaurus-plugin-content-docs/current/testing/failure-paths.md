---
title: Χάρτης κάλυψης failure-path
description: Κάθε failure scenario που απαιτεί η εντολή, αντιστοιχισμένο στα test(s) που πραγματικά το ασκούν — ώστε ένα κενό να είναι ορατό, όχι υποθετικό.
---

# Failure-path coverage map

Η εντολή test είναι ρητή: **τα failure paths μετράνε** — μια αλλαγή που μπορεί να
σπάσει σε dropped connection, rejected order, desync, token rotation ή dead node
αποστέλλεται με ένα test για αυτό, στο ίδιο commit. Αυτή η σελίδα αντιστοιχίζει κάθε
απαιτούμενο σενάριο στο test(s) που το ασκεί, ώστε ένα πραγματικό κενό να είναι *ορατό*
παρά υποθετικό. Όταν προσθέτετε ένα failure path, προσθέστε μια γραμμή εδώ.

## Απαιτούμενα σενάρια → tests

| Σενάριο | Tier(s) | Tests |
|---|---|---|
| **Connection drop → reconnect** | unit · stress · E2E | `OpenApiConnectionTests.Dropped_connection_reconnects_and_raises_reconnected`; `FakeTradingSession.Disconnect/ReconnectAsync` και `SyncTradingSession` (DST); `MiscUiTests` reconnect-modal states |
| **Order rejection** | unit · stress | `CopyTransparencyTests.A_rejected_open_emits_a_Failed_execution_fact_with_the_reason`; `CopyCircuitBreakerTests`; DST `CopyDstWorld.FailOrders` / `RejectMarketRange` |
| **Desync / resync** | unit · stress | `CopyPartialFillTests.Resync_tops_up_a_broker_partial_fill…`; `CopyEngineHostTests.Reconnect_resync_closes_orphaned_destination_positions` (+ `…tolerates_a_position_not_found…`); `CopyAdvancedScenariosTests.Reconnect_resync_opens_missing_copies_and_closes_orphans_after_a_desync`; `CopyChaosDstTests` |
| **Token rotation / invalidation** | unit · integration · stress | `OpenApiAuthorizationTests.MarkRefreshFailed_*` (escalation window); `FakeTradingSession.InvalidateToken`; `TokenRotationSignatureTests`, `LiveTokenBootstrapTests`, `OpenApiTokenRefreshPersistenceTests` (integration); DST `RotateTokens` |
| **Node death → lease reclaim** | unit · integration · stress | `NodeInstanceReclaimerTests` (unit + integration); `CopyRulesDomainTests.Lease_is_held_only_by_the_claiming_node_until_it_expires`; `CopyHostWatchdogTests`, `CopyNodeAffinityTests`, `PropFirmTrackingLeaseTests` (integration); `CopyLeaseReclaimStressTests` |
| **AI provider error (4xx/5xx/timeout/malformed)** | unit · integration | `AnthropicAiClientTests.Fails_gracefully_on_error_status` / `…on_malformed_json` / `…on_empty_content`; `AiHttpResilienceTests`, `AiRecommendDisabledTests` (integration) |
| **AI fully disabled (no key)** | unit · integration · E2E | `AiFeatureServiceTests`; `AiRecommendDisabledTests`; `AiPagesTests` |
| **Database transient failure / migration lock** | integration | `DatabaseResilienceTests`; `MigrationLockTests` |
| **Node HTTP agent failure / retry** | integration | `NodeAgentHttpResilienceTests` |
| **Container self-exit reconciliation** | unit | `BacktestCompletionPollerTests`; `RunCompletionPoller` coverage στο `ContainerCommandHelpersTests` |
| **Prop-firm breach** | unit · integration | `PropFirmChallengeRulesTests`; `PropFirmAlertNotifierTests`; `PropFirmChallengePersistenceTests` |
| **Invalid input / auth reject (UI + branding)** | unit · integration · E2E | `LoginTests.Invalid_credentials_show_an_error`; `HexColorTests.Rejects_invalid_hex`; `BrandingOptionsValidatorTests` |

## Thin spots — επαληθεύστε πριν υποθέσετε ότι καλύπτονται

Αυτά αξίζουν έναν explicit έλεγχο (προσθέστε μια γραμμή παραπάνω μόλις επιβεβαιωθεί ή συμπληρωθεί):

- **MCP tool auth rejection** — `McpKeyAuthHandler` rejects a bad/absent key. Δεν βρέθηκε
  dedicated test· προσθέστε ένα integration test που καλεί ένα MCP tool endpoint με
  missing/invalid key και asserts 401.
- **CBot build failure surfacing** — ένα compile error πρέπει να εμφανιστεί στο
  instance/UI ως `Failed` με το build output. Το `CBotLifecycleTests` καλύπτει το happy
  path· επιβεβαιώστε ότι το failure branch είναι asserted.
- **Live order execution** — η end-to-end copy execution ενάντια σε real cTrader credentials
  παραμένει gated (χρειάζεται credentials + ένα node cluster)· δείτε [Live copy trading](./live-copy-trading.md).

## Πώς αυτό επιβάλλεται

Η deterministic stress suite (DST, `tests/StressTests`) replay αυτά τα failures σε ένα
compressed clock και πρέπει να μένει green — **ποτέ μην αποδυναμώσετε ένα DST scenario για να
περάσει· διορθώστε τον κώδικα**. Το [FakeTradingSession](./fake-trading-session.md) είναι
ο cTrader-faithful simulator που αυτά τα unit tests οδηγούν· επεκτείνετέ τον για νέα
broker behavior αντί να χαλαρώσετε μια assertion.
