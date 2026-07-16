---
description: "Construir, executar e fazer backtest de cBots do cTrader (C# e Python, ambos .NET) a partir do editor Monaco integrado no navegador, executar na imagem oficial ghcr.io/spotware/ctrader-console."
---

# Construir e fazer backtest de cBots

Construir, executar e fazer backtest de cBots do cTrader (C# **e** Python, ambos .NET) a partir do editor Monaco integrado no navegador, executar na imagem oficial `ghcr.io/spotware/ctrader-console`.

## Construir

- **Página Builder** hospeda o editor Monaco; `CBotBuilder` compila o projeto com
  `dotnet build` **em um contêiner descartável** (`AppOptions.BuildImage`, diretório de trabalho bind-mount
  em `/work`), para que alvos MSBuild não confiáveis não atinjam o host. Restauração do NuGet é cacheada
  entre compilações por meio de um volume compartilhado. O host da web precisa de acesso ao socket do Docker.
- Modelos iniciais de C# + Python ficam em `src/Nodes/Builder/Templates/`.

## Executar e fazer backtest

- **Instances** = hierarquia de estado TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). A transição substitui a entidade (mudança de id),
  o id do contêiner é mantido.
- `NodeScheduler` escolhe o nó elegível com menor carga; `ContainerDispatcherFactory` roteia para
  um agente HTTP de nó remoto ou despachante do Docker local.
- Pollers de conclusão reconciliam contêineres fechados (contêineres de backtest saem automaticamente via
  `--exit-on-stop`); relatório presente → concluído (armazena `ReportJson`), ausente → falhou.
- Logs de contêiner ao vivo são transmitidos para o navegador por SignalR; curvas de patrimônio de backtest são analisadas do
  relatório e representadas em gráfico.

## Dados de mercado de backtest são armazenados em cache por conta

O cTrader Console baixa dados históricos de tick/barra em seu `--data-dir`. Esse diretório é um
**cache estável e persistente identificado pela conta de trading** (seu número de conta) — bind-mounted do
disco do nó em seu próprio caminho de contêiner (`/mnt/data`), um **mount separado e não aninhado** do
diretório de trabalho por instância. Portanto, cada backtest na mesma conta **reutiliza** os dados já
baixados em vez de baixá-los novamente a cada execução. (Anteriormente, o
diretório de dados ficava sob o diretório de trabalho por instância, cujo id muda a cada execução, o que forçava um
novo download a cada backtest.) O diretório de trabalho por instância efêmero ainda contém o algoritmo, parâmetros, senha
e relatório; o cache de dados compartilhado é contabilizado no uso de dados de backtest de um nó e limpo pela
ação de limpeza do nó.

## Configurações de backtest

O diálogo **Backtest** expõe as configurações de backtest do cTrader Console ajustáveis pelo usuário, para que você nunca precise
tocar uma linha de comando:

