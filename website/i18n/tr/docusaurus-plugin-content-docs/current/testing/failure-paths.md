---
title: Başarısızlık-yolu kapsam haritası
description: Yönergenin gerektirdiği her başarısızlık senaryosu, onu gerçekten çalıştıran test(ler)le eşleştirilmiş — böylece bir boşluk varsayılmaz, görünür olur.
---

# Başarısızlık-yolu kapsam haritası

Test yönergesi açıktır: **başarısızlık yolları önemlidir** — düşen bir bağlantıda, reddedilen bir emirde,
bir senkronizasyon kopmasında, bir belirteç rotasyonunda veya ölü bir düğümde kırılabilecek bir değişiklik,
aynı işlemde bunun için bir testle gönderilir. Bu sayfa, her gerekli senaryoyu onu çalıştıran test(ler)le
eşleştirir, böylece gerçek bir boşluk varsayılmak yerine *görünür* olur. Bir başarısızlık yolu eklediğinizde,
buraya bir satır ekleyin.

## Gerekli senaryolar → testler

| Senaryo | Katman(lar) | Testler |
|---|---|---|
| **Bağlantı kopması → yeniden bağlanma** | birim · stres · E2E | `OpenApiConnectionTests.Dropped_connection_reconnects_and_raises_reconnected`; `FakeTradingSession.Disconnect/ReconnectAsync` ve `SyncTradingSession` (DST); `MiscUiTests` yeniden-bağlanma-modal durumları |
| **Emir reddi** | birim · stres | `CopyTransparencyTests.A_rejected_open_emits_a_Failed_execution_fact_with_the_reason`; `CopyCircuitBreakerTests`; DST `CopyDstWorld.FailOrders` / `RejectMarketRange` |
| **Senkronizasyon kopması / yeniden senk.** | birim · stres | `CopyPartialFillTests.Resync_tops_up_a_broker_partial_fill…`; `CopyEngineHostTests.Reconnect_resync_closes_orphaned_destination_positions` (+ `…tolerates_a_position_not_found…`); `CopyAdvancedScenariosTests.Reconnect_resync_opens_missing_copies_and_closes_orphans_after_a_desync`; `CopyChaosDstTests` |
| **Belirteç rotasyonu / geçersiz kılma** | birim · entegrasyon · stres | `OpenApiAuthorizationTests.MarkRefreshFailed_*` (yükseltme penceresi); `FakeTradingSession.InvalidateToken`; `TokenRotationSignatureTests`, `LiveTokenBootstrapTests`, `OpenApiTokenRefreshPersistenceTests` (entegrasyon); DST `RotateTokens` |
| **Düğüm ölümü → kira geri alma** | birim · entegrasyon · stres | `NodeInstanceReclaimerTests` (birim + entegrasyon); `CopyRulesDomainTests.Lease_is_held_only_by_the_claiming_node_until_it_expires`; `CopyHostWatchdogTests`, `CopyNodeAffinityTests`, `PropFirmTrackingLeaseTests` (entegrasyon); `CopyLeaseReclaimStressTests` |
| **AI sağlayıcı hatası (4xx/5xx/zaman aşımı/bozuk)** | birim · entegrasyon | `AnthropicAiClientTests.Fails_gracefully_on_error_status` / `…on_malformed_json` / `…on_empty_content`; `AiHttpResilienceTests`, `AiRecommendDisabledTests` (entegrasyon) |
| **AI tamamen devre dışı (anahtar yok)** | birim · entegrasyon · E2E | `AiFeatureServiceTests`; `AiRecommendDisabledTests`; `AiPagesTests` |
| **Veritabanı geçici hatası / migrasyon kilidi** | entegrasyon | `DatabaseResilienceTests`; `MigrationLockTests` |
| **Düğüm HTTP ajan hatası / yeniden deneme** | entegrasyon | `NodeAgentHttpResilienceTests` |
| **Konteyner kendi-çıkışı uzlaşması** | birim | `BacktestCompletionPollerTests`; `ContainerCommandHelpersTests`'te `RunCompletionPoller` kapsamı |
| **Prop-firm ihlali** | birim · entegrasyon | `PropFirmChallengeRulesTests`; `PropFirmAlertNotifierTests`; `PropFirmChallengePersistenceTests` |
| **Geçersiz girdi / kimlik reddi (UI + markalama)** | birim · entegrasyon · E2E | `LoginTests.Invalid_credentials_show_an_error`; `HexColorTests.Rejects_invalid_hex`; `BrandingOptionsValidatorTests` |

## İnce noktalar — kapsandığını varsaymadan önce doğrulayın

Bunlar açık bir kontrole değer (onaylandığında veya doldurulduğunda yukarıya bir satır ekleyin):

- **MCP aracı kimlik reddi** — `McpKeyAuthHandler` kötü/eksik bir anahtarı reddeder. Özel bir test
  bulunamadı; eksik/geçersiz bir anahtarla bir MCP aracı uç noktasını çağıran ve 401 iddia eden bir
  entegrasyon testi ekleyin.
- **cBot derleme hatası yüzeye çıkarma** — bir derleme hatası, örnek/UI'da derleme çıktısıyla `Failed`
  olarak inmelidir. `CBotLifecycleTests` mutlu yolu kapsar; başarısızlık dalının iddia edildiğini onaylayın.
- **Canlı emir yürütme** — gerçek cTrader kimlik bilgilerine karşı uçtan uca kopya yürütme hâlâ kapılıdır
  (kimlik bilgileri + bir düğüm kümesi gerekir); bkz. [Canlı kopya işlem](./live-copy-trading.md).

## Bu nasıl zorunlu kılınır

Deterministik stres paketi (DST, `tests/StressTests`) bu başarısızlıkları sıkıştırılmış bir saatte yeniden
oynatır ve yeşil kalmalıdır — **bir DST senaryosunu geçmesi için asla zayıflatmayın; kodu düzeltin**.
[FakeTradingSession](./fake-trading-session.md), bu birim testlerinin yönlendirdiği cTrader-sadık
simülatördür; bir iddiayı gevşetmek yerine yeni broker davranışı için onu genişletin.
