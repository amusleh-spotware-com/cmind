---
description: "Suite de testes de copy-trading completamente reprodivel. Duas camadas:"
---

# Suite de testes de copy-trading (deterministica + live)

Suite de testes de copy-trading completamente reprodivel. Duas camadas:

1. **Testes deterministicos** (xUnit, sem rede) — matematica de copia + logica do motor. Rapidos, CI, sem secrets. Cobrem cada modo de gerenciamaneto de dinheiro, cada filtro/opcao, resiliencia do motor.
2. **Testes E2E live** (contas demo cTrader reais) — `CopyEngineHost` real colocando + copiando ordens entre contas reais. Completamente automatizados, re-executaveis como teste de unidade: le credenciais em cache de arquivos gitignored locais, auto-refresca token de acesso, pula limpo quando secrets ausentes (CI permanece verde).

Nunca executa contra conta financiada live — toda conta **demo**, todo teste live fecha posicoes que abriu.

## Layout

```
tests/UnitTests/CopyTrading/
  CopySizingCalculatorTests.cs   — cada modo de dimensionamento + arredondamento + lote min/max
  CopyDecisionEngineTests.cs     — direcao/reverse/slippage/delay/filtro de simbolo/tamanho-zero
  CopyEngineHostTests.cs         — logica de copia do host contra sessao fake em memoria
  FakeTradingSession.cs          — IOpenApiTradingSession deterministico (registra ordens/fechamentos/emendas)
  OpenApiConnectionTests.cs      — conecta / reconecta / backoff / falta fatal (resiliencia)

tests/IntegrationTests/CopyLive/
  LiveCopySecrets.cs             — carrega os secrets gitignored, salva tokens atualizados
  LiveTokenBootstrapTests.cs     — one-shot: descriptografa tokens do DB do app no cache de tokens
  LiveCopyFixture.cs             — gira o token de acesso, expoe a lista de contas demo
  LiveCopyScenario.cs            — executa um cenario real de copia de ponta a ponta
  CopyTradingLiveTests.cs        — os cenarios live (1:1, 1:many, reverse, …)
```

## Secrets (locais, gitignored — nunca commitados)

Todos os creds em `<repo>/secrets/` (ja em `.gitignore`). Dev escreve **apenas os dois primeiros arquivos**; o terceiro (tokens) e auto-produzido pelo onboarding.

`secrets/openapi-test-app.local.json` — app Open API:

```json
{ "ClientId": "2175_…", "ClientSecret": "…" }
```

`secrets/openapi-cids.local.json` — credenciais de login cID para autorizar (um ou muitos):

```json
{ "Cids": [
  { "Cid": "amusleh",  "Username": "amusleh",  "Password": "…" },
  { "Cid": "afhacker", "Username": "afhacker", "Password": "…" }
] }
```

`secrets/openapi-tokens.local.json` — **escrito pelo onboarding**, multi-cID, atualizado cada execucao:

```json
{ "Cids": [
  { "Cid": "amusleh", "RefreshToken": "…", "AccessToken": "…", "IsLive": false,
    "Accounts": [ { "CtidTraderAccountId": 25172589, "TraderLogin": 3635817, "IsLive": false }, … ] }
] }
```

Refresh token **nunca expira**, entao apos onboarding unico testes live funcionam indefinidamente: cada execucao troca refresh token de cada cID por token de acesso fresco (rotacao) — sem navegador, sem prompts.

## Onboarding unico (completamente automatizado — sem interacao dev alem de salvar credenciais)

Onboarding dirige login real de cTrader ID em navegador headless a partir de credenciais cID salvas, captura OAuth callback em listener HTTPS local em redirect registrado do app (`https://localhost:7080/openapi/callback`), troca code por tokens, carrega lista de contas, escreve cache multi-cID. Execute uma vez por maquina (ou ao adicionar cID):

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

Autoriza cada cID em `openapi-cids.local.json`, escreve `openapi-tokens.local.json`. Apos isso testes de copia live nao precisam de mais nada. (A conta cID do cTrader ID do cID deve nao ter 2FA/captcha no login para automacao completar.)

**Bootstrap alternativo** (se contas ja autorizadas no app em execucao): descriptografa tokens armazenados direto do volume Postgres do app ao inves de re-autorizar:

```bash
docker run -d --name cmind-pg-extract -e POSTGRES_PASSWORD=appdev \
  -v app-pg-data:/var/lib/postgresql/data -p 5544:5432 postgres:17-alpine
CMIND_VOLUME_CONN="Host=127.0.0.1;Port=5544;Database=appdb;Username=postgres;Password=appdev" \
  dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveTokenBootstrapTests
docker rm -f cmind-pg-extract
```