- **Symbol / Timeframe** — o timeframe é um **dropdown de cada período do cTrader** (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1`, e os períodos Renko/Range/Heikin), no
  caso canônico do console, para que você sempre escolha um `--period` válido.
- **From / To** — a janela de backtest (`--start` / `--end`).
- **Data mode** — um dos três modos do cTrader (`--data-mode`): **Tick data** (`tick`, preciso),
  **m1 bars** (`m1`, rápido), ou **Open prices only** (`open`, mais rápido).
- **Starting balance** — padrão de `10000` (`--balance`). Um **saldo de 0 não coloca trades e faz o
  cTrader emitir um relatório vazio que então falha** ("Message expected"), portanto um saldo diferente de zero é
  sempre enviado.
- **Commission** — `--commission`.
- **Spread** — `--spread`, um **campo numérico em pips que não pode ficar abaixo de 0**. É **ocultado no modo Tick
  data**, onde o cTrader deriva o spread dos dados de tick em si (nenhum `--spread` é enviado).

O diretório de dados (`--data-file` / `--data-dir`) é gerenciado pelo próprio app (um cache por conta, veja
acima), não exposto no diálogo.

:::note cTrader falha em um backtest vazio
Se um backtest produz **sem resultados** — sem trades, ou sem dados de mercado para as datas/símbolos escolhidos —
o gravador de relatório do próprio cTrader Console lança `Message expected` e sai sem um relatório. O app não pode
corrigir esse bug upstream, mas o detecta e marca a instância como **Failed** com um motivo acionável
("no backtest results for the selected range…") em vez de um rastreamento de pilha bruto. Escolha um intervalo de data mais amplo
que tem dados de mercado disponíveis e tente novamente.
:::

## Página de detalhe da instância

Abrir uma instância (`/instance/{id}`) mostra seu status ao vivo, logs e — para um backtest — a curva de
patrimônio. O **título da aba do navegador** reflete a instância específica (**nome do cBot · tipo · símbolo**, por ex.
`TrendBot · Backtest · EURUSD`) para que uma aba de execução ao vivo e uma aba de backtest sejam distinguíveis num piscar de olhos.
Uma execução e um backtest do mesmo cBot são rastreados como **linhagens** distintas (um id de linhagem estável mantido
através de transições de estado), portanto a página segue exatamente uma instância e nunca mistura dados de uma execução com dados de um
backtest.

## Controles de ciclo de vida da instância

Cada linha de instância (e sua página de detalhe) tem controles corretos para o estado. Uma instância **ativa** mostra
**Stop**; uma **terminal** (Stopped / Completed / Failed) mostra **Start (▶)** para relançá-la com
o mesmo cBot, conta, símbolo, timeframe, conjunto de parâmetros e imagem (uma execução reinicia como uma execução, um
backtest como um backtest). Clicar em Stop mostra um aviso "Stopping…" e desabilita o ícone até que seja
resolvido, e uma execução recém-criada aparece na lista imediatamente — sem recarga de página.

Logs do console são **persistidos quando uma instância encerra** — para uma execução (ao parar) e para um
**backtest** (ao completar) — para que os logs da última execução permaneçam visualizáveis na página de detalhe e,
via barra de ferramentas de log, **copiados para a área de transferência** (ícone Copiar logs) ou **baixados**
(ícone Baixar logs) mesmo após o contêiner desaparecer. Ambos atuam no log completo do console da instância, não apenas na
cauda visível na tela.

Um `.algo` **carregado** nunca foi compilado aqui, então sua coluna **Last Build** na página de cBots é deixada
em branco (mostra um tempo de compilação apenas para cBots que você compila no navegador).

## Editar e re-executar uma instância parada

Uma instância **parada** (execução ou backtest) tem um controle **Edit** — um ícone em sua linha na lista **e**
ao lado de Start/Stop em sua página de detalhe — que abre um diálogo **preenchido** com sua configuração atual.
Você pode alterar a **conta de trading, símbolo, timeframe, conjunto de parâmetros e tag de imagem** (e, para um
backtest, a **janela e todas as configurações de backtest** acima), então **Save & start** relança com as
novas configurações (substituindo a instância parada). O controle é **desabilitado enquanto a instância está ativa** —
apenas uma instância parada pode ser editada.

## Executar do editor de código

Clicar em **Run** no editor de código abre um diálogo em vez de disparar uma execução cega e codificada:

- **Trading account** (obrigatório) — a conta do cTrader à qual o cBot se conecta.
- **Parameter set** (opcional) — escolha um conjunto existente ou deixe em branco para executar com o **padrão
  valores de parâmetro** do cBot. Um botão **+** ao lado do seletor cria um novo conjunto de parâmetros
  inline (veja abaixo) e o seleciona.
- **Symbol / Timeframe** padrão de `EURUSD` / `h1` e podem ser alterados; **Cancel** ou **Run**.

Em **Run** o editor salva + compila a fonte atual, inicia a instância na conta escolhida
com os parâmetros escolhidos, então rastreia os logs do contêiner ao vivo. (O fluxo de log encaminha o cookie de autenticação
do usuário conectado para o hub SignalR `/hubs/logs`, portanto ele se conecta em vez de falhar com
`Invalid negotiation response received`.)

## Conjuntos de parâmetros

Um **parameter set** é um conjunto nomeado e reutilizável de substituições de parâmetros de cBot armazenado como um objeto JSON plano
mapeando cada nome de parâmetro para um valor escalar, por ex. `{"Period": 14, "Label": "trend"}`. No
tempo de execução/backtest, ele é transformado no arquivo `params.cbotset` do cTrader
(`{ "Parameters": { … } }`). Você pode criar/editar um conjunto como JSON bruto do diálogo **Parameter
sets** do cBot ou inline no diálogo Run.

Todo conjunto de parâmetros **pertence a um cBot**: o diálogo New Parameter Set lista todos os seus cBots e você
**deve escolher um** — a criação é bloqueada até que um cBot seja selecionado. O **nome de um conjunto é único por cBot**:
criar ou renomear um conjunto para um nome que outro conjunto do mesmo cBot já usa é rejeitado (um erro claro
no diálogo, `409 Conflict` na API). O mesmo nome pode ser reutilizado em um **cBot diferente**.

O JSON é **validado** ao salvar: ele deve ser um objeto plano único cujos valores são todos escalares
(string / number / bool). Uma raiz não-objeto, um array, um objeto aninhado, um valor `null` ou JSON
malformado é rejeitado (um erro claro no diálogo, `400 Bad Request` na API). Um objeto vazio `{}`
é permitido e significa "sem sobrescrita".

## Notas sobre CLI do cTrader Console

Backtests precisam de `--data-mode` (padrão `m1`), datas como `dd/MM/yyyy HH:mm`, e
JSON de argumento posicional `params.cbotset`; `run` rejeita `--data-dir` (apenas backtest). Veja
`ContainerCommandHelpers`.

## Nós e escala

A capacidade de execução escala adicionando agentes de nó (auto-registro + heartbeat). Veja
[node discovery](../operations/node-discovery.md) e [scaling](../deployment/scaling.md).

## Uma conta de trading é obrigatória

Executar ou fazer backtest de um cBot requer uma conta de trading do cTrader para se conectar. Até você adicionar uma em
**Trading accounts**, os botões **Run New cBot** / **Backtest New cBot** são desabilitados (com um
tooltip) e a página mostra um prompt vinculado à configuração de conta — você não mais recebe um erro bruto
`stream connect failed` de um bot sem conta.
