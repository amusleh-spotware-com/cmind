---
description: "Construa, execute e faca backtest de cBots cTrader (C# e Python, ambos .NET) a partir do Monaco IDE no navegador, executados na imagem oficial ghcr.io/spotware/ctrader-console."
---

# Construir e fazer backtest de cBots

Construa, execute e faca backtest de cTrader cBots (C# **e** Python, ambos .NET) a partir do Monaco
IDE no navegador, executados na imagem oficial `ghcr.io/spotware/ctrader-console`.

## Construcao

- Pagina **Builder** hospeda editor Monaco; `CBotBuilder` compila projeto com
  `dotnet build` **em container descartavel** (`AppOptions.BuildImage`, work dir bind-mount
  em `/work`), para que targets MSBuild nao confiaveis nao alcancem o host. Restauracao NuGet em cache
  entre construcoes via volume compartilhado. Host web precisa de acesso ao socket Docker.
- Templates iniciais C# + Python vivem em `src/Nodes/Builder/Templates/`.

## Execucao e backtest

- **Instancias** = hierarquia de estado TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Transicao substitui entidade (mudanca de id),
  id do container transportado.
- `NodeScheduler` escolhe no com menor carga elegivel; `ContainerDispatcherFactory` roteia para
  agente HTTP de no remoto ou despachante Docker local.
- Pollers de conclusao reconciliam containers encerrados (containers de backtest auto-encerram via
  `--exit-on-stop`); relatorio presente → concluido (armazena `ReportJson`), ausente → falhou.
- Logs de container ao vivo transmitem para navegador sobre SignalR; curvas de equity de backtest parseadas do
  relatorio + renderizadas em grafico.

## Notas do CLI do cTrader Console

Backtests precisam de `--data-mode` (padrao `m1`), datas como `dd/MM/yyyy HH:mm`, e
`params.cbotset` JSON arg posicional; `run` rejeita `--data-dir` (somente backtest). Veja
`ContainerCommandHelpers`.

## Nos e escala

Capacidade de execucao escala adicionando agentes de nos (auto-registro + heartbeat). Veja
[descoberta de nos](../operations/node-discovery.md) e [escala](../deployment/scaling.md).

## Uma conta de trading e necessaria

Executar ou fazer backtest de um cBot precisa de uma conta de trading cTrader para conectar. Ate que voce adicione uma em
**Contas de trading**, os botoes **Executar Novo cBot** / **Fazer Backtest de Novo cBot** estao desabilitados (com um
tooltip) e a pagina mostra um aviso linkando para configuracao de conta — voce nao encontra mais um
erro `stream connect failed` bruto de um bot sem conta.
