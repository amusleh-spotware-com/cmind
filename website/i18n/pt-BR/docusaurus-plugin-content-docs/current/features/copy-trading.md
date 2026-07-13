---
description: "Espelhe a conta cTrader mestre em uma+ contas escravas — entre brokers, entre cID — com controle por destino + reconciliação nível de dinheiro."
---

# Copy trading

Espelhe **mestre** conta cTrader em uma+ contas **escravas** — entre brokers, entre cID — com controle por destino + reconciliação nível de dinheiro.

## Conceitos

- **Perfil de cópia** — um mestre (`SourceAccountId`) + um+ **destinos**. Ciclo de vida: `Draft → Running → Paused → Stopped` (`Error` em falha). Raiz de agregado: `CopyProfile` (possui `CopyDestination`).
- **Destino** — uma conta escrava + conjunto de regras completo para como mestre é copiado para ele. Toda config por destino, então um mestre alimenta escravos conservadores + agressivos ao mesmo tempo.
- **Host de mecanismo de cópia** — trabalhador em execução para perfil (`CopyEngineHost`). Subscreve fluxo de execução mestre, aplica cada evento a cada destino.
- **Supervisor** — `CopyEngineSupervisor`, serviço de fundo em cada nó. Hospeda perfis atribuídos, auto-cicatriza entre cluster (veja [escala](../deployment/scaling.md)).

## O que é espelhado

| Evento mestre | Ação escrava |
|--------------|--------------|
| Posição aberta de mercado / intervalo de mercado | Abra uma cópia dimensionada (rotulada com id de posição de origem) |
| Ordem pendente limite / parada / parada-limite | Coloque a ordem pendente correspondente |
| Alteração de ordem pendente | Emende a ordem pendente espelhada no local |
| Cancelamento / vencimento de ordem pendente | Cancele a ordem pendente espelhada |
| Fechamento parcial | Feche a mesma proporção da posição escrava |
| Escala para dentro (aumento de volume) | Abra o volume adicionado (opt-in) |
| Mudança de stop-loss / trailing-stop | Emende a proteção da posição escrava |
| Fechamento completo | Feche a cópia escrava |

Cada cópia **rotulada com id de posição/ordem de origem**. Após reconectar, o host reconstrói estado de reconciliação: abre cópias que mestre mantém mas escravo falta, fecha "órfãos" escravos que mestre não mais mantém — **sem duplicar negociações**.

## Criando um perfil

O diálogo **New Profile** na página Copy Trading coleta tudo antecipadamente: nome do perfil, conta de origem (mestre), contas de destino (escravas) (multi-select com botão **Select all**; mestre escolhido excluído da lista escrava), + conjunto de opções por destino completo abaixo. Todas as entradas **validadas antes de salvar** — nome/origem/destino ausentes, parâmetro de dimensionamento não positivo, limites de lote negativos/inconsistentes, % de drawdown fora de alcance, nenhum tipo de ordem habilitado, filtro de símbolo vazio ou pares de mapa de símbolo malformados superfícies como lista de erro + bloco salvo. Na confirmação, perfil criado + cada escravo selecionado adicionado com configurações escolhidas.

As ações de linha respeitam o ciclo de vida: **Iniciar** habilitado apenas quando não em execução, **Parar** + **Pausar** apenas quando em execução, **Excluir** desabilitado durante execução + solicita confirmação antes de remover perfil + destinos.

## Opções por destino

Defina no diálogo Novo Perfil, no painel por destino da página Copy Trading ou via `POST /api/copy/profiles/{id}/destinations`:

