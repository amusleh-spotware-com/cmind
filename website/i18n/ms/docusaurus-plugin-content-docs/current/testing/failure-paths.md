---
title: Peta perlindungan laluan kegagalan
description: Setiap senario kegagalan yang dimandatkan, dipetakan kepada ujian yang sebenarnya melatihnya — jadi jurang boleh dilihat, tidak diandaikan.
---

# Peta perlindungan laluan kegagalan

Mandatori ujian adalah nyata: **laluan kegagalan dikira** — perubahan yang boleh pecah pada sambungan yang jatuh,
penolakan pesanan, nycersync, putaran token, atau nod mati ships dengan ujian untuk nó,
dalam komit yang sama. Halaman ini memetakan setiap senario yang diperlukan kepada ujian yang melatbelinya, jadi jurang sebenar *terlihat* bukan diandaikan. Apabila anda menambah laluan kegagalan, tambahkan baris di sini.

## Senario diperlukan → ujian

| Senario | Lapisan | Ujian |
|---|---|---|
| **Sambungan jatuh → sambung semula** | unit · tekanan · E2E | `OpenApiConnectionTests.Dropped_connection_reconnects_and_raises_reconnected`; `FakeTradingSession.Disconnect/ReconnectAsync` dan `SyncTradingSession` (DST); `MiscUiTests` keadaan modal sambung semula |
| **Penolakan pesanan** | unit · tekanan | `CopyTransparencyTests.A_rejected_open_emits_a_Failed_execution_fact_with_the_reason`; `CopyCircuitBreakerTests`; DST `CopyDstWorld.FailOrders` / `RejectMarketRange` |
| **Nycersync / resync** | unit · tekanan | `CopyPartialFillTests.Resync_tops_up_a_broker_partial_fill…`; `CopyEngineHostTests.Reconnect_resync_closes_orphaned_destination_positions` (+ `…tolerates_a_position_not_found…`); `CopyAdvancedScenariosTests.Reconnect_resync_opens_missing_copies_and_closes_orphans_after_a_desync`; `CopyChaosDstTests` |
| **Putaran / pembatalan token** | unit · integrasi · tekanan | `OpenApiAuthorizationTests.MarkRefreshFailed_*` (tetingkap eskalasi); `FakeTradingSession.InvalidateToken`; `TokenRotationSignatureTests`, `LiveTokenBootstrapTests`, `OpenApiTokenRefreshPersistenceTests` (integrasi); DST `RotateTokens` |
| **Kematian nod → ambil semula lesen** | unit · integrasi · tekanan | `NodeInstanceReclaimerTests` (unit + integrasi); `CopyRulesDomainTests.Lease_is_held_only_by_the_claiming_node_until_it_expires`; `CopyHostWatchdogTests`, `CopyNodeAffinityTests`, `PropFirmTrackingLeaseTests` (integrasi); `CopyLeaseReclaimStressTests` |
| **Ralat pembekal AI (4xx/5xx/masa tamat/malformed)** | unit · integrasi | `AnthropicAiClientTests.Fails_gracefully_on_error_status` / `…on_malformed_json` / `…on_empty_content`; `AiHttpResilienceTests`, `AiRecommendDisabledTests` (integrasi) |
| **AI dilumpuhkan sepenuhnya (tiada kunci)** | unit · integrasi · E2E | `AiFeatureServiceTests`; `AiRecommendDisabledTests`; `AiPagesTests` |
| **Kegagalan DB sementara / kunci migrasi** | integrasi | `DatabaseResilienceTests`; `MigrationLockTests` |
| **Kegagalan ejen nod HTTP / retry** | integrasi | `NodeAgentHttpResilienceTests` |
| **Container keluar sendiri sepadan** | unit | `BacktestCompletionPollerTests`; liputan `RunCompletionPoller` dalam `ContainerCommandHelpersTests` |
| **Pelanggaran prop-firm** | unit · integrasi | `PropFirmChallengeRulesTests`; `PropFirmAlertNotifierTests`; `PropFirmChallengePersistenceTests` |
| **Input tidak sah / auth tolak (UI + branding)** | unit · integrasi · E2E | `LoginTests.Invalid_credentials_show_an_error`; `HexColorTests.Rejects_invalid_hex`; `BrandingOptionsValidatorTests` |

## Titik nipis — sahkan sebelum mengandaikan liputan

Ini wajar disemak secara eksplisit (tambah baris di atas sebaik disahkan atau dipenuhi):

- **Penolakan auth alat MCP** — `McpKeyAuthHandler` menolak kunci buruk/tidak ada. Tiada ujian berdedikasi dijumpai; tambah ujian integrasi yang memanggil titik akhir alat MCP dengan kunci yang hilang/tidak sah dan menegaskan 401.
- **Pencerminan kegagalan bina CBot** — ralat kompilasi harus sampai ke contoh/UI sebagai `Failed` dengan output bina. `CBotLifecycleTests` liputan laluan feliz; sahkan cawangan kegagalan asserting.
- **Pelaksanaan pesanan langsung** — pelaksanaan salinan end-to-end terhadap kredensi cTrader sebenar masih terkunci (memerlukan kredensi + kluster nod); lihat [Live copy trading](./live-copy-trading.md).

## Bagaimana ini dikuatkuasakan

Suite tekanan deterministik (DST, `tests/StressTests`) memutar semula kegagalan ini pada jam kompak dan harus kekal hijau — **tidak pernah melemahkan senario DST untuk melepaskannya melepasi; baiki kod**. [FakeTradingSession](./fake-trading-session.md) ialah simulator yang setia kepada cTrader yang diuji unit ini gunakan; perluasnya untuk kelakuan broker baharu dan bukan meredakan assertion.
