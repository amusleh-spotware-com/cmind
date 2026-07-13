---
title: 0003 — Nós cTrader CLI são HTTP + JWT, sem SSH/shell
description: Por que agentes de nó remoto expõem apenas uma API HTTP com JWTs de curta vida e nunca um shell.
---

# 0003 — Nós cTrader CLI são HTTP + JWT, sem SSH/shell

## Contexto

Containers de backtest/execução rodam em hosts remotos. A abordagem óbvia — SSH e rodando docker — dá à aplicação principal execução arbitrária de código remoto e credenciais de longa vida em cada nó. Este é um grande raio de impacto para um sistema que executa cBots não confiáveis de usuários.

## Decisão

Cada host remoto roda um **agente HTTP** `CtraderCliNode` independente com **sem SSH e sem shell**. A aplicação principal chama o agente sobre HTTP; cada solicitação carrega um **JWT HS256** de curta vida (5 minutos, `iss=app-main` / `aud=app-node`) assinado com o segredo daquele nó. O agente:

- executa apenas imagens correspondentes a `AllowedImagePrefix` (com um limite de caminho para que `ghcr.io/spotware` não possa corresponder `ghcr.io/spotware-evil/...`);
- executa docker via `ArgumentList` — nunca uma string shell;
- é **stateless**, encontrando containers pelo rótulo `app.instance`;
- auto-registra e faz heartbeat para `POST /api/nodes/register`; a aplicação principal faz upsert do `CtraderCliNode` **por nome**, então um nó sobrevive mudanças de IP.

## Consequências

- Um token de solicitação vazado expira em minutos; não há credencial shell permanente para roubar.
- A capacidade do agente é limitada para "executar uma imagem permitida" — não pode ser transformada em um shell remoto geral.
- A identidade do nó é baseada em nome, então reprovisionar um nó com um novo IP não deixa seu histórico órfão.
