---
description: "Construir, executar, fazer backtest de cBots cTrader (C# e Python, ambos .NET) a partir do Monaco IDE integrado no navegador, executar na imagem oficial ghcr.io/spotware/ctrader-console."
---

# Construir & fazer backtest de cBots

Construir, executar, fazer backtest de cBots cTrader (C# **e** Python, ambos .NET) a partir do Monaco
IDE integrado no navegador, executar na imagem oficial `ghcr.io/spotware/ctrader-console`.

## Construir

- Página **Builder** hospeda o editor Monaco; `CBotBuilder` compila o projeto com
  `dotnet build` **em container descartável** (`AppOptions.BuildImage`, diretório de trabalho vinculado
  em `/work`), para que destinos MSBuild de usuários não confiáveis não alcancem o host. Restauração
  do NuGet é armazenada em cache entre builds através de volume compartilhado. Host da web precisa de
  acesso ao socket Docker.
- Templates iniciais C# + Python vivem em `src/Nodes/Builder/Templates/`.

## Executar & fazer backtest

- **Instances** = hierarquia de estado TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Transição substitui entidade (mudança de id),
  container id é mantido.
- `NodeScheduler` escolhe nó elegível menos carregado; `ContainerDispatcherFactory` roteia para
  agente HTTP de nó remoto ou despachante Docker local.
- Pollers de conclusão reconciliam containers encerrados (containers de backtest saem automaticamente via
  `--exit-on-stop`); relatório presente → concluído (armazena `ReportJson`), ausente → falhou.
- Logs de container ao vivo são transmitidos para o navegador via SignalR; curvas de patrimônio de backtest
  são analisadas do relatório + gráficas.

## Dados de mercado de backtest são armazenados em cache por conta

O cTrader Console baixa dados históricos de tick/bar em seu `--data-dir`. Esse diretório é um
**cache estável e persistente com chave na conta de negociação** (seu número de conta) — vinculado
do disco do nó em seu próprio caminho de container (`/mnt/data`), uma **montagem separada e não aninhada**
do diretório de trabalho por instância. Portanto, cada backtest na mesma conta **reutiliza** dados já
baixados em vez de baixá-los novamente a cada execução. (Anteriormente o
diretório de dados vivia sob o diretório de trabalho por instância, cuja id muda a cada execução, forçando
um download novo a cada backtest.) O diretório de trabalho efêmero por instância ainda contém o algoritmo,
parâmetros, senha e relatório; o cache de dados compartilhado é contado no uso de dados de backtest de um
nó e limpo pela ação node-clean.

## Configurações de backtest

O diálogo **Backtest** expõe as configurações de backtest do cTrader Console ajustáveis pelo usuário, para que
você nunca tenha que tocar em uma linha de comando:

- **Symbol / Timeframe** — o timeframe é um **dropdown de todo período cTrader** (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1` e períodos Renko/Range/Heikin), no
  caso canônico do console, para que você sempre escolha um `--period` válido.
- **From / To** — a janela de backtest (`--start` / `--end`).
- **Data mode** — um dos três modos cTrader (`--data-mode`): **Tick data** (`tick`, preciso),
  **m1 bars** (`m1`, rápido), ou **Open prices only** (`open`, mais rápido).
- **Starting balance** — padrão de `10000` (`--balance`). Um **saldo de 0 não coloca negociações
  e faz o cTrader emitir um relatório vazio que depois trava** ("Message expected"), portanto um
  saldo diferente de zero é sempre enviado.
- **Commission** e **Spread** — `--commission` / `--spread` (spread em pips).

O diretório de dados (`--data-file` / `--data-dir`) é gerenciado pelo app em si (um cache por conta, veja
acima), não exposto no diálogo.

## Página de detalhe da instância

Abrir uma instância (`/instance/{id}`) mostra seu status ao vivo, logs e — para um backtest — a curva de
patrimônio. O **título da aba do navegador** reflete a instância específica (**nome do cBot · tipo · símbolo**,
p.ex. `TrendBot · Backtest · EURUSD`) para que uma aba de execução ao vivo e uma aba de backtest sejam
distinguíveis à primeira vista. Uma execução e um backtest do mesmo cBot são rastreados como **linhagens**
distintas (uma id de linhagem estável realizada através de transições de estado), portanto a página segue exatamente
uma instância e nunca mistura dados de uma execução com os de um backtest.

## Controles de ciclo de vida da instância

Cada linha de instância (e sua página de detalhe) tem controles corretos de estado. Uma instância **ativa**
mostra **Stop**; uma **terminal** (Stopped / Completed / Failed) mostra **Start (▶)** para relançá-la com
o mesmo cBot, conta, símbolo, timeframe, ParamSet e imagem (uma execução reinicia como uma execução, um
backtest como um backtest). Clicar em Stop mostra uma notificação "Stopping…" e desativa o ícone até
se resolver, e uma execução recém-criada aparece na lista imediatamente — sem recarregar a página.

Logs do console são **persistidos quando uma instância termina** — para uma execução (ao parar) e para um
**backtest** (na conclusão) — para que os logs da última execução permaneçam visualizáveis na página de detalhe
e, via a barra de ferramentas de logs, **copiados para a área de transferência** (ícone Copiar logs) ou
**baixados** (ícone Baixar logs) até mesmo após o container ter desaparecido. Ambos agem no log de console
completo da instância, não apenas na cauda na tela.

Um `.algo` **enviado** nunca foi construído aqui, portanto sua coluna **Last Build** na página de cBots fica
em branco (mostra um tempo de build apenas para cBots que você constrói no navegador).

## Editar & executar novamente uma instância parada

Uma instância **parada** (execução ou backtest) tem um controle **Edit** — um ícone em sua linha na lista
**e** ao lado de Start/Stop em sua página de detalhe — que abre um diálogo **pré-preenchido** com sua
configuração atual. Você pode alterar a **conta de negociação, símbolo, timeframe, ParamSet e tag de imagem**
(e, para um backtest, a **janela e todas as configurações de backtest** acima), depois **Save & start**
relança com as novas configurações (substituindo a instância parada). O controle é **desativado enquanto a
instância está ativa** — apenas uma instância parada pode ser editada.

## Executar a partir do editor de código

Clicar em **Run** no editor de código abre um diálogo em vez de disparar uma execução cega e codificada:

- **Trading account** (obrigatório) — a conta cTrader à qual o cBot se conecta.
- **Parameter set** (opcional) — escolha um conjunto existente, ou deixe vazio para executar com os
  **valores de parâmetro padrão** do cBot. Um botão **+** ao lado do seletor cria um novo ParamSet
  inline (veja abaixo) e o seleciona.
- **Symbol / Timeframe** padrão para `EURUSD` / `h1` e podem ser alterados; **Cancel** ou **Run**.

No **Run** o editor salva + compila o fonte atual, inicia a instância na conta escolhida
com os parâmetros escolhidos, depois acompanha os logs de container ao vivo. (O fluxo de logs encaminha
o cookie de autenticação do usuário conectado para o hub SignalR `/hubs/logs`, para que se conecte em vez de
falhar com `Invalid negotiation response received`.)

## Parameter sets

Um **parameter set** é um conjunto nomeado e reutilizável de substituições de parâmetros de cBot armazenado
como um objeto JSON plano mapeando cada nome de parâmetro para um valor escalar, p.ex.
`{"Period": 14, "Label": "trend"}`. No momento da execução/backtest é transformado no arquivo cTrader
`params.cbotset` (`{ "Parameters": { … } }`). Você pode criar/editar um conjunto como JSON bruto do
diálogo **Parameter sets** do cBot ou inline a partir do diálogo Run.

Cada ParamSet **pertence a um cBot**: o diálogo New Parameter Set lista todos seus cBots e você
**deve escolher um** — criação é bloqueada até que um cBot seja selecionado. Um **nome de conjunto é
único por cBot**: criar ou renomear um conjunto para um nome que outro conjunto do mesmo cBot já usa é
rejeitado (um erro claro no diálogo, `409 Conflict` na API). O mesmo nome pode ser reutilizado em um
**cBot diferente**.

O JSON é **validado** ao salvar: deve ser um único objeto plano cujos valores são todos escalares
(string / number / bool). Uma raiz não-objeto, um array, um objeto aninhado, um valor `null`, ou JSON
malformado é rejeitado (um erro claro no diálogo, `400 Bad Request` na API). Um objeto vazio `{}`
é permitido e significa "sem substituições".

## Notas da CLI do cTrader Console

Backtests precisam de `--data-mode` (padrão `m1`), datas como `dd/MM/yyyy HH:mm`, e
argumento posicional JSON `params.cbotset`; `run` rejeita `--data-dir` (apenas backtest). Veja
`ContainerCommandHelpers`.

## Nodes & escala

Capacidade de execução escala adicionando agentes node (auto-registro + heartbeat). Veja
[node discovery](../operations/node-discovery.md) e [scaling](../deployment/scaling.md).

## Uma conta de negociação é obrigatória

Executar ou fazer backtest de um cBot requer uma conta de negociação cTrader à qual se conectar. Até que você
adicione uma em **Trading accounts**, os botões **Run New cBot** / **Backtest New cBot** estão desativados
(com tooltip) e a página mostra um prompt vinculando à configuração de conta — você não mais bate um erro
bruto `stream connect failed` de um bot sem conta.
