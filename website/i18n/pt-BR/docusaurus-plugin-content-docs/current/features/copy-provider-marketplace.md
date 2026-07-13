---
description: "Diretório navegável de estratégias de cópia. Fornecedor publica perfil de cópia como listagem com badge verificado ao vivo (conta de fonte de estratégia troca dinheiro real, não…"
---

# Marketplace de fornecedor de cópia (Fase 4)

Diretório navegável de estratégias de cópia. Fornecedor **publica** perfil de cópia como listagem com badge **verificado ao vivo** (conta de fonte de estratégia troca dinheiro real, não demo) mais taxa de desempenho. Seguidores navegam marketplace, classificados por pontuação de desempenho projetada a partir de dados de transparência de execução.

## Modelo

- `CopyProviderListing` = agregado: `UserId`, `ProfileId`, nome de exibição, descrição, taxa de desempenho, `VerifiedLive`, `Published` + `PublishedAt`. Uma listagem por perfil (índice único).
- **Verificado ao vivo** derivado na hora da publicação da fonte de perfil `TradingAccount.IsLive` — fornecedor não pode auto-afirmar.
- Stats de desempenho **não armazenados em listagem** — projeção read-model em log de transparência `CopyExecution` (taxa de preenchimento, latência média, slippage realizado médio), para que marketplace sempre reflita qualidade de execução ao vivo.

## Ranking

`CopyEndpoints.MarketplaceScore(fillRate, avgLatencyMs, avgSlippagePoints, verifiedLive)` → pontuação 0–100: taxa de preenchimento domina (×60), baixa latência + baixo slippage adicionam (×20 cada), badge verificado ao vivo adiciona pequeno bônus de confiança. Determinístico + monótono, para que ordenação fique estável.

## API

- `POST /api/copy/profiles/{id}/publish` — publicar/atualizar listagem de perfil (`DisplayName`, `Description`, `PerformanceFeePercent`); verified-live defina da conta de origem.
- `DELETE /api/copy/profiles/{id}/publish` — despublicar.
- `GET /api/copy/marketplace` — todas as listagens publicadas, classificadas, cada uma com resumo de desempenho (execuções, taxa de preenchimento, latência média, slippage médio, pontuação) + badge verificado ao vivo.

## Testes

- **Unidade** (`CopyProviderListingTests`) — invariantes de agregado: nome de exibição obrigatório; publicar set timestamp; despublicar ocultar; update replace campos de exibição + taxa + badge.
- **Integração** (`CopyMarketplaceTests`, Postgres real) — listagem publicada persiste com badge; uma listagem por perfil (índice único); pontuação de ranking prefere fornecedores verificados/alta preenchimento.

Host de cópia intocado (listagens + read model apenas), para que suite stress DST de cópia não seja afetada.