## Seguranca — apenas demo

Testes live negociam **apenas contas demo**: fixture filtra cache de tokens para contas com `IsLive == false` e conecta ao gateway demo, entao ordem nunca pode pousar em conta live/financiada. Toda posicao que um teste abre e fechada no cleanup.

## Executando

```bash
# Testes de copia deterministicos apenas (rapidos, sem secrets, CI-safe)
dotnet test tests/UnitTests --filter FullyQualifiedName~CopyTrading

# Testes de copia live contra as contas demo reais (precisa dos dois arquivos de secrets)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests

# Tudo
dotnet test
```

Sem arquivos de secrets, testes live imprimem razao do skip + passam como no-ops, entao suite segura para executar em qualquer lugar.

## Cobertura

### Gerenciamento de dinheiro / dimensionamento (deterministico — `CopySizingCalculatorTests`)
FixedLot · LotMultiplier · NotionalMultiplier (tamanho do contrato / moeda) · ProportionalBalance ·
ProportionalEquity · ProportionalFreeMargin · AutoProportional · FixedRiskPercent · FixedLeverage ·
escala **para cima** e **para baixo** por mismatch de balanco/alavancagem/capacidade (a "regra de ouro") · arredondamento de passo de lote · min-lot skip vs forcar-min · max-lot cap · limite mais apertado de bound-vs-spec min & max · skip de master balance zero.

### Filtros de decisao (deterministico — `CopyDecisionEngineTests`)
Whitelist/prefer-list/blacklist de simbolo · LongOnly / ShortOnly · reverse inverte o lado efetivo ·
slippage sobre limite skip + exatamente-no-limite permitido · skip de sinal estale (max delay) · skip tamanho-zero ·
reconciliacao de reconexao (dedup de abertura faltante, fechamento de orfaos).

### Host do motor de copia (deterministico — `CopyEngineHostTests`, sessao em memoria)
Abertura espelha ordem de mercado (lado / volume / label) · **reverse** inverte lado e **troca SL/TP** ·
**mapeamento de simbolo** resolve o simbolo destino · **falha de ordem em um escravo ainda copia para os outros** ·
fechamento fonte fecha a copia espelhada · reconexao ressincroniza fecha copias orfas.

### Resiliencia de conexao (deterministico — `OpenApiConnectionTests`)
Chega a Connected apos auth do app · conexao caida reconecta e re-autentica · erro de auth fatal falha ·
backoff exponencial.

### Live, contas demo cTrader reais (`CopyTradingLiveTests`)
Atualizacao de token + listagem de contas · **1:1** copia executa · **1:many** copia espelha para todo escravo ·
**reverse** vira master buy em slave sell · **cross-cID** (mestre sob um cID espelha para escravo sob outro, cada autenticando com proprio token). Cada um abre posicao real min-lot no mestre, espera o motor espelhar (matched por source-position-id label no escravo), asserta, fecha tudo. Mercado fechado reportado **Inconclusivo**, nao falhando.

## Log e auditabilidade

Cada operacao de copy-trading logada via eventos estruturados gerados por fonte (`Core/Logging/LogMessages.cs`, IDs de evento 1043–1055), trilha completa auditavel:

| Evento | Id | Significado |
|-------|----|---------|
| CopyHostStarted | 1046 | motor de um perfil subiu (fonte + contagem de destinos) |
| CopySourceOpen | 1047 | mestre abriu posicao (simbolo / lado / lotes) |
| CopyOrderPlaced | 1048 | ordem de copia enviada a um escravo (simbolo / lado / volume / source id) |
| CopySkipped | 1049 | uma copia foi pulada e porque (slippage / direcao / filtro_de_simbolo / tamanho_zero / …) |
| CopyProtectionApplied | 1050 | SL/TP aplicado a uma copia escrava |
| CopyOpenFailed | 1051 | abertura de copia escrava falhou (isolada — outros escravos continuam) |
| CopySourceClose / CopyPositionClosed | 1052 / 1053 | mestre fechou → copia escrava fechada |
| CopyCloseFailed | 1054 | fechamento de copia escrava falhou |
| CopyResync | 1055 | reconciliacao de reconexao (contagem de aberturas fonte, orfaos fechados) |
| CopyPartialClose | 1056 | fechamento parcial mestre espelhado — fatia proporcional fechada em um escravo |
| CopyScaleIn | 1057 | scale-in mestre espelhado (opt-in) — volume adicionado copiado para escravo |
| CopyPendingOrderPlaced | 1058 | pendente limite/stop espelhada para escravo (opt-in) |
| CopyPendingOrderCancelled | 1059 | pendente fonte cancelada → pendente escrava cancelada |
| CopyTrailingApplied | 1060 | trailing stop aplicado a copia escrava (opt-in) |
| CopyStopLossAmended | 1061 | movimento de SL fonte re-emendou a copia escrava |
| CopyHostTokenRotated | 1062 | supervisor reiniciou um host em execucao apos rotacao de token de acesso |

