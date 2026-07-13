---
title: Mapa de cobertura de caminho de falha
description: Cada cenário de falha que o mandato requer, mapeado para o teste(s) que realmente exercita — para que uma lacuna seja visível, não presumida.
---

# Mapa de cobertura de caminho de falha

O mandato de teste é explícito: **caminhos de falha contam** — uma mudança que pode quebrar em uma conexão descartada, um pedido rejeitado, uma desincronização, uma rotação de token ou um nó morto é enviado com um teste para isso, no mesmo commit. Esta página mapeia cada cenário obrigatório para o teste(s) que o exercita, para que uma lacuna real seja *visível* em vez de presumida. Quando você adicionar um caminho de falha, adicione uma linha aqui.

## Cenários obrigatórios → testes

| Cenário | Camada(s) | Testes |
|---|---|---|
| **Conexão descartada → reconectar** | unidade · stress · E2E | `OpenApiConnectionTests.Dropped_connection_reconnects_and_raises_reconnected`; `FakeTradingSession.Disconnect/ReconnectAsync` e `SyncTradingSession` (DST); `MiscUiTests` estados de modal de reconexão |
| **Rejeição de pedido** | unidade · stress | `CopyTransparencyTests.A_rejected_open_emits_a_Failed_execution_fact_with_the_reason`; `CopyCircuitBreakerTests`; DST `CopyDstWorld.FailOrders` / `RejectMarketRange` |
| **Desincronização / ressincronização** | unidade · stress | `CopyPartialFillTests.Resync_tops_up_a_broker_partial_fill…`; `CopyEngineHostTests.Reconnect_resync_closes_orphaned_destination_positions` (+ `…tolerates_a_position_not_found…`); `CopyAdvancedScenariosTests.Reconnect_resync_opens_missing_copies_and_closes_orphans_after_a_desync`; `CopyChaosDstTests` |
| **Rotação / invalidação de token** | unidade · integração · stress | `OpenApiAuthorizationTests.MarkRefreshFailed_*` (janela de escalação); `FakeTradingSession.InvalidateToken`; `TokenRotationSignatureTests`, `LiveTokenBootstrapTests`, `OpenApiTokenRefreshPersistenceTests` (integração); DST `RotateTokens` |
| **Morte do nó → reclamação de aluguel** | unidade · integração · stress | `NodeInstanceReclaimerTests` (unidade + integração); `CopyRulesDomainTests.Lease_is_held_only_by_the_claiming_node_until_it_expires`; `CopyHostWatchdogTests`, `CopyNodeAffinityTests`, `PropFirmTrackingLeaseTests` (integração); `CopyLeaseReclaimStressTests` |
| **Erro de fornecedor de IA (4xx/5xx/timeout/malformado)** | unidade · integração | `AnthropicAiClientTests.Fails_gracefully_on_error_status` / `…on_malformed_json` / `…on_empty_content`; `AiHttpResilienceTests`, `AiRecommendDisabledTests` (integração) |
| **IA totalmente desabilitada (sem chave)** | unidade · integração · E2E | `AiFeatureServiceTests`; `AiRecommendDisabledTests`; `AiPagesTests` |
| **Falha transitória de banco de dados / bloqueio de migração** | integração | `DatabaseResilienceTests`; `MigrationLockTests` |
| **Falha do agente HTTP do nó / retry** | integração | `NodeAgentHttpResilienceTests` |
| **Reconciliação de auto-saída do container** | unidade | `BacktestCompletionPollerTests`; `RunCompletionPoller` cobertura em `ContainerCommandHelpersTests` |
| **Violação de prop-firm** | unidade · integração | `PropFirmChallengeRulesTests`; `PropFirmAlertNotifierTests`; `PropFirmChallengePersistenceTests` |
| **Entrada inválida / rejeição de autenticação (interface + marca)** | unidade · integração · E2E | `LoginTests.Invalid_credentials_show_an_error`; `HexColorTests.Rejects_invalid_hex`; `BrandingOptionsValidatorTests` |

## Pontos finos — verificar antes de assumir coberto

Estes valem uma verificação explícita (adicione uma linha acima uma vez confirmada ou preenchida):

- **Rejeição de autenticação da ferramenta MCP** — `McpKeyAuthHandler` rejeita uma chave ruim/ausente. Nenhum teste dedicado foi encontrado; adicione um teste de integração que chama um ponto final de ferramenta MCP com uma chave ausente/inválida e afirma 401.
- **Falha de compilação de cBot superficializada** — um erro de compilação deve pousar na instância/interface como `Failed` com a saída de compilação. `CBotLifecycleTests` cobre o caminho feliz; confirme que o branch de falha é afirmado.
- **Execução de pedido ao vivo** — execução de cópia ponta a ponta contra credenciais cTrader reais permanece fechada (precisa de credenciais + cluster de nó); veja [Live copy trading](./live-copy-trading.md).

## Como isto é aplicado

A suite de stress determinístico (DST, `tests/StressTests`) repete essas falhas em um relógio comprimido e deve permanecer verde — **nunca enfraquça um cenário DST para fazê-lo passar; corrija o código**. O [FakeTradingSession](./fake-trading-session.md) é o simulador fiel a cTrader que esses testes de unidade conduzem; estenda-o para novo comportamento de broker em vez de relaxar uma afirmação.
