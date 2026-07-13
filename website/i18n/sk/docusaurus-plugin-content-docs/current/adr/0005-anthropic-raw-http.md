---
title: 0005 — Klient AI používa raw HTTP, nie Anthropic SDK
description: Prečo IAiClient volá Anthropic API cez typovaný HttpClient namiesto oficiálneho SDK a prečo je AI úplne uzamknutý na kľúči.
---

# 0005 — Klient AI používa raw HTTP, nie Anthropic SDK

## Kontext

Každá funkcia AI (generovanie stratégie, samoreparácia, strážca rizika, post-mortemy) volá Anthropic
API. Závislosť SDK pridáva prechodný povrch, ktorý neovládame, viaže nás s ich tempom vydávania
a skrýva presný kontrakt drôtu, ktorý musíme pochopiť pre odolnosť a náklady.

## Rozhodnutie

`IAiClient` volá Anthropic cez **raw HTTP** cez typovaný `HttpClient` — zámyselne **nie** 
SDK. `AiFeatureService` je jednotný orchestrátor zdieľaný koncovými bodmi Web, MCP `AiTools` a
`AiRiskGuard`. Celý povrch je **uzamknutý na `AppOptions.Ai.ApiKey`**: bez kľúča, každá funkcia
vracia `AiResult.Fail` a aplikácia beží nezmenená.

## Dôsledky

- Kľúč sa nevyžaduje na budovanie, testovanie alebo E2E — CI a lokálny dev spúšťajú úplnú aplikáciu bez AI.
- Vlastníme tvar požiadavky/odpovede, politiku opakovania/timeout a účtovanie tokenov výslovne.
- Nové funkcie Anthropic musia byť zapojené ručne; obchodujeme pohodlie za kontrolu a menší
  povrch závislosti. Pozrite si referenciu `claude-api` pre aktuálne ID modelov a parametre.
