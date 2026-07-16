---
description: "Compile, execute e backtest cBots do cTrader (C# e Python, ambos .NET) do editor Monaco integrado no navegador, execute na imagem oficial ghcr.io/spotware/ctrader-console."
---

# Compilar e fazer backtest de cBots

Compile, execute e backtest cBots do cTrader (C# **e** Python, ambos .NET) do editor Monaco integrado no navegador, execute na imagem oficial `ghcr.io/spotware/ctrader-console`.

## Compilar

- A página **Builder** hospeda o editor Monaco; `CBotBuilder` compila o projeto com
  `dotnet build` **em um contêiner descartável** (`AppOptions.BuildImage`, diretório de trabalho montado
  em `/work`), para que alvos de MSBuild de usuários não confiáveis não alcancem o host. A restauração do NuGet é armazenada em cache
  entre compilações por meio de um volume compartilhado. O host web precisa de acesso ao socket Docker.
- Os templates de inicialização C# + Python residem em `src/Nodes/Builder/Templates/`.

## Executar e fazer backtest

- **Instances** = hierarquia de estado TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). A transição substitui a entidade (mudança de id),
  a id do contêiner é mantida.
- `NodeScheduler` seleciona o nó menos carregado elegível; `ContainerDispatcherFactory` roteia para
  agente HTTP de nó remoto ou despachante Docker local.
- Pollers de conclusão reconciliam contêineres encerrados (contêineres de backtest se encerram automaticamente via
  `--exit-on-stop`); relatório presente → completado (armazena `ReportJson`), ausente → falhou.
- Os logs do contêiner ao vivo transmitem para o navegador por SignalR; as curvas de patrimônio de backtest são analisadas do
  relatório + gráfico.

## Os dados de mercado de backtest são armazenados em cache por conta

O cTrader Console baixa dados históricos de tick/bar em seu `--data-dir`. Esse diretório é um
**cache estável e persistente com chave na conta de negociação** (seu número de conta) — montado em bind do
disco do nó em seu próprio caminho de contêiner (`/mnt/data`), uma **montagem separada e não aninhada** do
diretório de trabalho por instância. Então, cada backtest na mesma conta **reutiliza** os dados já baixados
em vez de re-baixá-los a cada execução. (Anteriormente, o
diretório de dados residia sob o diretório de trabalho por instância, cuja id muda a cada execução, o que forçava um novo
download a cada backtest.) O diretório de trabalho por instância efêmero ainda contém o algoritmo, parâmetros, senha
e relatório; o cache de dados compartilhado é contado no uso de dados de backtest de um nó e limpo pela
ação de limpeza do nó.

## Configurações de backtest

O diálogo **Backtest** expõe todas as configurações que a CLI de backtest do cTrader Console aceita, para que você nunca
precise tocar em uma linha de comando:

- **From / To** — a janela de backtest (`--start` / `--end`).
- **Data mode** — `m1` (barras de 1 minuto) ou `tick` (`--data-mode`).
- **Starting balance** — padrão de `10000` (`--balance`). Um **saldo de 0 não coloca nenhuma negociação e faz
  cTrader emitir um relatório vazio que depois falha** ("Message expected"), portanto um saldo diferente de zero é
  sempre enviado.
- **Commission** e **Spread** (`--commission` / `--spread`, spread em pips).
- **Advanced options** — uma caixa de forma livre `name=value` por linha para qualquer outra opção de backtest que cTrader
  suporta (ex: `applyCommissionAutomatically=true`); cada linha se torna um argumento de CLI `--name value`.

## Página de detalhe da instância

Abrir uma instância (`/instance/{id}`) mostra seu status ao vivo, logs e — para um backtest — a curva de patrimônio.
O **título da aba do navegador** reflete a instância específica (**nome do cBot · tipo · símbolo**, ex:
`TrendBot · Backtest · EURUSD`) para que uma aba de execução ao vivo e uma aba de backtest sejam distinguíveis à primeira vista.
Uma execução e um backtest do mesmo cBot são rastreados como **linhagens** distintas (uma id de linhagem estável mantida
através de transições de estado), para que a página siga exatamente uma instância e nunca misture os dados de uma execução com os dados de um
backtest.

## Controles de ciclo de vida da instância

Cada linha de instância (e sua página de detalhe) possui controles corretos de estado. Uma instância **ativa** mostra
**Stop**; uma **terminal** (Stopped / Completed / Failed) mostra **Start (▶)** para relançá-la com
o mesmo cBot, conta, símbolo, timeframe, conjunto de parâmetros e imagem (uma execução reinicia como execução, um
backtest como backtest). Clicar em Stop mostra um aviso "Stopping…" e desabilita o ícone até ser resolvido, e uma
execução recém-criada aparece na lista imediatamente — sem recarregar a página.

