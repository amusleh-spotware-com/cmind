---
title: 0005 — Odjemalec AI uporablja surovi HTTP, ne SDK Anthropic
description: Zakaj IAiClient klice Anthropic API preko tipanega HttpClient namesto uradnega SDK in zakaj je AI v celoti zaklenjen na ključ.
keywords:
  - Anthropic
  - AI
  - HTTP
  - SDK
  - varnost
---

# 0005 — Odjemalec AI uporablja surovi HTTP, ne SDK Anthropic

## Kontekst

Vsaka značilnost AI (generiranje strategije, samo-popravilo, varnostni stražar, post-mort) klice Anthropic
API. Odvisnost SDK doda površino, ki je ne nadziramo, povezuje naš razpored izdaj na njihova,
in skriva točen pogovor na žici, ki ga moramo razmisli za obstojnost in stroške.

## Odločitev

`IAiClient` klice Anthropic čez **surovi HTTP** preko tipanega `HttpClient` — namenoma **ne** SDK.
`AiFeatureService` je edini orkestrator, ki ga delijo spletne končne točke, MCP `AiTools` in
`AiRiskGuard`. Celotna površina je **zaklenjena na `AppOptions.Ai.ApiKey`**: brez ključa vsi
se vrnejo `AiResult.Fail` in aplikacija teče nespremenjeno.

## Posledice

- Ključ ni potreben za gradnjo, testiranje ali E2E — CI in lokalnega razvoja tečejo polno aplikacijo brez AI.
- Lastimo obliko zahteve/odgovora, politiko ponovnega poskusa/časa čakanja in racun žetonov eksplicitno.
- Nove značilnosti Anthropic morajo biti žičane ročno; zamenjamo priročnost za nadzor in manjšo
  površino odvisnosti. Glej referenco `claude-api` za trenutne ID modelov in parametre.
