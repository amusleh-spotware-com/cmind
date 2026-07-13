---
slug: /features
title: Características — o passeio completo
description: Tudo que cMind pode fazer — copy trading, IA, compilar & backtest, guardas prop-firm, rótulo branco, PWA, MCP e mais.
sidebar_label: Visão Geral
---

# Características — o passeio completo 🧭

Bem-vindo ao grande passeio. cMind empacota *muito* em um aplicativo, então aqui está o mapa. Cada capacidade
tem seu próprio documento de aprofundamento — clique em qualquer coisa que esteja coçando.

## 🔁 Copy trading

A joia da coroa. Espelhe uma conta mestre em muitas e mantenha-as sincronizadas mesmo quando a internet
se comporta mal.

- **[Copy trading](./copy-trading.md)** — o núcleo: espelhamento, tipos de ordem, SL/TP, derrapagem, desSync/resync.
- **[Transparência de execução](./copy-execution-transparency.md)** — veja exatamente o que foi copiado, quando e por quê.
- **[Taxas de desempenho](./copy-performance-fees.md)** — cobre seu sinal, estilo de marca d'água alta.
- **[Marketplace de provedores](./copy-provider-marketplace.md)** — deixe traders descobrir e seguir provedores.
- **[Notificações](./copy-notifications.md)** — obtenha informações quando algo precisar de você.
- **[Recomendador de cópia por IA](./ai-copy-recommender.md)** — deixe a IA sugerir a quem copiar.
- **[Ciclo de vida do token Open API](./token-lifecycle.md)** — como cMind mantém exatamente um token válido por cID.

## 📊 Sua base de origem

- **[Dashboard](./dashboard.md)** — o centro de comando ao vivo, mobile-first: KPIs com sparklines, um gráfico de atividade, um anel de status, um feed ao vivo e (para administradores) saúde do cluster. Ele se atualiza automaticamente.

## 🧠 Núcleo de IA

Não é uma caixa de bate-papo afixada ao lado — IA que realmente *faz o trabalho*.

- **[Assistente de IA, agente, guarda de risco & alertas](./ai.md)** — geração de estratégia, compilações auto-reparáveis, um guarda de risco de fundo que pode parar bots automaticamente e alertas inteligentes.

## 🛠️ Compilar & executar

- **[Compilar & backtest cBots](./build-and-backtest.md)** — o IDE Monaco no navegador, templates C#/Python, compilações em sandbox e curvas de patrimônio ao vivo.
- **[Servidor MCP](./mcp.md)** — expor ferramentas do cMind sobre HTTP + SSE para que clientes IA possam acioná-lo.

## 🏢 Execute como negócio

- **[Rótulo branco / marca](./white-label.md)** — remarca cada superfície via config.
- **[Simulação de desafio prop-firm](./prop-firm.md)** — cumpra regras de perda diária, drawdown e alvo com patrimônio ao vivo.
- **[Alternâncias de característica](./feature-toggles.md)** — decida o que cada implantação/locatário vê.
- **[Conformidade / legal](./compliance.md)** — a trilha de auditoria e superfície legal.

## 📱 A experiência

- **[Aplicativo instalável (PWA)](./pwa.md)** — mobile-first, shell offline, adicionar à tela inicial.
- **[Sistema de design de interface e mobile-first](../ui-guidelines.md)** — os tokens de design e regras por trás da aparência.

## ⚙️ Sob o capô

Os bits operacionais que mantêm tudo funcionando:

- **[Frota de nós & descoberta](../operations/node-discovery.md)** — como nós se auto-registram e cicatrizam.
- **[Escala horizontal](../deployment/scaling.md)** — adicionar réplicas, nenhum coordenador externo necessário.
- **[Registro & auditoria](../operations/logging.md)** — logs estruturados + OpenTelemetry.
- **[Implantação](../deployment/local.md)** — execute-o em qualquer lugar.

:::note Mantendo docs honestos
Cada documento de característica é mantido em sincronismo com o código — altere o comportamento, atualize o doc, mesmo
commit. Se você nunca ver deriva, isso é um bug: por favor
[abra um problema](https://github.com/amusleh-spotware-com/cmind/issues/new/choose) ou envie um PR. 🙏
:::
