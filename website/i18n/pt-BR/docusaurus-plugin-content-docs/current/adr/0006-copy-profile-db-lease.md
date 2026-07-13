---
title: 0006 — Hospedagem de cópia é coordenada por um lease atômico de DB
description: Por que perfis de cópia são reivindicados através de um lease atômico do Postgres em vez de um coordenador dedicado, e como isso previne double-copying.
---

# 0006 — Hospedagem de cópia é coordenada por um lease atômico de DB

## Contexto

Um perfil de cópia em execução deve ser hospedado por **exatamente um** nó — dois hosts no mesmo perfil significa que cada negociação de fonte é espelhada duas vezes (dinheiro real perdido). Nós vêm e vão (scaling, crashes, rolling updates), e não queremos um serviço coordenador separado para rodar e manter vivo.

## Decisão

Cada `CopyEngineSupervisor` reivindicava perfis com um **lease atômico de DB** na tabela `CopyProfiles`:

- **Reclamação** — um `ExecuteUpdate` atômico (ou `FOR UPDATE SKIP LOCKED` ao limitar por nó) leva perfis que não são atribuídos *ou* cujo lease expirou. Atomicidade significa que dois supervisores concorrentes nunca reivindicam a mesma linha.
- **Renovação** — um nó vivo atualiza seu lease a cada ciclo, então mantém sua reclamação.
- **Reclamação** — um lease de nó crasheado expira, e um sobrevivente pega o perfil em seu próximo ciclo (auto-cura). No desligamento graciável, o nó **libera** seus leases imediatamente para que o failover seja rápido.
- **Watchdog** — um host cuja tarefa saiu enquanto o perfil ainda é nosso é reiniciado.
- A reconciliação é jittered para evitar uma manada trovejante de `UPDATE`s na escala.

## Consequências

- Nenhum coordenador autônomo para implantar ou manter saudável — Postgres é a única fonte de verdade.
- Double-copying é prevenido pela atomicidade em nível de linha, não por bloqueio em nível de aplicação.
- A latência de failover é limitada pelo TTL do lease (menos o caminho rápido de liberação graciável).
- Este é o caminho do dinheiro; é guarded pelo conjunto de testes de estresse determinístico (DST) — nunca enfraquece um cenário DST para fazê-lo passar.
