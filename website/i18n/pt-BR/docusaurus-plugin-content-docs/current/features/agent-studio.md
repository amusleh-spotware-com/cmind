---
description: "Agent Studio — crie agentes de trading orientados por persona, sem codigo, com personagem e arquetipo que gerenciam contas toward seus objetivos sob o Nucleo de Autonomia e Seguranca (envelope de risco, disjuntor, botao de panico, consentimento de disclaimer versionado)."
---

# Agent Studio

Agent Studio permite criar um **agente de trading com personagem** — sem codigo — e dar a ele gerenciamento de
suas contas toward objetivos mensuraveis. Um agente e como um cBot orientado por personalidade: voce escolhe um arquetipo
e atitude, define os guardrails, e ele executa sob o **Nucleo de Autonomia e Seguranca**.

Abra **AI → Agent Studio** (`/agent-studio`).

## Crie um agente

O dialogo **New agent** coleta, sem codigo:

- **Nome** e **arquetipo** — Scalper, Day Trader, Swing Trader, Position Trader, News Trader,
  Contrarian, Mean Reversion ou Breakout/Momentum. Cada preset fixa uma cadencia e postura sensata.
- **Atitude** — deslizadores de agressividade, paciencia e tendencia.
- **Nivel de autonomia** — **Advisory** (apenas propoe) ou **Approval-gated** (age apenas apos sua
  aprovacao por-acao). **Full Auto** (sem aprovacao por-negociacao) adicionalmente requer um **envelope de risco**
  e aceitacao do disclaimer de risco antes de poder armar.

A persona compila **deterministicamente** no prompt de sistema do agente (nao LLM o autora), entao a
mesma configuracao sempre produz as mesmas instrucoes — reprodivel e auditavel.

## A lista de agentes

Todo agente mostra em uma tabela de sala de controle: **qual agente, seu tipo, quantas contas gerencia, seus
objetivos, status de execucao e ultima acao**, com controles **Start / Stop / Kill**. O botao Kill para um
agente em execucao imediatamente.

## Seguranca e um invariante de dominio, nao uma configuracao

Tudo que toca dinheiro passa pelo **Nucleo de Autonomia e Seguranca**:

- **Envelope de risco** — limites rigidos por ordem (maxima perda diaria, exposicao aberta, tamanho de posicao, alavancagem,
  perdas consecutivas, ordens/hora, simbolos permitidos). Cada ordem e validada contra ele antes do dispatch;
  uma violacao e recusada, nao ajustada. Necessario antes de um agente chegar a Full Auto.
- **Disjuntor** — halta determinasticamente novos riscos em sequencia de perdas, violacao de perda diaria, **violacao hard de meta de performance**, ou **indisponibilidade do provedor AI** (um modelo down ou alucinando nunca abre
  posicoes frescas).
- **Consentimento de disclaimer versionado** — uma aceitacao unica e versionada e necessaria para armar Full Auto
  (consentimento exigido por lei, nao aprovacao por-negociacao); mudar o disclaimer força re-aceite.
- **Botao de panico** — parada idempotente de emergencia em todo agente em execucao.

## Objetivos

Deixe um agente com **objetivos mensuraveis** — ex. *mantenha max drawdown abaixo de 4%*, *fator de lucro pelo menos
1.5*, *taxa de acerto >= 55%*. Cada alvo e **Hard** (um guardrail — uma violacao dispara o disjuntor) ou
**Soft** (orienta raciocinio apenas), avaliado como On-track / At-risk / Breached.

## O pipeline de decisao

Uma vez iniciado, um agente executa um **loop 24/7 supervisionado** (`AgentRuntimeService`). A cada tick, para cada
conta gerenciada, ele: le o **estado de conta deterministico** (verdade fundamental, nunca memoria do modelo);
pede a decisao do motor de decisao; passa pelo **gate de seguranca** (`AgentDecisionProcessor`) —
nivel de autonomia → disjuntor → envelope de risco; escreve um registro append-only **`AgentDecisionRecord`**; e
para ou executa conforme o gate direciona. O loop e **isolado por falta** (falha de um agente nunca toca
outro ou o host) e **seguro por padrao**: e inerte a menos que AI esteja configurada *e*
`App:Ai:AgentRuntimeEnabled` esteja definido, e nunca abre risco fresco enquanto o provedor AI esta indisponivel.

- **Gate de aprovacao** — ordem proposta de um agente **Approval-gated** e registrada como **Pending** e nao faz
  nada ate o dono a aprove (`POST /api/agent-studio/{id}/decisions/{seq}/approve` ou
  `/reject`); **Full Auto** passa pelo envelope sem aprovacao por-negociacao; **Advisory** apenas
  propoe.
- **Livro de auditoria** — cada decisao e reproduzivel: raciocinio (XAI), a evidencia que citou, o veredito do gate,
  a intent da ordem e se executou, em `GET /api/agent-studio/{id}/decisions`.
- **Mesa de pesquisa** — um debate multi-agente sob demanda: analistas Alpha/Sentiment/Technical/Risk cada dao
  uma visao e um Revisor sintetiza uma proposta (`POST /api/agent-studio/{id}/debate`).
- **Memoria** — o agente lembra cada decisao e recalls memoria recente em seu proximo prompt para
  continuidade (`GET /api/agent-studio/{id}/memory`).

Cada linha da lista de agentes **Details** abre o feed de decisoes do agente (com Approve/Reject em ordens pendentes),
sua memoria, e uma aba Run-debate.

## Escopo

Enviado: o ciclo de vida completo do agente, o gate de seguranca deterministico, o runtime 24/7, o
gate de aprovacao humano-no-loop, o livro de auditoria, e a **integracao live com cTrader Open API** — a
store de estado da conta (le saldo real, posicoes e exposicao aberta em lotes) e o executor de ordens (coloca ordens de mercado reais, lotes→volume via tamanho de lote do simbolo), ambos resolvendo credenciais OAuth de cada conta gerenciada e degradando suavemente quando uma conta nao esta vinculada. **Requer a chave da API Anthropic** para o modelo gerar ordens (ate entao o motor segura); ainda por vir sao metas de debate multi-agente e memoria/reflexao em camadas. O runtime e desligado a menos que `App:Ai:AgentRuntimeEnabled` esteja definido, entao trading live
acontece apenas em um opt-in explicito e totalmente consentido.

## Contas gerenciadas e edicao

Ao criar um agente, voce escolhe a(s) conta(s) de trading que ele gerencia (necessario antes de poder iniciar).
Todo agente pode ser **editado** depois (nome, temperamento, autonomia e contas gerenciadas) pelo icone de lapis
na linha da tabela de agentes. Controles de ciclo de vida (details, edit, start, stop, kill) sao botoes de icone,
cada um desabilitado em estados onde a acao nao se aplica.
