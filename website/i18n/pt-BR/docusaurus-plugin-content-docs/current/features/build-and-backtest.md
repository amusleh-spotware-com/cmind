---
description: "Compilar, executar, fazer backtest de cBots cTrader (C# e Python, ambos .NET) a partir do editor Monaco integrado no navegador, executar na imagem oficial ghcr.io/spotware/ctrader-console."
---

# Compilar e fazer backtest de cBots

Compile, execute e faça backtest de cBots cTrader (C# **e** Python, ambos .NET) a partir do editor
Monaco integrado no navegador, executando na imagem oficial `ghcr.io/spotware/ctrader-console`.

## Compilar

- A página **Builder** hospeda o editor Monaco; `CBotBuilder` compila o projeto com
  `dotnet build` **em um contêiner descartável** (`AppOptions.BuildImage`, diretório de trabalho montado em bind
  em `/work`), para que destinos MSBuild de usuários não confiáveis não acessem o host. A restauração do NuGet é armazenada em cache
  entre compilações via volume compartilhado. O host web precisa de acesso ao socket do Docker.
- Os modelos iniciais de C# e Python vivem em `src/Nodes/Builder/Templates/`.

## Executar e fazer backtest

- **Instances** = hierarquia de estado TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Transição substitui a entidade (id muda),
  id do contêiner é mantido.
- `NodeScheduler` escolhe o nó elegível menos carregado; `ContainerDispatcherFactory` roteia para
  agente HTTP de nó remoto ou despachante Docker local.
- Pollers de conclusão reconciliam contêineres encerrados (contêineres de backtest saem automaticamente via
  `--exit-on-stop`); relatório presente → completado (armazena `ReportJson`), ausente → falha.
- Logs de contêiner ao vivo são transmitidos para o navegador via SignalR; curvas de patrimônio de backtest são analisadas do
  relatório e exibidas em gráfico.

## Os dados de mercado de backtest são armazenados em cache por conta

O cTrader Console baixa dados históricos de tick/bar para seu `--data-dir`. Esse diretório é um
**cache estável e persistente com chave na conta de negociação** (seu número de conta) — montado em bind do
disco do nó em seu próprio caminho de contêiner (`/mnt/data`), um **mount separado e não aninhado** do
diretório de trabalho por instância. Portanto, cada backtest na mesma conta **reutiliza** o
dado já baixado em vez de fazê-lo novamente a cada execução. (Antes, o
diretório de dados ficava sob o diretório de trabalho por instância, cujo id muda a cada execução, o que forçava um novo
download a cada backtest.) O diretório de trabalho efêmero por instância ainda contém o algoritmo, parâmetros, senha
e relatório; o cache de dados compartilhado é contabilizado no uso de dados de backtest de um nó e é limpo pela
ação node-clean.

## Configurações de backtest

O diálogo **Backtest** expõe as configurações de backtest do cTrader Console ajustáveis pelo usuário, para que você nunca precise
tocar em uma linha de comando:

- **Symbol / Timeframe** — o timeframe é um **dropdown de todo período cTrader** (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1`, e os períodos Renko/Range/Heikin), no
  uso canônico da console, então você sempre escolhe um `--period` válido.
- **From / To** — a janela de backtest (`--start` / `--end`).
- **Data mode** — um dos três modos cTrader (`--data-mode`): **Tick data** (`tick`, preciso),
  **m1 bars** (`m1`, rápido), ou **Open prices only** (`open`, mais rápido).
- **Starting balance** — padrão `10000` (`--balance`). Um **saldo de 0 não coloca trades e faz
  cTrader emitir um relatório vazio que depois falha** ("Message expected"), então um saldo diferente de zero é
  sempre enviado.
- **Commission** — `--commission`.
- **Spread** — `--spread`, um **campo numérico em pips que não pode ser inferior a 0**. Ele é **oculto no modo Tick
  data**, onde cTrader deriva o spread dos dados de tick em si (nenhum `--spread` é enviado).

O diretório de dados (`--data-file` / `--data-dir`) é gerenciado pelo próprio aplicativo (um cache por conta, veja
acima), não exposto no diálogo.

:::note cTrader falha em um backtest vazio
Se um backtest não produz **resultados** — nenhum trade, ou nenhum dado de mercado para as datas/símbolo escolhidos —
o próprio gravador de relatórios da cTrader Console lança `Message expected` e sai sem um relatório. O aplicativo não pode
corrigir esse bug upstream, mas detecta e marca a instância como **Failed** com um motivo acionável
("nenhum resultado de backtest para o intervalo selecionado…") em vez de um rastreamento de pilha bruto. Escolha uma faixa de data mais ampla
que tenha dados de mercado disponíveis e tente novamente.
:::

## Página de detalhe da instância

Abrir uma instância (`/instance/{id}`) mostra seu status ao vivo, logs e — para um backtest — a curva de patrimônio.
O **título da aba do navegador** reflete a instância específica (**nome do cBot · tipo · símbolo**, por exemplo
`TrendBot · Backtest · EURUSD`) para que uma aba de run ao vivo e uma aba de backtest possam ser distinguidas à primeira vista.
Um run e um backtest do mesmo cBot são rastreados como **lineagens** distintas (um id de lineagem estável carregado
através de transições de estado), para que a página siga exatamente uma instância e nunca misture dados de um run com um
de um backtest.

## Controles de ciclo de vida da instância

Cada linha de instância (e sua página de detalhe) possui controles corretos para estado. Uma instância **ativa** mostra
**Stop**; uma **terminal** (Stopped / Completed / Failed) mostra **Start (▶)** para reiniciá-la com
o mesmo cBot, conta, símbolo, timeframe, conjunto de parâmetros e imagem (um run reinicia como um run, um
backtest como um backtest). Clicar em Stop mostra um aviso "Stopping…" e desativa o ícone até que seja
resolvido, e um run recém-criado aparece na lista imediatamente — sem recarga de página.

Os logs do console são **persistidos quando uma instância é encerrada** — para um run (ao parar) e para um
**backtest** (ao completar) — para que os logs da última execução permaneçam visualizáveis na página de detalhe e,
via barra de ferramentas de log, **copiados para a área de transferência** (ícone Copy logs) ou **baixados** (ícone Download logs)
mesmo após o contêiner desaparecer. Ambos atuam no log completo do console da instância, não apenas na
cauda na tela.

Um **backtest concluído** também persiste seu **relatório cTrader** em ambos os formatos — o **JSON** bruto
(o mesmo que a curva de patrimônio e análise de IA leem) e o relatório completo em **HTML**. Ambos são
disponíveis para download da linha de backtest **e** da página de detalhe via ícones dedicados. Apenas os
relatórios da **última execução** são mantidos, e os ícones são **desativados** para qualquer backtest que não foi iniciado, está em execução ou falhou
(e nunca são mostrados para uma instância de run) — apenas um backtest concluído tem um relatório para fazer download.

Um `.algo` **enviado** nunca foi compilado aqui, portanto sua coluna **Last Build** na página de cBots fica
em branco (mostra um tempo de compilação apenas para cBots que você compila no navegador).

## Editar e executar novamente uma instância parada

Uma instância **parada** (run ou backtest) tem um controle **Edit** — um ícone em sua linha na lista **e**
ao lado de Start/Stop em sua página de detalhe — que abre um diálogo **pré-preenchido** com sua configuração atual.
Você pode alterar a **conta de negociação, símbolo, timeframe, conjunto de parâmetros e tag de imagem** (e, para um
backtest, a **janela e todas as configurações de backtest** acima), depois **Save & start** o reinicia com as
novas configurações (substituindo a instância parada). O controle é **desativado enquanto a instância está ativa** —
apenas uma instância parada pode ser editada.

## Executar do editor de código

Clicar em **Run** no editor de código abre um diálogo em vez de disparar uma execução cega e codificada:

- **Trading account** (obrigatório) — a conta cTrader à qual o cBot se conecta.
- **Parameter set** (opcional) — escolha um conjunto existente, ou deixe vazio para executar com o **padrão**
  **valores de parâmetro** do cBot. Um botão **+** ao lado do seletor cria um novo conjunto de parâmetros
  inline (veja abaixo) e o seleciona.
- **Symbol / Timeframe** têm padrão `EURUSD` / `h1` e podem ser alterados; **Cancel** ou **Run**.

Em **Run** o editor salva + compila a fonte atual, inicia a instância na conta escolhida
com os parâmetros escolhidos, depois segue os logs do contêiner ao vivo. (O fluxo de log encaminha o
cookie de autenticação do usuário conectado para o hub SignalR `/hubs/logs`, para que ele se conecte em vez de falhar com
`Invalid negotiation response received`.)

## Conjuntos de parâmetros

Um **parameter set** é um conjunto nomeado e reutilizável de sobreposições de parâmetros de cBot armazenado como um objeto JSON plano
mapeando cada nome de parâmetro para um valor escalar, por exemplo `{"Period": 14, "Label": "trend"}`. No
tempo de run/backtest é transformado no arquivo `params.cbotset` do cTrader
(`{ "Parameters": { … } }`). Você pode criar/editar um conjunto como JSON bruto do diálogo **Parameter
sets** do cBot ou inline do diálogo Run.

Cada conjunto de parâmetros **pertence a um cBot**: o diálogo New Parameter Set lista todos os seus cBots e você
**deve escolher um** — criação é bloqueada até que um cBot seja selecionado. O **nome** de um conjunto é **exclusivo por cBot**:
criar ou renomear um conjunto para um nome que outro conjunto do mesmo cBot já usa é rejeitado (um erro
claro no diálogo, `409 Conflict` na API). O mesmo nome pode ser reutilizado em um **cBot diferente**.

O JSON é **validado** ao salvar: deve ser um único objeto plano cujos valores sejam todos escalares
(string / number / bool). Uma raiz que não é objeto, um array, um objeto aninhado, um valor `null`, ou JSON
malformado é rejeitado (um erro claro no diálogo, `400 Bad Request` na API). Um objeto vazio `{}`
é permitido e significa "nenhuma sobreposição".

## Notas da CLI cTrader Console

Backtests precisam de `--data-mode` (padrão `m1`), datas como `dd/MM/yyyy HH:mm`, e
argumento posicional JSON `params.cbotset`; `run` rejeita `--data-dir` (apenas backtest). Veja
`ContainerCommandHelpers`.

## Nós e escala

Capacidade de execução escala adicionando agentes de nó (auto-registro + heartbeat). Veja
[node discovery](../operations/node-discovery.md) e [scaling](../deployment/scaling.md).

## Uma conta de negociação é necessária

Executar ou fazer backtest de um cBot precisa de uma conta de negociação cTrader para se conectar. Até você adicionar uma em
**Trading accounts**, os botões **Run New cBot** / **Backtest New cBot** estão desativados (com um
tooltip) e a página mostra um prompt linkando para configuração de conta — você não mais encontra um erro bruto
`stream connect failed` de um bot sem conta.
