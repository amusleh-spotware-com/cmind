---
description: "Verificacao completa do trabalho restante de copy-trading — tudo abaixo realmente executado, nao apenas autoria."
---

# Execucao de verificacao de copy-trading (2026-07-10)

Verificacao completa do trabalho restante de copy-trading — tudo abaixo **realmente executado**, nao apenas autoria.

## Ao vivo (contas demo cTrader reais) — 8/8 passam
1:1 · 1:many · reverse · cross-cID · partial-close · **pending limit + cancel** · **trailing stop** · token-refresh.
Cenarios live adicionados `RunPendingAsync` / `RunTrailingAsync` (+ `LoadSpotPriceAsync`, `OpenPositionSnapshot.StopLoss/TrailingStopLoss`).

## Integracao (Postgres real, Testcontainers) — passa
- `CopyNodeAffinityTests` — reclaim atomic real do supervisor: primeiro no reclama todos os perfis em execucao, segundo reclama **0** (sem double-copy); pause libera + reclama.
- `TokenRotationSignatureTests` — assinatura muda somente em rotacao real de token.

## In-cluster (kind + Helm) — passa
Instalado `kind`/`kubectl`/`helm`, executou `scripts/k8s-e2e.sh` contra cluster kind real:
- **Job Deterministic: 101 passou** in-cluster.
- **Job Live: 8 passou** in-cluster (init-container `seed-secrets` copia Secret → emptyDir gravavel, contas demo reais).
- Job `Complete 1/1`, script exit 0.

## Bugs encontrados durante verificacao (corrigidos + re-verificados)
- **Eventos pendentes**: cTrader anexa *placeholder de Posicao nao-aberta* a ordem pendente resting limit/stop `ORDER_ACCEPTED`/`CANCELLED`. `SourceExecutionsAsync` agora classifica placement/cancel como evento de ordem antes do branch de posicao, mas permite que fill de limit/stop (ex. stop-loss-triggered close) caia no caminho de fechamento.
- **Refresh tokens de uso unico**: cTrader gira refresh token a cada atualizacao. Cache somente leitura que nao pode persistir se auto-invalida. Job K8s live copia Secret em **emptyDir** gravavel; Job padrao para suite deterministica. `SaveTokens` agora best-effort. Simbolos live forçados para FX (BTCUSD trailing amendments broker-rejected).
- Nomenclatura de imagem de script corrigida para corresponder ao split `registry/repository` do Helm + `pullPolicy=Never`.

## Programa de espelhamento avancado + ciclo-de-vida-de-token + escala (2026-07-10) — tiers deterministicos passam

Programa de acompanhamento adiciona filtragem de tipo de ordem, copia de vencimento de ordem pendente, slippage de faixa de mercado /
stop-limit, toggles de copia SL/TP, troca de token no local harmonica (um token valido por cID),
simulador fiel a cTrader, lease de no auto-cicatrizante, arquivo unificado de dev-credentials.

- **Unidade — 210 passou** (`dotnet test tests/UnitTests`). Nova cobertura de copia: filtro de tipo de ordem
  (abertura + pendente), slippage de espelhamento de faixa de mercado + preco base, copia de vencimento on/off, slippage stop-limit,
  alteracao pendente, iniciar-com-mestre-aberto, desconectar→mestre-negociou→reconectar ressincroniza
  (abertura faltante + fechamento orfao), troca de token no local (sem reinicio), invalidade cross-cID,
  invariantes de dominio, propriedade de lease, bump de token-version.
- **Integracao (Postgres real, Testcontainers) — passa**: `CopyNodeAffinityTests` (reclamacao atomica,
  sem double-copy, liberacao de pause, **reclam de lease expirado por outro no**),
  `TokenRotationSignatureTests` (assinatura muda em bump de token-version),
  `OpenApiAuthorizationPersistenceTests` (TokenVersion persiste + incrementa em atualizacao).
- **E2E** (`tests/E2ETests`): viagem de opcao de destino agora asserta filtro de tipo de ordem,
  copia-expiracao, slippage de copia junto com ciclo de vida completo.
- **Build**: limpo sob `TreatWarningsAsErrors`; `get_file_problems` do Rider limpo em arquivos alterados.

Cenarios live (contas demo cTrader reais) para pending-stop, faixa de mercado, expiracao, iniciar-com-aberto,
rotacao de token mid-run autoria contra mesmo motor; executa com
`secrets/dev-credentials.local.json` unificado por [dev-credentials.md](dev-credentials.md).

## Follow-up conhecido
Execucao live in-cluster girou token de uso unico; regenere cache local com
`CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`
(cTrader acelerou sua pagina OAuth bem apos a execucao — tente novamente quando limpar).
