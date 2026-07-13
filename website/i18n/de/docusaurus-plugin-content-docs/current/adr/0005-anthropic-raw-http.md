---
title: 0005 – Der KI-Client verwendet rohes HTTP, nicht das Anthropic SDK
description: Warum IAiClient die Anthropic API über einen typisierten HttpClient aufruft, anstatt das offizielle SDK zu verwenden, und warum KI vollständig auf einem Schlüssel gated ist.
---

# 0005 – Der KI-Client verwendet rohes HTTP, nicht das Anthropic SDK

## Kontext

Jedes KI-Feature (Strategy Generation, Self-Repair, Risk Guard, Post-Mortems) ruft die Anthropic API auf. Eine SDK-Abhängigkeit fügt eine transitive Oberfläche hinzu, die wir nicht kontrollieren, koppelt unseren Release-Rhythmus an ihren und versteckt den exakten Wire-Contract, den wir für Resilienz und Kosten ableiten müssen.

## Entscheidung

`IAiClient` ruft Anthropic über **rohes HTTP** durch einen typisierten `HttpClient` auf – absichtlich **nicht** das SDK. `AiFeatureService` ist der einzige Orchestrator, der von Web-Endpoints, den MCP-`AiTools` und `AiRiskGuard` geteilt wird. Die ganze Oberfläche ist **gated auf `AppOptions.Ai.ApiKey`**: ohne Schlüssel gibt jedes Feature `AiResult.Fail` zurück und die App läuft unverändert.

## Konsequenzen

- Kein Schlüssel ist für Build, Test oder E2E erforderlich – CI und lokale Dev laufen die volle App ohne KI.
- Wir besitzen den Request/Response-Shape, Retry/Timeout-Richtlinie und Token-Rechnung explizit.
- Neue Anthropic-Features müssen von Hand verdrahtet werden; wir handeln Convenience für Kontrolle und eine kleinere Abhängigkeitsoberfläche ein. Siehe die `claude-api`-Referenz für aktuelle Model-IDs und Parameter.
