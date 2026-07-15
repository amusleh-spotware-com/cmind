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

## Executar a partir do editor de código

Clicar em **Executar** no editor de código abre uma caixa de diálogo em vez de disparar uma execução cega e fixa:

- **Conta de trading** (obrigatória) — a conta cTrader à qual o cBot se conecta.
- **Conjunto de parâmetros** (opcional) — escolha um conjunto existente ou deixe vazio para executar com os **valores de parâmetros padrão** do cBot. Um botão **+** ao lado do seletor cria um novo conjunto de parâmetros inline (veja abaixo) e o seleciona.
- **Símbolo / Timeframe** são por padrão `EURUSD` / `h1` e podem ser alterados; **Cancelar** ou **Executar**.

Ao **Executar**, o editor salva e compila o código-fonte atual, inicia a instância na conta escolhida com os parâmetros escolhidos e então acompanha os logs do contêiner ao vivo. (O fluxo de logs encaminha o cookie de autenticação do usuário conectado ao hub SignalR `/hubs/logs`, de modo que ele se conecta em vez de falhar com `Invalid negotiation response received`.)

## Conjuntos de parâmetros

Um **conjunto de parâmetros** é um conjunto nomeado e reutilizável de substituições de parâmetros do cBot, armazenado como um objeto JSON plano que mapeia cada nome de parâmetro para um valor escalar, por ex. `{"Period": 14, "Label": "trend"}`. No momento da execução/backtest, ele é convertido no arquivo `params.cbotset` do cTrader (`{ "Parameters": { … } }`). Você pode criar/editar um conjunto como JSON bruto na caixa de diálogo **Conjuntos de parâmetros** do cBot ou inline na caixa de diálogo Executar.

O JSON é **validado** ao salvar: deve ser um único objeto plano cujos valores sejam todos escalares (string / número / bool). Uma raiz não-objeto, um array, um objeto aninhado, um valor `null` ou JSON malformado é rejeitado (erro claro na caixa de diálogo, `400 Bad Request` na API). Um objeto vazio `{}` é permitido e significa "sem substituições".
