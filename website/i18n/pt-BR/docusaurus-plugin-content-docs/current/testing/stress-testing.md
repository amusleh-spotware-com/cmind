---
description: "Suite de stress. Martela partes do app cuja falha custa dinheiro aos usuarios — principalmente copy trading — com cargas de trabalho hostis, randomizadas e com injeccao de falhas. Asserta sistema…"
---

# Teste de stress

Suite de stress. Martela partes do app cuja falha custa dinheiro aos usuarios — principalmente **copy trading** — com cargas de trabalho hostis, randomizadas e com injeccao de falhas. Asserta que o sistema permanece correto. Vive em `tests/StressTests`, executa no gate verde normal de `dotnet test`.

## Abordagem — Teste de Simulacao Determinista (DST)

A melhor forma de stress de sistemas financeiros distribuidos e **teste de simulacao determinista**, segundo TigerBeetle, FoundationDB, Antithesis: executa logica real contra um *mundo simulado*, dirige com carga de trabalho **aleatoria com semente** + falhas injetadas, asserta invariantes em quiescencia. Tudo com semente + deterministico → qualquer falha reproduz exata da semente. Combinado com:

- **Injeccao de falhas estilo chaos engineering** (estilo Netflix Chaos Monkey) — quedas de conexao, rejeicoes de ordem, rotacao de token, morte de no.
- **Invariantes baseadas em propriedade** — nao asserta sequencias exatas de chamadas; asserta propriedades que devem manter nao importando como eventos se entrelaçam (convergencia, sem orfaos, no-maximo-um lease holder).

O app ja envia um modelo de mundo DST perfeito: `FakeTradingSession`, sessao Open API em memoria fiel a cTrader. Suite de stress o reutiliza (linked, unica fonte de verdade) nao mock, entao corretora simulada se comporta como a real.

## O que cobre

### Copy trading (foco principal)

Dirigido via `CopyDstWorld` (`tests/StressTests/CopyTrading/`), executa `CopyEngineHost` real contra sessao fake, emite carga de trabalho de fonte consistente com associacao:

| Cenario | Testa |
|---|---|
| `Mass_fan_out…` | 1 fonte → 80 destinos, 150 aberturas depois fechamentos; fan-out completo + drain |
| `High_frequency_open_close…` | 300 aberturas/fechamentos rapidos entrelacados; sem posicoes vazadas |
| `Partial_close_and_scale_in_storm…` | churn de fechamento parcial + scale-in; estabilidade do conjunto de labels |
| `Connection_flap_storm…` | desconexao/reconexao repetida de soquete + desync em voo; convergencia de resync |
| `Order_rejection_cascade…` | subconjunto rejeita cada ordem; destinos saudaveis unaffected, entao auto-curam via resync |
| `Token_rotation_storm…` | trocas rapidas de token no local durante tempestade de ordens |
| `Randomized_chaos_workload…` (10 sementes) | **nucleo DST** — todo tipo de evento + toda falha entrelacados imprevisivelmente |
| `CopyLeaseReclaimStressTests` | morte de no + reclaim de lease em cluster escalado (dominio puro, `FakeTimeProvider`) |

**Invariante de convergencia.** Em repouso, cada destino saudavel espelha exatamente o conjunto de posicoes fonte ainda abertas — sem orfaos, nenhum faltando. Assertada no *conjunto* de labels (scale-in legitimamente abre segunda posicao destino sob mesmo label fonte, entao labels duplicados esperados). Destino atualmente rejeitando ordens pode ficar atrasado, reconciliado uma vez curado.

**Invariante de lease.** Em cluster onde nos morrem + revivem em agenda com semente, no maximo um no detem lease valido em um perfil; lease de no morto expira exato no vencimento, e reclamado; cluster saudavel estabiliza com todo perfil detido por exatamente um no. Espelha o predicado de claim do `CopyEngineSupervisor` contra metodos de lease de dominio do `CopyProfile`.

### Thread-safety do harness

`FakeTradingSession` single-threaded; carga de stress muta do thread de teste enquanto host le/escreve de seu loop. `SyncTradingSession` embrulha, torna cada operacao de sessao atomica em um gate (sem segurar gate atraves de callback de reconexao — invertia ordem de lock vs `_stateGate` do host e causaria deadlock). Simulador em si intocado.

## Bugs encontrados

- **Corrida de resync de inicializacao em `CopyEngineHost`.** `OnReconnected` conectado antes de carga de referencia inicial + primeiro resync, que rodava sem `_stateGate`. Soquete flap durante inicializacao rodava segundo resync concorrent, corrompia dicionarios de estado nao-concorrentes do host (`_symbolDetails`, `_sourceVolumes`). Corrigido: carga de inicializacao + primeiro resync agora executam sob gate. Corrida de producao, nao artefato de teste — carga de trabalho DST chaos a superficializou.

## Executando

```bash
dotnet test tests/StressTests/StressTests.csproj
```

Suite **serializada** (`[assembly: CollectionBehavior(DisableTestParallelization = true)]`): cada teste gira loop de host background, conduz a quiescencia sob wall clock, entao run paralelo passa fome em tasks de host e torna timeouts de convergencia flakys. Cargas de trabalho dimensionadas para terminar em segundos entao suite permanece no gate verde padrao. Falha imprime sua semente; re-executa aquela semente para reproduzir entrelaecimento exato.

## Extendendo

- Novo comportamento de copia → adicione op fonte a `CopyDstWorld` (mantenha associacao do livro fonte consistente com stream de eventos) + caso ponderado em `CopyChaosDstTests`. Se pode criar ou aposentar uma posicao destino, garanta que invariante de convergencia ainda mantem.
- Nova falha → adicione injetor a `CopyDstWorld` (delega a superfice de controle de `FakeTradingSession` via `SyncTradingSession`) + exercite em cenario nomeado mais mix de caos.
- Mantenha simulador fiel a cTrader (veja mandato `CLAUDE.md` raiz); nunca enfraqueça para fazer um teste de stress passar.