Logs emitidos como JSON Serilog compacto (props estruturadas: `ProfileId`, `DestinationCtid`, `SourcePositionId`, `Symbol`, `Side`, `Volume`, …), exportados para OTLP quando `OTEL_EXPORTER_OTLP_ENDPOINT` definido. **Completamente configuravel** por categoria via config padrao — ex. aumentar/diminuir verbosidade do motor de copia sem tocar codigo:

```jsonc
// appsettings.json — Serilog level overrides
"Serilog": { "MinimumLevel": { "Override": {
  "CopyEngine": "Information",              // a trilha de auditoria do CopyEngineHost
  "Nodes.CopyTrading": "Information"        // supervisor / atualizacao de token
} } }
```

Teste `Audit_log_records_every_trading_operation` do host asserta que trilha dispara para abertura, ordem, protecao, fechamento.

## Casos extremos (validados contra como plataformas reais de copia/MAM falham)

Slippage & latencia, sufixo/mismatch de simbolo, negociacoes duplicadas na reconexao, mismatch de alavancagem & dimensionamento seguro de margem, diferencas de moeda de deposito/tamanho de contrato, lote min/max & arredondamento, ordens rejeitadas, filtros de direcao, limpeza de orfaos apos desconexao — tudo coberto acima. Fontes:
[leverage mismatch](https://copygram.app/blog/education/the-truth-about-leverage-mismatches-copying-high-leverage-low-leverage-accounts) ·
[cross-broker copying](https://www.mt4copier.com/cross-broker-trade-copying-efficient-forex-replication/) ·
[copier pitfalls](https://www.mt4copier.com/copy-trading-pitfalls-every-account-manager-must-avoid/) ·
[slippage & latency](https://copygram.app/blog/education/understanding-slippage-latency-copy-trading) ·
[why copy trading fails](https://xtsupport.zendesk.com/hc/en-us/articles/51566808595993-Why-Copy-Trading-Fails-Causes-Prevention-Guide) ·
[risk parameters](https://www.mt4copier.com/risk-parameters/).

## Cobertura de espelhamento avancado (partial close · ordens pendentes · SL-trailing)

Host espelha mais que abertura/fechamento de mercado. Cada comportamento = flag opt-in por destino em `CopyDestination` (`MirrorPartialClose` padrao on, `MirrorScaleIn`/`CopyPendingOrders`/`CopyTrailingStop` padrao off), guardado por metodos de intencao, jsonb-persisitido (migracao `CopyAdvancedMirroringAndNodeAffinity`).

| Comportamento | Teste deterministico (`CopyEngineHostTests`) | Teste live |
|-----------|--------------------------------------------|-----------|
| Fechamento parcial → fatia proporcional | `Partial_close_mirrors_a_proportional_slice_on_the_slave` (1.0→0.4 fecha 60%) + caminho desabilitado | `Partial_close_shrinks_the_slave_copy_proportionally` ✅ |
| Scale-in | `Scale_in_is_ignored_by_default_and_mirrored_when_enabled` | — |
| Pendente limite/stop colocada | `Pending_order_is_placed_on_the_slave_when_enabled` (Theory: Limit+Stop) + caminho desabilitado | `Pending_limit_order_is_mirrored_and_cancel_propagates` ✅ |
| Cancelamento pendente | `Source_pending_cancel_cancels_the_slave_pending` | (mesmo teste live — cancela no mestre, asserta escravo cancela) ✅ |
| Pendente fill sem double-open | `Filled_pending_does_not_double_open` (order-id → position-id dedupe) | — |
| Trailing stop | `Trailing_stop_is_applied_to_the_copy_when_enabled` | `Trailing_stop_is_mirrored_onto_the_slave_copy` ✅ |
| Movimento de SL fonte re-amenda | `Source_stop_loss_move_re_amends_the_copy` | — |
| Eventos de auditoria disparam | `Advanced_mirroring_audit_events_fire` (1056/1058/1059) | — |

Todos os testes live acima **verificados verdes contra contas demo cTrader reais** (1:1, 1:many, reverse, cross-cID, partial close, pending+cancel, trailing).

Adicoes de wire em `OpenApiTradingSession`: `SendPendingOrderAsync`, `CancelOrderAsync`, `ReconcilePendingOrdersAsync`, flag trailing em `AmendPositionSltpAsync`, campos de ordem/pendente em `ExecutionEvent`, `LoadSpotPriceAsync` (subscribe spot → bid/ask, usado por testes live pending/trailing para colocar ordens resting longe do mercado), `StopLoss`/`TrailingStopLoss` em `OpenPositionSnapshot` (estado observavel de trailing da copia via reconciliacao). Copias de destino permanecem rotuladas por **source position id** (copias pendentes por source **order id**) entao reconciliacao de reconexao permanece id-baseada, nunca duplica negociacao.

**Gotcha de evento cTrader (verificado live):** `ORDER_ACCEPTED`/`ORDER_CANCELLED` de ordem pendente resting carrega **placeholder `Position` nao-OPEN** alem da `Order`. Stream deve classificar como *evento de ordem* **antes** do branch de posicao (gated em posicao nao `OPEN`), senao placement pendente mal-interpreta como fechamento de posicao. `SourceExecutionsAsync` faz isso; missing it silently drops all pending mirroring.

## Rotacao de token + afinidade de no

- **Rotacao em hosts em execucao.** `CopyEngineSupervisor` registra assinatura de token em cada host em execucao e, a cada reconciliacao, reconstrói plano do DB (recem-girado por `OpenApiTokenRefreshService`). Assinatura alterada reinicia host (`CopyHostTokenRotated`, 1062); novo host's `ResyncAsync` reconstrói estado sem duplicar negociacoes. Forcar rotacao mid-run via `IOpenApiTokenClient.RefreshAsync` para verificar host live continua copiando.
- **Afinidade de no (sem double-copy).** Ambos Web no local e `CopyAgent` worker executam um supervisor. Cada perfil em execucao reivindicado por exatamente um no (`CopyProfile.AssignedNode`, `ExecuteUpdate` atomico claim keyed off `CopyOptions.NodeName`, nome padrao da maquina). Supervisor hospeda apenas perfis que possui; stop/pause libera claim. Cobertura:
  - Dominio (unidade): `AssignToNode_makes_profile_hosted_by_only_that_node`,
    `Stopping_a_profile_releases_its_node_assignment`, `NodeIdentity_rejects_blank`.
  - **Integracao (Postgres real, Testcontainers)**: `CopyNodeAffinityTests` dirige `ClaimUnassignedProfilesAsync` real do supervisor — asserta primeiro no reclama todos os 3 perfis em execucao, segundo reclama **0** (sem double-host), pause→restart libera claim para outro no.
  - Deteccao de rotacao (`TokenRotationSignatureTests`): `TokenSignature` do supervisor muda quando token fonte ou destino gira, estavel caso contrario (host em execucao reinicia somente em rotacao real).

### Refresh tokens de uso unico (importante)

cTrader **refresh tokens sao de uso unico** — cada atualizacao retorna *novo* refresh token, invalida o antigo. Fixture live atualiza no inicio, persiste token girado para `secrets/openapi-tokens.local.json`. Consequencias:
- Se run atualiza mas **nao pode persistir** novo token (ex. mount somente leitura), cache de token morta, proxima run falha `ACCESS_DENIED`. Regenere com onboarding headless:
  `CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`.
- `LiveCopySecrets.SaveTokens` engole falhas de escrita para que cache somente leitura nao crasha run, mas suite **live** in-cluster ainda precisa **cache gravavel** (K8s Job copia Secret em emptyDir — veja docs de deploy).

## Executando a suite em cluster Kubernetes

Toda suite executa in-cluster contra app deployado via Helm, entao regressao e pega in-cluster igual local. Veja [`docs/deployment/kubernetes.md`](../deployment/kubernetes.md#in-cluster-test-suite).

```bash
scripts/k8s-e2e.sh                                   # kind cluster, suite deterministica (sem secrets)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # live
```

`Dockerfile.tests` constroi imagem runner; `tests-job.yaml` do Helm (gated `tests.enabled=false`) executa contra Postgres + Web in-cluster. **Padrao = suite deterministica de copia** (sem secrets, sem tokens girando). Para suite live, defina `tests.copySecret` para Secret mantendo `openapi-*.local.json` gitignored; init-container copia para **emptyDir gravavel** em `/app/secrets` (necessario — refresh tokens de uso unico devem ser persistiveis). Testes de copia precisam apenas Web + Postgres + cache de token — sem agentes de nos privilegiados. Script asserta Job exit 0 e logs contem `Passed!`.

**Verificado aqui (Docker, sem cluster):** imagem de teste executa suite deterministica (`101 passou`) e, com mount `secrets/` gravavel, suite **live** completa (`8 passou`) — caminho exato do Job menos Kubernetes. `kind`/`kubectl`/`helm` indisponiveis em ambiente de autoria, entao execucao completa de `k8s-e2e.sh` em cluster e o unico passo nao executado aqui.

## Suite live de matriz + caos (LiveCopyMatrix / LiveCopyChaos)

Duas suites live data-driven construidas sobre `LiveCopyScenario` / `LiveCopyFixture`, contra-parte live da suite DST de stress:

- **`LiveCopyMatrix`** — `[Theory]`/`[MemberData]` matriz de opcoes: uma abertura real de mestre por linha contra contas demo, cada uma com destino configurado diferentemente, assertando resultado dourado. Linhas: `one_to_one`, `half_multiplier`, `reverse` (lado oposto), `manage_only` (nao abre nada), `trading_hours_closed` (janela exclui agora → sem copia), `source_label_block` (filtro de label → sem copia), `lot_sanity_block` (teto → sem copia).
- **`LiveCopyChaos`** — motor de copia contra host hostil no inicio: mestre ja detem posicao antes do host iniciar, entao convergencia pode vir apenas de resync de inicio (`Sync-Open-on-Start`), mais caso negativo com toggle off. Caos deterministico de socket-flap / rotacao de token / rejeicao coberto exaustivamente pela suite DST; isso asserta o caminho live de resync.

Ambos **pulam limpamente** sem creds, reportam **Inconclusivo** em mercado fechado, cada linha limpa posicoes que abriu (apenas contas demo).

**Robustez da fixture:** `LiveCopyFixture` agora degrada para skip limpo quando tokens em cache nao podem ser atualizados (refresh tokens cTrader de uso unico — cache estagnada ou run offline ja nao falha toda a colecao live; re-executar OAuth onboarding para atualizar cache).

## Executando as suites live + cluster voce mesmo (SEM desculpas — tudo e auto-servico)

Tudo abaixo disponivel neste ambiente. **Nao** afirme "creds indisponiveis", "token estagnado" ou "kind/helm nao instalado" — configure e execute.

### 1. Obtenha um token Open API fresco (OAuth headless, sem interacao com navegador)

Refresh tokens cTrader sao de uso unico, entao token em cache VAI estagnar. Re-minte a partir de username/password cID salvos (`secrets/openapi-cids.local.json` + `secrets/openapi-test-app.local.json`, ou unificado `secrets/dev-credentials.local.json`). Teste de onboarding dirige OAuth headless via Playwright, escreve `secrets/openapi-tokens.local.json`:

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

~13s; autoriza cada cID, armazena tokens frescos. Re-execute quando suite live报告 fixture indisponivel devido a falha de refresh.

### 2. Execute as suites de copia live (contas demo cTrader reais)

```bash
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests   # espelhamento central (8)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyMatrix          # matriz de opcoes (7)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyChaos           # caos de resync (2)
```

Coloca + limpa ordens DEMO reais (nunca contas live), reporta **Inconclusivo** em mercado fechado. Verificado verde de ponta a ponta.

### 3. Bootstrap tokens de um volume de app em execucao (alternativo)

Se app executando + cID vinculado no app, extraia refresh token mais recente direto do volume `app-pg-data` Postgres ao inves de re-autorizar — veja `LiveTokenTokenBootstrapTests`, defina `CMIND_VOLUME_CONN`.

### 4. E2E de cluster Kubernetes

`kind`, `helm`, Docker disponiveis (instale kind/helm via `go install`/binarios de release ou `choco install kind kubernetes-helm` se nao no PATH). Script one-shot constroi+carrega imagens, faz deploy do chart, executa Job de teste in-cluster, asserta exit 0:

```bash
scripts/k8s-e2e.sh                                 # suite deterministica de copia (sem secrets)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh   # live in-cluster
```

Veja [../deployment/kubernetes.md](../deployment/kubernetes.md).
