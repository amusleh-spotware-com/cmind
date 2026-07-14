---
slug: /for-traders
title: cMind para traders cTrader
description: Por que um trader cTrader deve auto-hospedar cMind — possua sua stack e dados, autor, backtest, execute e monitore cBots em um console AI-powered, no seu laptop, VPS ou telefone.
keywords:
  - cTrader
  - Trading algorítmico
  - Plataforma de trading auto-hospedada
  - Backtesting de cBot
  - Bots de trading AI
  - Software de trading open source
sidebar_position: 5
---

# cMind para traders cTrader 📈

Você já negocia no cTrader. Você já malabariza um editor de código, um backtester, um VPS e três abas de navegador. **cMind colapsa tudo isso em um console escuro, amigável ao teclado que você executa você mesmo** — e é open source, então nada sobre sua vantagem, suas estratégias ou suas credenciais nunca sai da sua caixa.

:::tip[TL;DR]
Auto-hospede cMind em um laptop, um VPS barato ou um servidor doméstico. Autor, backtest, execute e monitore cBots em um lugar, com um núcleo de AI fazendo as tarefas. → [Execute em 5 minutos](./deployment/local.md)
:::

## Por que auto-hospedar em vez de um serviço hospedado?

- **Possua sua stack e seus dados.** Seus cBots, credenciais, tokens e histórico de equity vivem em **sua** infraestrutura — sem terceiros, sem lock-in, sem email "estamos descontinuando este produto".
- **É genuinamente seu para mudar.** C# 14 / .NET 10, strict DDD, EF Core + PostgreSQL, um servidor MCP — tudo open source e hackeável. Faça fork, estenda, envie uma PR.
- **Sem paywall por recurso.** Traga sua própria chave de AI para qualquer provedor; cada recurso de AI está ativado.

Prefere não executar servidores você mesmo? Uma empresa de hospedagem pode executar um cMind gerenciado para você — veja [Para provedores de cloud e VPS](./for-cloud-providers.md).

## Um console, sem malabarismo de abas

- **Autor** em um IDE real Monaco (o editor VS Code), com modelos **e** C# Python e `dotnet build` sandbox em containers descartáveis. → [Build e backtest](./features/build-and-backtest.md)
- **Backtest** em uma frota de nós e veja curvas de equity fluxuarem ao vivo.
- **Execute** estratégias ao vivo e **monitore** a partir de um painel. → [Painel](./features/dashboard.md)
- **Copie** uma conta mestre para muitas contas em brokers e IDs cTrader, com reconciliação que sobrevive conexões caídas e tokens rotativos. → [Copy trading](./features/copy-trading.md)

## AI que faz tarefas, não conversa fiada

Traga sua própria chave de API (qualquer provedor suportado — cloud ou um modelo local) e obtenha texto simples → um cBot compilando real com um loop de auto-reparação, ajuste de parâmetros, post-mortems de backtest e um guarda de risco que pode auto-parar um bot malcomportado. → [Conheça o núcleo de AI](./features/ai.md)

## Ferramentas de nível institucional, para um

O mesmo rigor que uma mesa paga, na sua própria caixa:

- [Backtest integrity](./features/backtest-integrity.md) · [Position sizing](./features/position-sizing.md)
- [Strategy health](./features/strategy-health.md) · [Regime lab](./features/regime-lab.md)
- [Execution TCA](./features/execution-tca.md) · [Trading journal](./features/trading-journal.md)
- [Agent Studio](./features/agent-studio.md) · [Contrarian positioning](./features/contrarian-positioning.md)

## Roda onde você faz

Comece no seu laptop com `docker compose up`, aumente para um VPS barato ou um servidor doméstico quando estiver pronto, e verifique seus bots no seu telefone — cMind é um [PWA](./features/pwa.md) instalável, mobile-first. → [Execute localmente](./deployment/local.md)

Quer que seu cliente de AI o dirija? Há um [servidor MCP](./features/mcp.md) integrado.

## Ajude a melhorar

cMind é open source e licenciado com MIT — o roadmap é com forma de comunidade:

- Abra issues e solicitações de recurso, e vote no que importa.
- Adicione modelos de cBot, adaptadores de provedor de AI ou traduções de UI.
- Envie PRs — três níveis de teste (unit + integration + E2E) e strict DDD mantêm o padrão alto, e o [Guia de contribuição](./contributing.md) o guia através disso.

Pronto? → [Leia a introdução](./intro.md) depois [execute localmente](./deployment/local.md).
