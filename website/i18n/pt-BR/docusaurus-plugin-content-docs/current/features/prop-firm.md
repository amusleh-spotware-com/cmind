---
description: "Firms prop de varejo (estilo FTMO) vendem contas de avaliacao: trader deve atingir meta de lucro enquanto permanece dentro de limites de risco (maxima perda diaria, maximo drawdown total/rastreador, consistencia, limites de tempo) antes de ser financiado. cMind permite que usuario crie desafios personalizados de qualquer formato da industria, vincule a TradingAccount, execute como operacao de copy-trading — iniciado/parado, hospedado em no, rastreado ao vivo sobre cTrader Open API."
---

# Simulacao de desafio prop-firm

Firms prop de varejo (estilo FTMO) vendem contas de **avaliacao**: trader deve atingir meta de lucro enquanto
permanece dentro de limites de risco (maxima perda diaria, maximo drawdown total/rastreador, consistencia, limites de tempo) antes de
ser financiado. cMind permite que usuario crie **desafio personalizado de qualquer formato da industria**, vincule a
`TradingAccount`, **execute como operacao de copy-trading** — iniciado/parado, hospedado em no,
rastreado **ao vivo sobre cTrader Open API**. Agregado avalia cada regra deterministicamente; em
aprovacao ou violacao, encerra desafio, marca, alerta usuario.

## Dominio (contexto limitado: PropFirm)

`PropFirmChallenge` = raiz de agregado (modulo `Core.PropFirm`), referencia sua `TradingAccount` por
id forte apenas (sem FK cross-agregado). Proprietario da avaliacao de regras, maquina de estado de fase, lease de no.

### Objetos de valor e conjunto de regras

- **`Money`** (nao negativo), **`MoneyAmount`** (signed), **`Percent`** (0-100], **`TradingDayRequirement`** (0-365).
- **`EquitySnapshot`** `(equity, balance)` — leitura alimentada ao agregado.
- **`ActivitySnapshot`** `(openPositions, openedInNewsWindow, holdingOverWeekend)` — fatos nao-equity.
- **`DailyLossLimit`** `(percent, basis)` — basis `Equity` (intraday, inclui P&L flutuante) ou `Balance`
  (realizado apenas).
- **`DrawdownLimit`** — `Static` (do saldo inicial), `TrailingPercent` (do pico de equity), ou
  `TrailingThresholdDollar` (rastreia pico de equity por valor fixo em dolar, entao **trava no saldo inicial**
  quando equity atinge o limiar — estilo futuros).
- **`ConsistencyRule`** `(maxSingleDayShareOfProfit)` — bloqueia aprovacao enquanto um dia domina o lucro total.
- **`ChallengeRules`** carrega acima mais `MaxCalendarDays`, `MaxInactivityDays`, `MaxOpenPositions`,
  `AllowWeekendHolding`, `AllowNewsTrading`, `Kind`, `SingleStep`. Matematica de regras vive em VOs
  (`DrawdownLimit.IsBreached`, `DailyLossLimit.IsBreached`, `ConsistencyRule.IsSatisfied`); agregado
  orquestra.

### Tipos e templates de desafio

`ChallengeTemplates.For(kind)` constroi preset valido para `OnePhase`, `TwoPhase`, `ThreePhase`,
`InstantFunding`, ou `Custom` (controle total). UI pre-preenche template; usuario pode ajustar qualquer campo.

### Fases e status

- **Fases:** `Evaluation → Verification → Funded` (single-step pula Verification).
- **Status:** `Active`, `Passed`, `Failed`, mais ciclo de vida `Stopped` (rastreamento pausado) — `Create` inicia
  desafio `Active`; `Stop()`/`Resume()` alternam `Active↔Stopped`.
- **`BreachReason`:** `DailyLoss`, `MaxDrawdown`, `Consistency`, `TimeLimit`, `Inactivity`,
  `WeekendHolding`, `NewsTrading`, `MaxExposure`.

### Avaliacao de regras

- **`RecordEquity(EquitySnapshot, now)`** — rola dia de trading nos limites do dia (captura lucro
  do dia anterior para regra de consistencia), atualiza picos/diarios, entao **falha na primeira violacao**
  (perda diaria → drawdown → limite de tempo → inatividade, em ordem) ou avanca fase quando meta de lucro,
  minimo de dias de trading, requisitos de consistencia todos cumpridos. Snapshots fora de ordem e registros em
  desafio terminal lancam `DomainException`.
- **`RecordActivity(ActivitySnapshot, now)`** — avalia regras de comportamento (max posicoes abertas, manter
  fim de semana, trading em noticias), carimba atividade para regra de inatividade.
- **`PropFirmDrawdownWarning`** suave dispara uma vez quando uso de equity cruza limiar configuravel.

Eventos de dominio: `PropFirmChallengeStarted`, `PropFirmChallengeStopped`, `PropFirmPhasePassed`,
`PropFirmChallengePassed`, `PropFirmChallengeBreached`, `PropFirmDrawdownWarning`.

## Rastreamento live (Execucao) — hospedado em no, auto-cicatrizante

Rastreamento espelha pilha de hospedagem de copy-trading exatamente; tracker prop = **primo somente leitura** do
motor de copia.

- **`PropFirmTrackingSupervisor`** (`src/Nodes/PropFirm`) — `BackgroundService` em cada no, controlado por
  `App:PropFirm:Enabled`. Cada ciclo **reivindica** desafios ativos em lease
  auto-cicatrizante (`AssignedNode` + `LeaseExpiresAt`; desafios de no morto recuperados quando lease expira —
  mesmo `ExecuteUpdate` atomico claim de copy trading, entao dois nos nunca rastreiam duplicado), renova leases,
  empurra tokens girados no local, para hosts cujo desafio saiu `Active`.
