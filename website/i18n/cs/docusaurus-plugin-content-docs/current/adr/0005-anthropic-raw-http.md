---
title: 0005 — Klient AI používá surový HTTP, ne SDK Anthropic
description: Proč IAiClient volá Anthropic API přes typovaný HttpClient místo oficiální sady SDK a proč je AI plně chráněna klíčem.
---

# 0005 — Klient AI používá surový HTTP, ne SDK Anthropic

## Kontext

Každá funkce AI (generování strategie, vlastní oprava, hlídač rizika, posmrtné analýzy) volá Anthropic
API. Závislost sady SDK přidává přechodný povrch, který nemáme pod kontrolou, váže naši rychlost vydání k jejich
a skrývá přesný kabel smluvní, který potřebujeme rozumět pro odolnost a náklady.

## Rozhodnutí

`IAiClient` volá Anthropic přes **surový HTTP** prostřednictvím typovaného `HttpClient` — záměrně **ne** sady
SDK. `AiFeatureService` je jediný orchestrátor sdílený webovými koncovými body, MCP `AiTools` a
`AiRiskGuard`. Celý povrch je **chráněn na `AppOptions.Ai.ApiKey`**: bez klíče každá funkce
vrátí `AiResult.Fail` a aplikace běží beze změny.

## Důsledky

- Pro stavbu, test nebo E2E se nevyžaduje žádný klíč — CI a místní vývoj spouštěj plnou aplikaci bez AI.
- Vlastníme tvar požadavku/odpovědi, zásadu opakování/vypršení a účtování tokenů výslovně.
- Nové funkce Anthropic musí být napojeny ručně; obchodujeme pohodlí za kontrolu a menší
  povrch závislosti. Aktuální ID modelů a parametry najdete v odkazu `claude-api`.
