---
description: "Construir, executar, fazer backtest de cBots cTrader (C# e Python, ambos .NET) do editor Monaco in-browser, executar na imagem oficial ghcr.io/spotware/ctrader-console."
---

# Build & backtest cBots

Construir, executar, fazer backtest de cBots cTrader (C# **e** Python, ambos .NET) do editor Monaco
in-browser, executar na imagem oficial `ghcr.io/spotware/ctrader-console`.

## Build

- A página **Builder** hospeda o editor Monaco; `CBotBuilder` compila o projeto com
  `dotnet build` **em um container descartável** (`AppOptions.BuildImage`, diretório de trabalho bind-mounted
  em `/work`), para que destinos MSBuild de usuários não confiáveis não alcancem o host. Restauração de NuGet em cache
  entre compilações via volume compartilhado. O host web precisa de acesso ao Docker socket.
- Modelos iniciais de C# + Python vivem em `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = hierarquia de estado TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). A transição substitui a entidade (mudança de id),
  container id é mantido.
- `NodeScheduler` seleciona o nó elegível menos carregado; `ContainerDispatcherFactory` roteia para
  agente HTTP de nó remoto ou despachador Docker local.
- Pollers de conclusão reconciliam containers exited (containers de backtest auto-exit via
  `--exit-on-stop`); relatório presente → completo (armazenar `ReportJson`), ausente → falha.
- Logs de container em tempo real fazem stream para o navegador sobre SignalR; curvas de capital de backtest analisadas do
  relatório + gráficos.

## Backtest market data is cached per account

O cTrader Console baixa dados históricos de tick/bar em seu `--data-dir`. Esse diretório é um
**cache estável e persistente indexado pela conta de negociação** (seu número de conta) — bind-mounted do
disco do nó em seu próprio caminho de container (`/mnt/data`), um **mount separado e não aninhado** do
diretório de trabalho por instância. Portanto, cada backtest na mesma conta **reutiliza** os dados já baixados
em vez de baixá-los novamente a cada execução. (Anteriormente o
diretório de dados residia sob o diretório de trabalho por instância, cujo id muda a cada execução, o que forçava um novo
download a cada backtest.) O diretório de trabalho por instância efêmero ainda contém o algo, parâmetros, senha
e relatório; o cache de dados compartilhado é contabilizado no uso de dados de backtest de um nó e limpo pela
ação de limpeza de nó.

## Backtest settings

O diálogo **Backtest** expõe cada configuração que a CLI de backtest do cTrader Console aceita, para que você nunca
tenha que tocar em uma linha de comando:

- **From / To** — a janela de backtest (`--start` / `--end`).
- **Data mode** — um dos três modos cTrader (`--data-mode`): **Tick data** (`tick`, preciso),
  **m1 bars** (`m1`, rápido), ou **Open prices only** (`open`, mais rápido).
- **Starting balance** — padrão para `10000` (`--balance`). Um **saldo 0 não coloca trades e faz
  cTrader emitir um relatório vazio que depois falha** ("Message expected"), portanto um saldo diferente de zero é
  sempre enviado.
- **Commission** e **Spread** — `--commission` / `--spread` (spread em pips).
- **Data file** (opcional) — um caminho lado do nó para um arquivo de dados históricos (`--data-file`); deixar vazio para
  usar os dados baixados/em cache.
- **Expose environment variables** — um toggle que passa as variáveis de ambiente do host para o cBot
  (o flag `--environment-variables`).

## Instance detail page

Abrir uma instância (`/instance/{id}`) mostra seu status em tempo real, logs e — para um backtest — a curva de capital.
O **título da guia do navegador** reflete a instância específica (**nome do cBot · tipo · símbolo**, por ex.
`TrendBot · Backtest · EURUSD`) para que uma guia de execução em tempo real e uma guia de backtest sejam distinguíveis à primeira vista.
Uma execução e um backtest do mesmo cBot são rastreados como **linhagens** distintas (um id de linhagem estável mantido
entre transições de estado), portanto a página segue exatamente uma instância e nunca mistura dados de uma execução com dados de um
backtest.

## Instance lifecycle controls

Cada linha de instância (e sua página de detalhe) tem controles corretos de estado. Uma instância **ativa** mostra
**Stop**; uma **terminal** (Stopped / Completed / Failed) mostra **Start (▶)** para relançá-la com
o mesmo cBot, conta, símbolo, timeframe, conjunto de parâmetros e imagem (uma execução reinicia como uma execução, um
backtest como um backtest). Clicar em Stop mostra um aviso "Stopping…" e desativa o ícone até ser
resolvido, e uma execução recém-criada aparece na lista imediatamente — sem recarga de página.

Os logs de console são **persistidos quando uma instância termina** — para uma execução (ao parar) e para um
**backtest** (ao concluir) — portanto, os logs da última execução permanecem visualizáveis na página de detalhe e,
via barra de ferramentas de logs, **copiados para a área de transferência** (ícone Copiar logs) ou **baixados** (ícone Download de logs)
até mesmo após o container ter desaparecido. Ambos atuam no log completo do console da instância, não apenas a
cauda na tela.

Um `.algo` **carregado** nunca foi construído aqui, portanto sua coluna **Last Build** na página cBots é deixada
em branco (mostra um tempo de build apenas para cBots que você compila no navegador).

## Edit & re-run a stopped instance

Uma instância **parada** (execução ou backtest) tem um controle **Edit** — um ícone em sua linha na lista **e**
ao lado de Start/Stop em sua página de detalhe — que abre um diálogo **pré-preenchido** com sua configuração atual.
Você pode mudar a **conta de negociação, símbolo, timeframe, conjunto de parâmetros e tag de imagem** (e, para um
backtest, a **janela e todas as configurações de backtest** acima), depois **Save & start** a relança com as
novas configurações (substituindo a instância parada). O controle é **desativado enquanto a instância está ativa** —
apenas uma instância parada pode ser editada.

## Run from the code editor

Clicar em **Run** no editor de código abre um diálogo em vez de disparar uma execução cega e codificada:

- **Trading account** (obrigatório) — a conta cTrader à qual o cBot se conecta.
- **Parameter set** (opcional) — escolha um conjunto existente, ou deixe vazio para executar com os **valores padrão de parâmetros** do cBot.
  Um botão **+** próximo ao seletor cria um novo conjunto de parâmetros
  inline (veja abaixo) e o seleciona.
- **Symbol / Timeframe** padrão para `EURUSD` / `h1` e podem ser alterados; **Cancel** ou **Run**.

Em **Run** o editor salva + compila a fonte atual, inicia a instância na conta escolhida
com os parâmetros escolhidos, e depois cauda os logs do container em tempo real. (O stream de logs encaminha o cookie de autenticação
do usuário conectado ao hub SignalR `/hubs/logs`, portanto se conecta em vez de falhar com
`Invalid negotiation response received`.)

## Parameter sets

Um **parameter set** é um conjunto nomeado e reutilizável de sobrescrita de parâmetros de cBot armazenado como um objeto JSON plano
mapeando cada nome de parâmetro para um valor escalar, por ex. `{"Period": 14, "Label": "trend"}`. Na
hora da execução/backtest é transformado no arquivo `params.cbotset` do cTrader
(`{ "Parameters": { … } }`). Você pode criar/editar um conjunto como JSON bruto do diálogo **Parameter
sets** do cBot ou inline do diálogo Run.

Cada conjunto de parâmetros **pertence a um cBot**: o diálogo New Parameter Set lista todos os seus cBots e você
**deve escolher um** — a criação é bloqueada até que um cBot seja selecionado. O **nome de um conjunto é único por cBot**:
criar ou renomear um conjunto para um nome que outro conjunto do mesmo cBot já usa é rejeitado (um erro
claro no diálogo, `409 Conflict` na API). O mesmo nome pode ser reutilizado em um **cBot diferente**.

O JSON é **validado** ao salvar: deve ser um único objeto plano cujos valores são todos escalares
(string / number / bool). Uma raiz não-objeto, um array, um objeto aninhado, um valor `null`, ou JSON mal formado
é rejeitado (um erro claro no diálogo, `400 Bad Request` na API). Um objeto vazio `{}`
é permitido e significa "sem sobrescritas".

## cTrader Console CLI notes

Backtests precisam `--data-mode` (padrão `m1`), datas como `dd/MM/yyyy HH:mm`, e
JSON de argumento posicional `params.cbotset`; `run` rejeita `--data-dir` (somente backtest). Veja
`ContainerCommandHelpers`.

## Nodes & scale

A capacidade de execução escala adicionando agentes de nó (auto-register + heartbeat). Veja
[node discovery](../operations/node-discovery.md) e [scaling](../deployment/scaling.md).

## A trading account is required

Executar ou fazer backtest de um cBot precisa de uma conta de negociação cTrader para se conectar. Até adicionar uma sob
**Trading accounts**, os botões **Run New cBot** / **Backtest New cBot** são desativados (com um
tooltip) e a página mostra um prompt vinculando à configuração de conta — você não bate mais em um erro bruto
`stream connect failed` de um bot sem conta.
