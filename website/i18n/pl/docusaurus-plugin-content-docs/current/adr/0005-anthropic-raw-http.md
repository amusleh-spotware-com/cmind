---
title: 0005 — The AI client uses raw HTTP, not the Anthropic SDK
description: Dlaczego IAiClient wywoła interfejs API Anthropic przez typowy HttpClient zamiast oficjalnego SDK i dlaczego AI jest w pełni zależne od klucza.
---

# 0005 — Klient AI używa surowego HTTP, nie SDK Anthropic

## Kontekst

Każda funkcja AI (generowanie strategii, samonaprawa, strażnik ryzyka, sekcje zastrzeżeń) wywoła interfejs API Anthropic. Zależność SDK dodaje przechodnie powierzchnie, które nie kontrolujemy, łączy nasz harmonogram wydań z ich i ukrywa dokładny kontrakt drut, którym musimy się opiekować dla odporności i kosztu.

## Decyzja

`IAiClient` wywoła Anthropic przez **surowy HTTP** przez typowy `HttpClient` — świadomie **nie** SDK. `AiFeatureService` jest pojedynczym orkiestratorem współdzielonym przez endpointy sieciowe, `AiTools` MCP i `AiRiskGuard`. Cała powierzchnia jest **zależna od `AppOptions.Ai.ApiKey`**: bez klucza każda funkcja zwraca `AiResult.Fail` i aplikacja działa bez zmian.

## Konsekwencje

- Żaden klucz nie jest wymagany do kompilacji, testu lub E2E — CI i lokalne urządzenie uruchamiają całą aplikację bez AI.
- Jesteśmy właścicielami kształtu żądania/odpowiedzi, polityki ponawiania/limitu czasu i księgowości tokenów.
- Nowe funkcje Anthropic muszą być podłączone ręcznie; handlujemy wygodą za kontrolę i mniejszą powierzchnią zależności. Patrz odwołanie `claude-api` dla obecnych identyfikatorów modeli i parametrów.
