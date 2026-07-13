---
title: 0005 — O cliente AI usa HTTP bruto, não o SDK Anthropic
description: Por que IAiClient chama a API Anthropic através de um HttpClient tipado em vez do SDK oficial, e por que AI é totalmente controlado por uma chave.
---

# 0005 — O cliente AI usa HTTP bruto, não o SDK Anthropic

## Contexto

Cada recurso de AI (geração de estratégia, auto-reparação, proteção de risco, post-mortems) chama a API Anthropic. Uma dependência do SDK adiciona uma superfície transitiva que não controlamos, acopla nosso ciclo de lançamento ao deles e oculta o contrato exato de wire que precisamos raciocinar sobre resiliência e custo.

## Decisão

`IAiClient` chama Anthropic através de **HTTP bruto** através de um `HttpClient` tipado — deliberadamente **não** o SDK. `AiFeatureService` é o único orquestrador compartilhado por endpoints Web, as `AiTools` MCP e `AiRiskGuard`. Toda a superfície é **controlada por `AppOptions.Ai.ApiKey`**: sem uma chave, cada recurso retorna `AiResult.Fail` e a aplicação roda sem alterações.

## Consequências

- Nenhuma chave é necessária para compilação, teste ou E2E — CI e desenvolvimento local executam a aplicação completa sem AI.
- Possuímos a forma de solicitação/resposta, política de retry/timeout e contabilização de tokens explicitamente.
- Novos recursos Anthropic devem ser conectados manualmente; trocamos conveniência por controle e uma superfície de dependência menor. Veja a referência `claude-api` para ids de modelo atuais e parâmetros.