- **Dimensionamento** (`MoneyManagementMode` + parâmetro): lote fixo, lote/multiplicador nocional, balanço proporcional/patrimônio/margem livre, risco fixo %, alavancagem fixa, auto-proporcional, **risco-%-de-parada** (M7). Mais limites de lote min/max + força-min-lote. **Risco-de-parada** dimensiona destino para que ele risco percentual configurado de *seu próprio* balanço, derivado de **distância stop-loss do mestre** (`mestre riscos 2% → escravo auto-riscos 2%`): `lots = balance×% ÷ (stopDistance ×
  contractSize)`. Mestre aberto **sem** stop-loss não tem distância para dimensionar contra → usa **lote de fallback de risco máximo** configurado (M7) se definido, senão pulado (`no_stop_loss`) não adivinhado. **Patrimônio** proporcional/**margem livre** tamanho fora **patrimônio** real da conta (`balance + Σ floating P&L`, derivado por cTrader Open API que não entrega patrimônio), não simples balanço — então mestre sentado em lucro/perda aberto dimensiona cópias certo. Margem usada não exposta por API reconciliação, então margem livre tratada como patrimônio (proxy honest-disponível-fundos); outros modos leem balanço + pulam extra rodada round-trip de revaluação.
- **Filtro de direção**: ambos / apenas longo / apenas curto. **Reverter**: invertir lado (+ trocar SL↔TP) para cópia contrária.
- **Apenas gerenciar** (Ignorar-Negociações-Novas / Apenas-Fechar): espelhando closes, closes parciais + mudanças de proteção em posições já copiadas, mas abrindo **nenhuma** nova posição/ordem pendente (pulada `manage_only`). Use para diminuir destino sem cortar cópias existentes.
- **Sincronizar-Aberto-ao-iniciar** / **Sincronizar-Fechado-ao-iniciar** (padrão ativado): na **primeira** ressincronização do perfil, se abre cópias para posições pré-existentes do mestre, + se fecha cópias que mestre fechou enquanto perfil parado. Ambos aplicam apenas na inicialização — reconectar mid-run sempre reconcilia totalmente para que desSync recupere independentemente.
- **Mapa de símbolo** + **filtro de símbolo** (whitelist / blacklist). Cada entrada de mapa de símbolo carrega **multiplicador de volume por símbolo** opcional (substituição por símbolo cMAM) dimensionando tamanho de cópia para esse símbolo no topo do dimensionamento do destino (1 = sem alteração). Mapa inteiro importa/exporta como **CSV** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; colunas `Source,Destination,VolumeMultiplier`) — cada linha validada através de objetos de valor de domínio, então arquivo malformado não pode produzir mapa inválido.
- **Janela de horário de negociação** (C18) — janela UTC diária por destino (`start`/`end` minutos-do-dia, fim exclusivo; `start == end` = o dia todo). Novas aberturas fora da janela puladas (`trading_hours`); janela com `start > end` envolve passado meia-noite (por exemplo 22:00–06:00). Posições existentes permanecem gerenciadas.
- **Filtro de rótulo de origem** (C18, equivalente cTrader de filtro de número mágico MT) — quando definido, copie apenas negociações de mestre cujo rótulo corresponda **exatamente** (por exemplo, negociações de um bot ou apenas rótulo manual); senão pulado (`source_label`). Vazio = copie tudo. Carregado em `ExecutionEvent.SourceLabel` da posição/ordem de mestre `TradeData.Label`, honrado na ressincronização também.
- **Proteção de conta** (ZuluGuard / Proteção Global de Conta) — observe **patrimônio ao vivo** do destino (`balance + Σ floating P&L`, consultado a cada `CopyDefaults.EquityGuardInterval`) contra piso `StopEquity` e/ou teto `TakeEquity` opcional. Na violação, aplique modo: **CloseOnly** (pare novas cópias, mantenha gerenciando existentes), **Frozen** (pare abertura), **SellOut** (feche **toda** cópia no destino imediatamente). Uma vez disparado, destino preso — sem novas aberturas até host reiniciar — + alerta `CopyAccountProtectionTriggered` levantado. `SellOut` requer `StopEquity`; `TakeEquity` deve sentar acima `StopEquity`. **Nenhuma caveat de garantia:** vender usa execução de mercado — como equivalente de cada concorrente, não pode garantir preço de preenchimento em mercado rápido/gapped.
- **Botão de pânico Flatten-All** (C8) — `POST /api/copy/profiles/{id}/flatten` fecha imediatamente **toda** posição copiada em cada destino + bloqueia contra novas aberturas. Roteado entre processos: API define sinalizador, supervisor entrega ao host em execução (reutilizando canal de rotação de token), que achata no local; sinalizador liberado para dispara exatamente uma vez (alerta `CopyFlattenAll`). Usuário então pausa/para perfil.
- **Guarda de regra prop-firm** (C7) — cumprimento que usuários copiers prop-firm pedem. Por destino, **limite de perda diária** (perda de patrimônio de abertura do dia) e/ou limite **de drawdown de rastreamento** (perda de patrimônio de pico em execução), ambos em moeda de depósito. Na violação destino **achatado automaticamente** (toda cópia fechada) + **bloqueado** resto do dia UTC (novas aberturas puladas `prop_lockout`); alerta `CopyPropRuleBreached` dispara. Bloqueio limpa quando o dia UTC passa (nova linha de base/pico tomado). Compartilha mesma pesquisa de patrimônio ao vivo que proteção de conta.
- **Jitter de execução** (C11, off por padrão) — atraso aleatório `0..N` ms antes de colocar cada cópia, para descontelar timestamps de ordem quase idênticos entre contas **próprias** do usuário. **Caveat de conformidade:** auxílio para empresas prop que *permitem* cópia — **não** ferramenta para evitar empresa que proíbe; ficar dentro das regras da sua empresa é sua responsabilidade.
- **Bloqueio de config** (C9) — congele configurações de destino por período (`POST …/destinations/{id}/lock` com minutos). Enquanto bloqueado, destino não pode ser removido (agregado rejeita com `CopyDestinationConfigLocked`) — protetor deliberado contra mudanças impulsivas durante drawdown. O bloqueio expira automaticamente em seu carimbo de data/hora.
- **Pré-alerta de consistência** (C10) — avise (uma vez por dia UTC) quando **lucro diário** do destino atinge percentual configurado do patrimônio de abertura do dia (`CopyConsistencyThresholdApproaching`), então regra de consistência prop-firm respeitada *antes* que dispare. Lado de lucro, independente de bloqueio lado perda; executa fora mesma linha de base do dia que guarda de regra prop.
- **Filtro de tipo de ordem** — escolha exatamente quais tipos de ordem de mestre copiar: mercado, intervalo de mercado, limite, parada, parada-limite (`CopyOrderTypes` flags; padrão todas). Seletividade estilo cMAM.
- **Copiar SL / Copiar TP** — espelhe stop-loss/take-profit do mestre ou gerencie proteção independentemente.
- **Copiar parada de rastreamento**, **espelhe fechamento parcial**, **espelhe escala para dentro** — cada independentemente alternável.
- **Copiar vencimento pendente** (padrão ativado) — espelhe carimbo de data/hora de vencimento Good-Till-Date da ordem pendente do mestre.
- **Copiar slippage do mestre** (padrão ativado) — para intervalo de mercado + ordens stop-limit, coloque ordem escravo com slippage exato do mestre em pontos (preço base tomado do spot ao vivo do escravo).
- **Guardas**: max drawdown %, limite de perda diária, atraso máximo de cópia, filtro de slippage (pule cópia se preço de escravo se moveu além de N pips de entrada de mestre). **Atraso máximo de cópia** medido contra carimbo de data/hora real do servidor de evento mestre (`ExecutionEvent.ServerTimestamp`) via `TimeProvider` injetado: sinal mais antigo do que max-lag configurado pulado, então cópia obsoleta nunca colocada tarde (atraso previamente sempre zero + guarda morto).
- **Normalização de precisão SL/TP** (M6) — preços stop-loss/take-profit copiados arredondados para precisão de dígito do símbolo **destino** antes de emendar, então preço mestre em precisão mais fina (ou desfasamento de dígito entre brokers) nunca dispara `INVALID_STOPLOSS_TAKEPROFIT` do servidor.
- **Disjuntor de rejeição / Guarda de Seguidor** (G8) — destino rejeitando `CopyDefaults.RejectionBudget` aperturas em uma fileira é **disparado**: sem novas aberturas para janela de cooldown (alerta `CopyDestinationTripped` dispara), parando tempestade de rejeição de martelar (prop-firm) conta. Posições existentes ainda gerenciadas + fechadas enquanto disparadas; disjuntor auto-reseta após cooldown + cópia bem-sucedida limpa contador.
- **Teto de sanidade de lote** (C14) — tamanho máximo absoluto de cópia e/ou múltiplo-do-mestre cap. Cópia computada excedendo limite absoluto, ou excedendo `N×` tamanho de lote do próprio mestre, **blocado rigidamente** (superfície como pulo `lot_sanity`, contado em `cmind.copy.skipped`) não colocado — defende contra classe de super-tamanho catastrófico (mestre 0.23-lote virando em 3 lotes em cada receptor via multiplicador disparatado ou bug de arredondamento). Ambas dimensões padrão `0` (off).

## Confiabilidade & casos extremos

Motor construído para realidade que qualquer coisa pode falhar anytime:

- **Timeout de correlação de preenchimento pendente de escravo** (C13) — escravo pendente espelhado cujo mestre pendente desapareceu (nem descansando nem preenchido recentemente) cancelado após timeout de correlação, então cópia escravo não pode preencher não correlacionada em posição desgerenciada (`CopyPendingTimedOut`). Ressincronização também limpa órfão de pendente preenchido com id-rotulado.
- **Fechamento/achatamento robusto** (M8) — fechando órfão em ressincronização, ou achatando na violação de guarda, tolera posição broker já fechada (`POSITION_NOT_FOUND`): cada fechamento executa independentemente, então um id obsoleto nunca aborta ressincronização ou deixa resto de conta desachatado.

- **Iniciar com mestre já em negociações** — na inicialização, host reconcilia + abre cópias para posições existentes do mestre.
- **Queda de conexão / desSync** — na reconexão, host reconcilia: abre cópias faltantes, fecha órfãos, re-rotula pendentes. Sem ordens duplicadas.
- **Falha de colocação de ordem** — falha em um destino registrada, nunca bloqueia outros destinos.
- **Token único válido por cID** — cTrader invalida o token de acesso antigo do cID no momento que novo é emitido. cMind troca token do host em execução **no local** (re-auth em soquete ao vivo) para que copiar continue sem largar fluxo. Veja [ciclo de vida do token](token-lifecycle.md).

## Auditabilidade

Cada ação emite evento de log estruturado, gerado por fonte (`LogMessages`) com id de perfil, cID de destino, ids de ordem/posição, + valores — ordem colocada/pulada (com razão), fechamento parcial, proteção aplicada, trailing aplicado, pendente colocado/emendado/cancelado, vencimento espelhado, slippage de intervalo de mercado espelhado, token trocado, resumo de ressincronização. Esta é a trilha de auditoria para conformidade + resolução de disputa.

Ao lado dos logs, o motor emite **métricas OpenTelemetry** em medidor `cMind.Copy` (registrado em pipeline OTel compartilhado, exportado sobre OTLP / para Azure Monitor como resto): `cmind.copy.latency` (evento mestre → despacho, ms), `cmind.copy.dispatch.duration` (leque para todos destinos, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (rotulado por destino), `cmind.copy.skipped` (rotulado por razão), + `cmind.copy.failed`. Estes tornam regressão de latência/slippage mensurável, não apenas visível em linha de log — suite ao vivo afirma them contra orçamento.

## API

- `GET /api/copy/profiles` — lista.
- `POST /api/copy/profiles` — criar (com ids de conta de destino opcionais).
- `GET /api/copy/profiles/{id}` — detalhe completo incl. cada opção de destino.
- `POST /api/copy/profiles/{id}/destinations` — adicione um destino com o conjunto de opção completo.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — remova.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — ciclo de vida.

## Testes

- **Unidade** (`tests/UnitTests/CopyTrading`) — modos de dimensionamento, filtros de decisão, filtro de tipo de ordem, cópia de vencimento, slippage de intervalo de mercado/stop-limit, toggles de SL/TP, fechamento parcial, pendente emendar/cancelar, iniciar com aberto, desconectar→desSync→ressincronizar, troca de token no local, invalidação entre cID. Executa contra `FakeTradingSession`, simulador na memória fiel a cTrader.
- **Integração** (`tests/IntegrationTests/CopyLive`) — afinidade de nó/reclamação de arrendamento, propagação de versão de token em Postgres real.
- **E2E** (`tests/E2ETests`) — viagem de opção de destino através de API + interface, ciclo de vida completo.
- **Stress / DST** (`tests/StressTests`) — teste de simulação determinística: cargas de trabalho aleatorizadas semeadas + injeção de falha (flap de soquete, rejeição de ordem, rejeição de intervalo de mercado, rotação de token, morte de nó) dirigem `CopyEngineHost` para quiescência + afirmam invariantes de convergência. Veja [testing/stress-testing.md](../testing/stress-testing.md). Esta suite superficial + consertou corrida real de inicialização: `OnReconnected` conectado antes de carga de referência inicial + ressincronização, então flap de soquete durante inicialização poderia executar ressincronização segunda concorrentemente + corromper dicionários de estado não-concorrente do host — carga de inicialização + primeira ressincronização agora executam sob `_stateGate`.
- **Ao vivo** — contas demo cTrader real; veja [testing/live-copy-trading.md](../testing/live-copy-trading.md).

Veja [dev-credentials.md](../testing/dev-credentials.md) para arquivo de credenciais único ao vivo + tiers E2E leem.

## Controles de perfil e gerenciamento de destino

Iniciar/parar são botões de ícone em cada linha de perfil (desabilitados quando a ação não se aplica). Contas de origem e
destino são mostradas pelo **número da conta**, nunca um id interno. Clicar em um perfil
abre um **diálogo** para gerenciar suas contas de destino (adicionar/remover com configurações completas por destino).
