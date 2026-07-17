---
description: "Espelhe a conta cTrader master em uma ou mais contas slave — entre brokers, entre cID — com controle por destino + reconciliação de nível bancário."
---

# Copy trading

Espelhe a conta cTrader **master** em uma ou mais contas **slave** — entre brokers, entre cID — com controle por destino + reconciliação de nível bancário.

## Conceitos

- **Perfil de cópia** — um master (`SourceAccountId`) + um ou mais **destinos**. Ciclo de vida: `Draft → Running → Paused → Stopped` (`Error` em falha). Raiz agregada: `CopyProfile` (possui `CopyDestination`).
- **Destino** — uma conta slave + conjunto completo de regras de como o master é copiado para ela. Toda configuração por destino, assim um master alimenta slaves conservadores + agressivos simultaneamente.
- **Host do mecanismo de cópia** — worker em execução do perfil (`CopyEngineHost`). Subscreve o fluxo de execução do master, aplica cada evento a cada destino.
- **Supervisor** — `CopyEngineSupervisor`, serviço de fundo em cada nó. Hospeda perfis atribuídos, auto-recuperação entre cluster (veja [scaling](../deployment/scaling.md)).

## O que é espelhado

| Evento master | Ação slave |
|--------------|------------|
| Abertura de posição market / market-range | Abre uma cópia dimensionada (rotulada com o id da posição de origem) |
| Ordem pendente limit / stop / stop-limit | Coloca a ordem pendente correspondente |
| Alteração de ordem pendente | Altera a ordem pendente espelhada em lugar |
| Cancelamento / vencimento de ordem pendente | Cancela a ordem pendente espelhada |
| Fechamento parcial | Fecha a mesma proporção da posição slave |
| Scale-in (aumento de volume) | Abre o volume adicionado (opt-in) |
| Alteração de stop-loss / trailing-stop | Altera a proteção da posição slave |
| Fechamento total | Fecha a cópia slave |

Toda cópia **rotulada com id da posição/ordem de origem**. Após reconexão, o host reconstrói o estado a partir da reconciliação: abre cópias que o master mantém mas slave não tem, fecha "órfãos" que o master não mantém mais — **sem duplicar trades**.

## Criando um perfil

**Novo Perfil** abre um formulário **página inteira** dedicado (`/copy-trading/new`), não um diálogo — o conjunto de opções é grande o suficiente que uma página se lê melhor em phone e desktop. Coleta tudo antecipadamente: nome do perfil, conta de origem (master), contas de destino (slave) (multi-seleção com botão **Selecionar tudo**; master escolhido excluído da lista slave), + o conjunto completo de opções por destino. **Apenas contas vinculadas através da cTrader Open API são selecionáveis** como master ou destino — copiar coloca ordens pela Open API, assim uma conta adicionada manualmente (apenas cID) não pode copiar e não está listada; quando nenhuma está vinculada a página mostra um aviso apontando para Contas de Negociação. Modos de dimensionamento, direção e o filtro de símbolos renderizam como **rótulos humanos** com uma **explicação com bullet points por modo** na dica de ajuda de gerenciamento de dinheiro. **Todo controle carrega uma dica de ajuda** explicando o que faz e como usá-lo. Entradas estruturadas usam **controles devidamente validados** — números/percentual via campos numéricos, modos/direção/filtro via seleções, filtro de símbolos via lista add/remover de chips de símbolos, e mapa de símbolos via tabela add/remover de linhas `Origem → Destino (× multiplicador)` — nunca um blob de texto separado por vírgula. Todas as entradas **validadas antes de salvar** — nome/origem/destino faltando, parâmetro de dimensionamento não-positivo, limites de lot negativo/inconsistente, percentual drawdown fora do intervalo, nenhum tipo de ordem habilitado, ou filtro de símbolo vazio aparecem como lista de erro + bloqueiam salvar. Ao criar, o perfil é criado + cada slave selecionado adicionado com as configurações escolhidas, então a página retorna à lista Copy Trading.

**Importar / exportar.** O bloco de configurações inteiro pode ser **exportado para um arquivo JSON** e **reimportado** para preencher o formulário, assim uma otimização pode ser reutilizada entre perfis sem retipagem. O mapa de símbolos pode igualmente ser **exportado / importado como um arquivo CSV** (`Origem,Destino,VolumeMultiplier`) — prepare um grande mapa de símbolos do broker em uma planilha e carregue em uma etapa. Os mesmos controles de símbolos e importação/exportação CSV também estão disponíveis no diálogo de destino na página Copy Trading.

Ações de linha respeitam ciclo de vida: **Iniciar** habilitado apenas quando não em execução, **Parar** + **Pausar** apenas quando em execução, **Deletar** desabilitado enquanto em execução + solicita confirmação antes de remover perfil + destinos.

