---
title: 0003 — cTrader CLI nodes are HTTP + JWT, no SSH/shell
description: Dlaczego agenci węzłów zdalnych udostępniają tylko HTTP API z krótkotrwałymi JWT i nigdy shellem.
---

# 0003 — Węzły cTrader CLI to HTTP + JWT, brak SSH/shell

## Kontekst

Kontenery backtestu/uruchomienia są wykonywane na hostach zdalnych. Oczywiste podejście — SSH i uruchomienie docker — daje głównej aplikacji arbitralną zdalną egzekucję kodu i długotrwałe poświadczenia na każdym węźle. To duży promień wybuchu dla systemu, który uruchamia niezaufane cBoty użytkownika.

## Decyzja

Każdy host zdalny uruchamia samodzielny agent `CtraderCliNode` **HTTP** z **bez SSH i bez shell**. Główna aplikacja wywoła agenta przez HTTP; każde żądanie nosi krótkotrwały **HS256 JWT** (5 minut, `iss=app-main` / `aud=app-node`) podpisany tajemnicą tego węzła. Agent:

- uruchamia tylko obrazy pasujące do `AllowedImagePrefix` (z granicą ścieżki, aby `ghcr.io/spotware` nie mogło pasować do `ghcr.io/spotware-evil/...`);
- wykonuje docker przez `ArgumentList` — nigdy ciąg shell;
- jest **bezstanowy**, znajdujący kontenery przez etykietę `app.instance`;
- sam się rejestruje i bije serce do `POST /api/nodes/register`; główna aplikacja upserta `CtraderCliNode` **po nazwie**, aby węzeł przetrwał zmiany IP.

## Konsekwencje

- Wyciek tokenu żądania wygasa w ciągu minut; nie ma stojącego poświadczenia shell do kradzieży.
- Możliwość agenta jest ograniczona do "uruchomienia dozwolonego obrazu" — nie można go zamienić w ogólny shell zdalny.
- Tożsamość węzła jest oparta na nazwie, więc ponowne aprowizowanie węzła z nowym IP nie osierocieć jego historii.
