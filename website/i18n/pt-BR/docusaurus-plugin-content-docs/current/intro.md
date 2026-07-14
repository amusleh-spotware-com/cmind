---
slug: /intro
title: Bem-vindo ao cMind
description: Uma introdução amigável ao cMind — a plataforma de operações de trading para cTrader, de código aberto e auto-hospedável.
sidebar_position: 1
---

# Bem-vindo ao cMind 👋

:::warning[Software alfa — não pronto para produção]
O cMind está em desenvolvimento ativo. Espere imperfeições, mudanças que quebram compatibilidade entre versões e recursos ainda em progresso. **Precisamos de testadores da comunidade, relatores de bugs e contribuidores iniciais** para ajudar a moldá-lo. Se você encontrar um problema, [reporte-o](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) — seu feedback do mundo real é a coisa mais valiosa que você pode contribuir agora.
:::

Então você quer criar robôs de trading, fazer backtest sem derreter seu notebook, executá-los em
várias máquinas, espelhar operações em uma dúzia de contas e ter uma IA de olho no risco enquanto você
dorme. **Você está exatamente no lugar certo.**

O cMind é uma **plataforma de operações de trading para cTrader, de código aberto e auto-hospedável**.
Pense nele como toda a sua mesa de operações — criação, execução, uma frota de computação, copy trading
e um núcleo de IA — reunidos em um app calmo, escuro e adaptado a celulares, que é seu de ponta a ponta.

:::tip[Em uma frase]
Construa → backtest → execute → copie suas estratégias de cTrader em escala, com IA integrada, nos seus
próprios servidores e sob a sua própria marca.
:::

## O que ele realmente faz?

| Você quer… | O cMind faz | Saiba mais |
|---|---|---|
| Escrever um cBot no navegador | IDE Monaco + modelos C#/Python, builds em sandbox | [Construir e backtest](./features/build-and-backtest.md) |
| Fazer backtest em várias máquinas | Uma frota de nós auto-recuperável escolhe a máquina menos ocupada | [Escalonamento](./deployment/scaling.md) |
| Copiar uma conta para muitas | Espelhamento robusto com ressincronização, sem operações duplicadas | [Copy trading](./features/copy-trading.md) |
| Deixar a IA fazer o trabalho pesado | Geração de estratégias, autorreparo, guardião de risco, post-mortems | [Núcleo de IA](./features/ai.md) |
| Ficar dentro das regras da prop firm | Acompanhamento de equity ao vivo + simulação de regras de desafio | [Prop-firm](./features/prop-firm.md) |
| Validar uma vantagem de backtest | Correção de overfitting PSR / DSR / t-stat | [Backtest Integrity Lab](./features/backtest-integrity.md) |
| Entender seus próprios hábitos | Detecção de vazamento comportamental + coach de IA | [Diário de trading](./features/trading-journal.md) |
| Acompanhar eventos macro para uma estratégia | Calendário ponto-no-tempo, bloqueio de notícias, API cBot | [Calendário econômico](./features/economic-calendar.md) |
| Pontuar a força macro de moedas | Perspectiva futura de IA em todos os pares | [Força de moeda](./features/currency-strength.md) |
| Proteger contas com 2FA | App autenticador TOTP + códigos de backup | [Autenticação de dois fatores](./features/two-factor-auth.md) |
| Deixar os proprietários ajustarem em tempo de execução | Toda opção white-label ao vivo em Configurações → Implantação | [Configurações do proprietário](./features/white-label-owner-settings.md) |
| Executá-lo em qualquer idioma | 23 idiomas incluindo RTL — a build falha se uma chave estiver faltando | [Localização](./features/localization.md) |
| Lançá-lo como *seu* produto | White-label completo: nome, cores, logotipo, favicon | [White-label](./features/white-label.md) |
| Executá-lo no seu celular | PWA instalável e mobile-first | [PWA](./features/pwa.md) |
| Controlá-lo por um cliente de IA | Servidor MCP integrado (HTTP + SSE) | [MCP](./features/mcp.md) |

## O caminho de 5 minutos ⏱️

Se você tem Docker e cinco minutos, já pode estar mexendo em uma instância real do cMind agora mesmo:

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
cp .env.example .env        # set OWNER_EMAIL + OWNER_PASSWORD
docker compose up --build
```

Depois abra **<http://localhost:8080>**, faça login e pronto. O passo a passo completo (com solução de
problemas para quando o Docker inevitavelmente tiver opiniões) está em
**[Executar localmente](./deployment/local.md)**.

## Novo por aqui? Siga a estrada de tijolos amarelos 🟡

1. **[Para quem é isto?](./audience.md)** — confirme que você é o nosso tipo de problema.
2. **[Executar localmente](./deployment/local.md)** — coloque uma instância real no ar.
3. **[Recursos](./features/README.md)** — o tour completo pelo que há dentro.
4. **[Implantar de verdade](./deployment/cloud.md)** — Docker, Kubernetes, Azure, AWS.
5. **[Torne-o seu](./white-label-for-business.md)** — aplique seu white-label para o seu negócio.
6. **[Contribuir](./contributing.md)** — PRs (humanos *e* assistidos por IA) são muito bem-vindos.

## Uma palavrinha sobre dinheiro 💸

O cMind movimenta **capital real**. Levamos isso a sério — cada mudança é entregue com testes unitários,
de integração e de ponta a ponta, incluindo caminhos de falha (conexões caídas, ordens rejeitadas, nós
mortos). Você também deveria levar a sério: **teste primeiro em uma conta demo** e leia as
[notas de conformidade](./features/compliance.md) antes de apontá-lo para algo real. Trading é
arriscado; este software é uma ferramenta, não aconselhamento financeiro.

Certo — chega de preâmbulo. Vamos construir algo. →