## Opções por destino

Defina na página Novo Perfil, no diálogo de destino na página Copy Trading, ou via `POST /api/copy/profiles/{id}/destinations`:

- **Dimensionamento** (`MoneyManagementMode` + parâmetro): lot fixo, multiplicador lot/nocional, balanço/equity/margem-livre proporcional, risco fixo %, alavancagem fixa, auto-proporcional, **risco-%-de-stop** (M7). Mais limites de lot mín/máx + forçar-lot-mín. **Risco-de-stop** dimensiona destino para que ele arrisce percentual configurado do *seu próprio* balanço, derivado da **distância de stop-loss do master** (`master arrisca 2% → slave auto-arrisca 2%`): `lots = balanço×% ÷ (stopDistance × tamanho-contrato)`. Master aberto **sem** stop-loss não tem distância para dimensionar — usa **lot de fallback de risco máximo** configurado (M7) se definido, senão pulado (`no_stop_loss`) não adivinhado. Equity/**margem-livre** proporcional dimensiona da **equity** real da conta (`balanço + Σ P&L flutuante`, derivado per cTrader Open API que não entrega equity), não balanço simples — assim master sentado em lucro/perda aberto dimensiona cópias corretamente. Margem usada não exposta por API reconciliar, assim margem-livre tratada como equity (proxy honesto de fundos disponíveis); outros modos leem balanço + pulam rodada de revalorização extra.
- **Filtro de direção**: ambos / apenas-longo / apenas-curto. **Reverso**: inverte lado (+ troca SL↔TP) para cópia contrária.
- **Apenas gerenciar** (Ignorar-Novos-Trades / Apenas-Fechar): espelha fechamentos, fechamentos parciais + mudanças de proteção em posições já copiadas, mas abre **nenhuma** nova posição/ordem pendente (pulada `manage_only`). Use para desmontar destino sem cortar cópias existentes.
- **Sincronizar-Abertas-ao-iniciar** / **Sincronizar-Fechadas-ao-iniciar** (padrão ativado): no **primeiro** resync do perfil, se deve abrir cópias para posições pré-existentes do master, + se deve fechar cópias que o master fechou enquanto o perfil estava parado. Ambas aplicam apenas ao início — reconexão mid-run sempre reconcilia completamente assim desync recupera independentemente.
- **Mapa de símbolos** + **filtro de símbolos** (whitelist / blacklist). Cada entrada de mapa-de-símbolos carrega **multiplicador de volume por-símbolo** opcional (substituição cMAM por-símbolo) dimensionando tamanho de cópia para aquele símbolo no topo do dimensionamento do destino (1 = sem mudança). Mapa inteiro importa/exporta como **CSV** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; colunas `Origem,Destino,VolumeMultiplier`) — cada linha validada através de objetos de valor de domínio, assim arquivo malformado não pode produzir mapa inválido.
- **Janela de horário de negociação** (C18) — janela UTC diária por-destino (`início`/`fim` minutos-do-dia, fim exclusivo; `início == fim` = o dia inteiro). Novas aberturas fora da janela puladas (`trading_hours`); janela com `início > fim` envolve passando meia-noite (ex. 22:00–06:00). Posições existentes permanecem gerenciadas.
- **Filtro de rótulo de origem** (C18, equivalente cTrader do filtro de número mágico MT) — quando definido, copia apenas trades do master cujo rótulo corresponde **exatamente** (ex. trades de um bot, ou rótulo apenas manual); senão pulado (`source_label`). Vazio = copia tudo. Carregado em `ExecutionEvent.SourceLabel` do rótulo `TradeData.Label` da posição/ordem do master, honrado em resync também.
- **Proteção de conta** (ZuluGuard / Global Account Protection) — observa **equity ao vivo** do destino (`balanço + Σ P&L flutuante`, polada a cada `CopyDefaults.EquityGuardInterval`) contra piso `StopEquity` e/ou teto `TakeEquity` opcional. Em brecha, aplica modo: **ApenasFechar** (para novas cópias, mantém gerenciando existentes), **Congelado** (para abrindo), **Venda** (fecha **todas** as cópias no destino imediatamente). Uma vez disparado, destino travado — nenhuma nova abertura até restart do host — + alerta `CopyAccountProtectionTriggered` levantado. `SellOut` requer `StopEquity`; `TakeEquity` deve ficar acima de `StopEquity`. **Aviso sem garantia:** venda usa execução de mercado — como equivalente de cada competidor, não pode garantir preço de preenchimento em mercado rápido/com gap.
- **Botão de pânico Flatten-All** (C8) — `POST /api/copy/profiles/{id}/flatten` fecha imediatamente **cada** posição copiada em cada destino + bloqueia contra novas aberturas. Roteado entre processos: API define flag, supervisor entrega ao host em execução (reutilizando canal de rotação de token), que achata no local; flag limpa assim dispara exatamente uma vez (`CopyFlattenAll` alerta). Usuário então pausa/para perfil.
- **Guarda de regra prop-firm** (C7) — enforcement que usuários copiadores prop-firm pedem. Por destino, **limite de perda diária** (perda do equity de abertura do dia) e/ou limite de **trailing-drawdown** (perda do equity de pico em execução), ambas em moeda de depósito. Em brecha destino **auto-achatado** (toda cópia fechada) + **bloqueado** resto do dia UTC (novas aberturas puladas `prop_lockout`); alerta `CopyPropRuleBreached` dispara. Bloqueio limpa quando dia UTC muda (baseline/pico novo tomado). Compartilha mesma pesquisa de equity ao vivo que proteção de conta.
- **Jitter de execução** (C11, desativado por padrão) — atraso aleatório `0..N` ms antes de colocar cada cópia, para decorrelacionar timestamps de ordem quase-idênticos entre **próprias** contas do usuário. **Aviso de conformidade:** ajuda para prop firms que *permitem* cópia — **não** ferramenta para evadir firm que proíbe; ficar dentro das regras da sua firm é sua responsabilidade.
- **Bloqueio de config** (C9) — congela configurações do destino por período (`POST …/destinations/{id}/lock` com minutos). Enquanto bloqueado, destino não pode ser removido (agregado rejeita com `CopyDestinationConfigLocked`) — guarda deliberado contra mudanças impulsivas durante drawdown. Bloqueio expira automaticamente em seu timestamp.
- **Pré-alerta de consistência** (C10) — avisa (uma vez por dia UTC) quando **lucro diário** do destino atinge percentual configurado do equity de abertura do dia (`CopyConsistencyThresholdApproaching`), assim regra de consistência prop-firm respeitada *antes* de disparar. Lado lucro, independente de lockout lado perda; roda fora do mesmo baseline do dia que guarda de regra prop.
- **Filtro de tipo de ordem** — escolha exatamente quais tipos de ordem do master copiar: mercado, market-range, limite, stop, stop-limite (`CopyOrderTypes` flags; padrão todos). Seletividade estilo cMAM.
- **Copiar SL / Copiar TP** — espelha stop-loss / take-profit do master, ou gerencia proteção independentemente.
- **Copiar trailing stop**, **espelhar fechamento parcial**, **espelhar scale-in** — cada independentemente alternável.
- **Copiar expiração pendente** (padrão ativado) — espelha timestamp de expiração Good-Till-Date da ordem pendente do master.
- **Copiar slippage do master** (padrão ativado) — para ordens market-range + stop-limite, coloca ordem slave com exato slippage-em-pips do master (preço base tomado do spot ao vivo do slave).
- **Guardas**: max drawdown %, limite de perda diária, atraso máximo de cópia, filtro de slippage (pula cópia se preço slave se moveu além de N pips da entrada do master). **Atraso máximo de cópia** medido contra timestamp de servidor real do evento do master (`ExecutionEvent.ServerTimestamp`) via `TimeProvider` injetado: sinal mais antigo que lag-máximo configurado pulado, assim cópia antiga nunca colocada atrasada (anteriormente atraso sempre zero + guarda morto).
- **Normalização de precisão SL/TP** (M6) — stop-loss/take-profit copiados preços arredondados para precisão de dígito do símbolo **destino** antes de alterar, assim preço do master com precisão mais fina (ou incompatibilidade de dígito entre brokers) nunca dispara `INVALID_STOPLOSS_TAKEPROFIT` do servidor.
- **Disjuntor de circuit de rejeição / Follower Guard** (G8) — destino rejeitando `CopyDefaults.RejectionBudget` aberturas em fileira é **disparado**: nenhuma nova abertura para janela de esfriamento (`CopyDestinationTripped` alerta dispara), parando tempestade de rejeição de bater (account) prop-firm. Posições existentes ainda gerenciadas + fechadas enquanto disparado; disjuntor auto-reseta após esfriamento + cópia bem-sucedida limpa contador.
- **Teto de sanidade de lot** (C14) — tamanho máximo absoluto de cópia e/ou múltiplo-de-master cap. Cópia computada excedendo cap absoluto, ou excedendo `N×` tamanho de lot do próprio master, **duro-bloqueado** (aparece como pular `lot_sanity`, contado em `cmind.copy.skipped`) não colocado — defende contra classe catastroficamente-super-dimensionada (master 0.23-lot virando 3 lots em cada receptor via multiplicador desgovernado ou bug de arredondamento). Ambas dimensões padrão `0` (desativado).

## Confiabilidade & casos extremos

Motor construído para realidade que qualquer coisa pode falhar a qualquer hora:

- **Timeout de correlação de preenchimento de pendente slave** (C13) — slave pendente espelhado cujo master pendente desapareceu (nem descansando nem preenchido fresquinho) cancelado após timeout de correlação, assim cópia slave não pode preencher não correlacionada em posição não gerenciada (`CopyPendingTimedOut`). Resync também limpa órfão de pendente preenchido rotulado por id de ordem.
- **Fechar/achatar robusto** (M8) — fechando órfão em resync, ou achatando em brecha de guarda, tolera posição que broker já fechou (`POSITION_NOT_FOUND`): cada fechar roda independentemente, assim um id antigo nunca aborta resync ou deixa resto da conta não-achatado.

- **Iniciar com master já em trades** — ao iniciar host reconcilia + abre cópias para posições existentes do master.
- **Desconexão de conexão / desincronização** — na reconexão host reconcilia: abre cópias faltando, fecha órfãos, re-rotula pendentes. Nenhuma ordem duplicada.
- **Falha de colocação de ordem** — falha em um destino registrada, nunca bloqueia outros destinos.
- **Único token válido por cID** — cTrader invalida token de acesso antigo do cID no momento novo emitido. cMind troca token do host em execução **no local** (re-auth em socket ao vivo) assim cópia continua sem soltar fluxo. Veja [token lifecycle](token-lifecycle.md).

## Auditabilidade

Cada ação emite evento de log estruturado gerado por fonte (`LogMessages`) com id de perfil, cID de destino, ids de ordem/posição, + valores — ordem colocada/pulada (com razão), fechamento parcial, proteção aplicada, trailing aplicado, pendente colocada/alterada/cancelada, expiração espelhada, slippage market-range espelhado, token trocado, sumário resync. Este é o trilho de auditoria para conformidade + resolução de disputas.

Além de logs, motor emite **métricas OpenTelemetry** em medidor `cMind.Copy` (registrado em pipeline OTel compartilhado, exportado sobre OTLP / para Azure Monitor como resto): `cmind.copy.latency` (evento-master → dispatch, ms), `cmind.copy.dispatch.duration` (fan-out para todos destinos, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (marcado por destino), `cmind.copy.skipped` (marcado por razão), + `cmind.copy.failed`. Estes fazem regressão de latência/slippage mensurável, não apenas visível em linha de log — suite ao vivo asserta contra orçamento.

## API

- `GET /api/copy/profiles` — lista.
- `POST /api/copy/profiles` — cria (com ids opcionais de conta de destino).
- `GET /api/copy/profiles/{id}` — detalhe completo incluindo cada opção de destino.
- `POST /api/copy/profiles/{id}/destinations` — adiciona um destino com o conjunto completo de opções.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — remove.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — ciclo de vida.

## Testes

- **Unit** (`tests/UnitTests/CopyTrading`) — modos de dimensionamento, filtros de decisão, filtro de tipo de ordem, cópia de expiração, slippage market-range/stop-limite, toggles SL/TP, fechamento parcial, pendente alterar/cancelar, iniciar-com-aberto, desconectar→desincronizar→resync, troca de token no local, invalidação entre cID. Roda contra `FakeTradingSession`, simulador em memória cTrader-fiel.
- **Integração** (`tests/IntegrationTests/CopyLive`) — afinidade de nó/reivindicação de lease, propagação de versão de token no Postgres real.
- **E2E** (`tests/E2ETests`) — viagem de redonda de opção de destino através de API + UI, ciclo de vida completo.
- **Stress / DST** (`tests/StressTests`) — testes de simulação determinística: cargas randomizadas semeadas + injeção de falha (oscilação de socket, rejeição de ordem, rejeição market-range, rotação de token, morte de nó) dirigem `CopyEngineHost` para quiescência + asserta invariantes de convergência. Veja [testing/stress-testing.md](../testing/stress-testing.md). Esta suite apareceu + corrigiu corrida real de inicialização: `OnReconnected` fiado antes de carga de referência inicial + resync, assim oscilação de socket durante inicialização poderia rodar segundo resync concorrentemente + corromper dicionários de estado não-concorrente do host — carga de inicialização + primeiro resync agora rodam sob `_stateGate`.
- **Ao vivo** — contas cTrader demo reais; veja [testing/live-copy-trading.md](../testing/live-copy-trading.md).

Veja [dev-credentials.md](../testing/dev-credentials.md) para arquivo de credenciais única que camadas ao vivo + E2E leem.

## Controles de perfil e gerenciamento de destino

Iniciar/parar são botões de ícone em cada linha de perfil (desabilitados quando a ação não se aplica). Contas de origem e destino são mostradas por seu **número de conta**, nunca um id interno. Clicar em um perfil abre um **diálogo** para gerenciar suas contas de destino (adicionar/remover com configurações completas por-destino).
