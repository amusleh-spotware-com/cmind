---
title: 0001 — Strict DDD com um Core puro
description: Por que a lógica de domínio vive em agregados em um projeto Core com zero dependências de infraestrutura.
---

# 0001 — Strict DDD com um `Core` puro

## Contexto

Esta aplicação move dinheiro real. Regras de negócio espalhadas por endpoints, serviços de background e componentes Razor decaem em comportamento não testável e inconsistente — exatamente onde um bug custa capital do usuário.

## Decisão

A lógica de domínio vive **em agregados, objetos de valor e serviços de domínio** em `src/Core`, que compila com **zero dependências de infraestrutura** (sem EF, HttpClient, Docker ou ASP.NET). Endpoints, ferramentas MCP, componentes e `BackgroundService`s **orquestram** — eles nunca decidem. Regras:

- Sem setters públicos; mudanças de estado através de métodos que revelam intenção e guardam invariantes.
- Agregados referenciam um ao outro por **strong ID**, não por propriedade de navegação.
- Um `SaveChanges` muta **um** agregado; fluxos entre agregados usam eventos de domínio.
- Primitivos cruzando um limite de domínio são envolvidos em objetos de valor.
- Violações de invariantes lançam uma `DomainException` de Core, não uma exceção de framework.

## Consequências

- Regras de domínio são testáveis unitariamente sem banco de dados ou host web.
- A pureza de `Core` é aplicada automaticamente por `ArchitectureGuardTests` e falharia na compilação se quebrada.
- Há mais cerimônia (objetos de valor, strong IDs, eventos de domínio) do que em um modelo anêmico — este é o custo deliberado de manter as regras de movimento de dinheiro corretas e em um único lugar.