Os logs do console são **persistidos quando uma instância encerra** — para uma execução (ao parar) e para um
**backtest** (após conclusão) — para que os logs da última execução permaneçam visíveis na página de detalhe e,
pela barra de ferramentas de log, **copiado para a área de transferência** (ícone Copy logs) ou **baixado** (ícone Download logs)
mesmo após o contêiner desaparecer. Ambos atuam no log completo do console da instância, não apenas no final
mostrado na tela.

Um `.algo` **carregado** nunca foi compilado aqui, então sua coluna **Last Build** na página de cBots é deixada
em branco (mostra um tempo de compilação apenas para cBots que você compila no navegador).

## Editar e re-executar uma instância parada

Uma instância **parada** (execução ou backtest) possui um controle **Edit** — um ícone em sua linha na lista **e**
ao lado de Start/Stop em sua página de detalhe — que abre um diálogo **pré-preenchido** com sua configuração atual.
Você pode alterar a **conta de negociação, símbolo, timeframe, conjunto de parâmetros e tag de imagem** (e, para um
backtest, a **janela e todas as configurações de backtest** acima), depois **Save & start** relança com as
novas configurações (substituindo a instância parada). O controle é **desabilitado enquanto a instância está ativa** —
apenas uma instância parada pode ser editada.

## Executar a partir do editor de código

Clicar em **Run** no editor de código abre um diálogo em vez de disparar uma execução cega e hard-coded:

- **Trading account** (obrigatório) — a conta cTrader à qual o cBot se conecta.
- **Parameter set** (opcional) — escolha um conjunto existente ou deixe vazio para executar com os
  **valores de parâmetro padrão** do cBot. Um botão **+** ao lado do seletor cria um novo conjunto de parâmetros
  inline (veja abaixo) e o seleciona.
- **Symbol / Timeframe** padrão para `EURUSD` / `h1` e podem ser alterados; **Cancel** ou **Run**.

Ao **Run** o editor salva + compila a fonte atual, inicia a instância na conta escolhida
com os parâmetros escolhidos, depois segue os logs do contêiner ao vivo. (O fluxo de log encaminha o
cookie de autenticação do usuário conectado para o hub SignalR `/hubs/logs`, para que se conecte em vez de falhar com
`Invalid negotiation response received`.)

## Conjuntos de parâmetros

Um **parameter set** é um conjunto nomeado e reutilizável de sobreposições de parâmetros de cBot armazenado como um
objeto JSON flat que mapeia cada nome de parâmetro para um valor escalar, ex: `{"Period": 14, "Label": "trend"}`. No
tempo de execução/backtest, é transformado no arquivo `params.cbotset` do cTrader
(`{ "Parameters": { … } }`). Você pode criar/editar um conjunto como JSON bruto no diálogo **Parameter
sets** do cBot ou inline no diálogo Run.

Todo conjunto de parâmetros **pertence a um cBot**: o diálogo New Parameter Set lista todos os seus cBots e você
**deve escolher um** — a criação é bloqueada até que um cBot seja selecionado. O **nome de um conjunto é único por cBot**:
criar ou renomear um conjunto para um nome que outro conjunto do mesmo cBot já usa é rejeitado (um erro claro
no diálogo, `409 Conflict` na API). O mesmo nome pode ser reutilizado em um **cBot diferente**.

O JSON é **validado** ao salvar: deve ser um único objeto flat cujos valores são todos escalares
(string / number / bool). Uma raiz não-objeto, um array, um objeto aninhado, um valor `null` ou
JSON malformado é rejeitado (um erro claro no diálogo, `400 Bad Request` na API). Um objeto vazio `{}`
é permitido e significa "sem sobreposições".

## Notas da CLI do cTrader Console

Backtests precisam de `--data-mode` (padrão `m1`), datas como `dd/MM/yyyy HH:mm` e
argumento posicional JSON `params.cbotset`; `run` rejeita `--data-dir` (somente backtest). Veja
`ContainerCommandHelpers`.

## Nós e escala

A capacidade de execução escala adicionando agentes de nó (auto-registro + heartbeat). Veja
[node discovery](../operations/node-discovery.md) e [scaling](../deployment/scaling.md).

## Uma conta de negociação é obrigatória

Executar ou fazer backtest de um cBot requer uma conta de negociação cTrader para se conectar. Até você adicionar uma sob
**Trading accounts**, os botões **Run New cBot** / **Backtest New cBot** estão desabilitados (com um
tooltip) e a página mostra um prompt vinculado à configuração de conta — você não obtém mais um erro bruto
`stream connect failed` de um bot sem conta.