- **`PropFirmTrackingHost`** (`src/Nodes/PropFirm`) — um por desafio. Abre `IOpenApiTradingSession`
  para conta e, em `App:PropFirm:EquityPollInterval`, recomputa equity live, alimenta ao
  agregado. Troca access token no local na rotacao (sem drop de sessao). Sai quando desafio
  nao mais `Active`.
- **`PropFirmEquityCalculator`** (`src/CTraderOpenApi/Client`) — matematica de equity fiel a cTrader.
  Equity **nao** entregue pela Open API, entao derivada: `equity = balance + Σ(P&L unrealized)`,
  onde P&L de cada posicao e `diferencaDePreco × unidades × taxa quote→deposito + swap + commission`
  (`unidades = volume wire / 100`; long reavalia em bid, short em ask). Balance de
  `ProtoOATrader`; posicoes (preco de entrada, swap, commission) de reconciliacao; bid/ask live de
  assinaturas spot. Puro e isolado — ponto quente de conversao de moeda unit-testado em si.

## Alertas

`PropFirmAlertNotifier` (`src/Infrastructure/PropFirm`) assina eventos de dominio de aprovacao/violacaoaviso
(registrados como `IDomainEventHandler<>`, despachados apos `SaveChanges` bem-sucedido), notifica usuario
atraves de trilha de alerta/auditoria estruturada (`LogMessages`). UI live reflete mesma mudanca de status. Isto
= reacao cross-contexto — nunca muta agregado de desafio.

## API (`/api/prop-firm`, funcionalidade `PropFirm`, papel User+)

| Metodo | Rota | Proposito |
|--------|-------|---------|
| GET | `/challenges` | lista desafios do usuario (tipo, fase, status, equity live, lease) |
| GET | `/challenges/{id}` | um desafio |
| GET | `/templates` | presets de industria para dialogo de criacao |
| POST | `/challenges` | cria de template **ou** conjunto de regras totalmente customizado |
| POST | `/challenges/{id}/start` | retomar rastreamento (Stopped → Active) |
| POST | `/challenges/{id}/stop` | parar rastreamento (Active → Stopped, libera lease) |
| POST | `/challenges/{id}/equity` | registra snapshot de equity → re-avalia (caminho manual/sem feed live) |
| DELETE | `/challenges/{id}` | soft-delete (bloqueado enquanto Active) |

MCP: `Mcp/Tools/PropFirmTools.cs` expoe list/create(from template)/record-equity/start/stop, controlado por
funcionalidade `PropFirm`.

UI: `/prop-firm` (nav *Prop Firm*, controlada por flag `PropFirm`) lista desafios com acoes de linha **Start/Stop/Delete**
(Start quando Stopped, Stop quando Active, Delete desabilitado enquanto Active), cria atraves de
`NewPropFirmChallengeDialog` (seletor de template + editor de regras completo). Toda criacao/edicao via dialogo MudBlazor.

## Feed de equity live — resolvido

Gap anterior de "sem feed P&L de conta live" fechado: quando `App:PropFirm:Enabled` definido, nos rastreiam
conta live sobre Open API, alimentam equity automaticamente. Sem ele (padrao), dominio e
**caminho de equity manual** (`POST …/equity`) executam inalterados — nenhum credencial cTrader necessario para build/test/E2E.

## Testes

- **Unidade** — `UnitTests/PropFirm/`: `PropFirmChallengeTests` (avanco de fase, min-dias, drawdown estatico/trailing,
  perda diaria, guardas de terminal/fora-de-ordem); `PropFirmChallengeRulesTests` (basis de perda diaria balance vs equity, trail+lock de trailing-threshold-dollar, bloco/permite consistencia, limite de tempo, inatividade,
  max-exposure, fim de semana, noticias, stop/resume, limite de lease, aprovacao libera lease, aviso de drawdown);
  `PropFirmValueObjectTests` (intervalos de VO + matematica de regra-VO); `PropFirmEquityCalculatorTests` (P&L long/short,
  swap/commission, conversao quote→deposito, precificacao faltante); `PropFirmTrackingHostTests` (equity live
  guia aprovacao/falha contra sessao fake estendida); `PropFirmAlertNotifierTests`. Tempo explicito /
  `FakeTimeProvider` — sem leituras de wall-clock.
- **Integracao** — `IntegrationTests/`: `PropFirmChallengePersistenceTests` (round-trip + record-equity +
  soft-delete, regras enriquecidas + lease round-trip) e `PropFirmTrackingLeaseTests` (claim, lease contestado,
  reclaim apos expiracao entre duas identidades de no) em Postgres real.
- **E2E** — `E2ETests/PropFirmTests.cs`: cria + record-equity para `Passed`; fluxo stop→start→breach;
  endpoints de templates.
- **Stress / DST** — `StressTests/PropFirm/PropFirmChallengeDstTests.cs`: streams de equity/atividade
  randomizados com semente (rolos de dia, picos, crashes, snapshots duplicados + fora de ordem, exposure/fim de semana/noticias) atraves de muitos desafios de regras mistas, assertando estados terminais estaveis exatamente-uma-vez, invariante pico-limite-atual,
  falhas justificadas.

## Configuracao (`App:PropFirm`)

`Enabled` (off por padrao), `ReconcileInterval`, `EquityPollInterval`, `LeaseTtl`,
`DrawdownWarnThresholdPercent`, `NodeName`.
