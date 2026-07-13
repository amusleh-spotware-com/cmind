---
title: 0002 — O estado de instância é TPH; uma transição substitui a entidade
description: Por que a id de uma instância muda conforme ela se move pelo seu ciclo de vida, e por que a id do container é a chave estável.
---

# 0002 — O estado de instância é TPH; uma transição substitui a entidade

## Contexto

Uma instância de execução/backtest move através de estados (pending → scheduled → starting → running → terminal). Modelamos o estado com **Table-Per-Hierarchy (TPH)** do EF Core: cada estado é um subtipo (`StartingRunInstance`, `RunningRunInstance`, …). A coluna de discriminador do TPH do EF **não pode mudar** em uma linha existente.

## Decisão

Uma transição de estado **substitui a entidade** por uma nova instância de subtipo em vez de mutar um campo de status. Como a linha é substituída, a **id da instância muda** entre starting → running → terminal. A **id do container é estável** e é transportada entre transições; o agente de nó HTTP é keyed pela id do container para status/relatório/parar/logs.

## Consequências

- Cada estado é um tipo distinto com apenas os campos e métodos válidos naquele estado — transições ilegais e acesso a campos sem sentido são erros de compilação, não verificações em runtime.
- Chamadores **não devem** cachear uma id de instância entre uma transição; use a id do container como o identificador estável para qualquer coisa que atravesse estados.
- A lógica de transição vive em `InstanceTransitions`; a mudança de id é intencional, não um bug.
