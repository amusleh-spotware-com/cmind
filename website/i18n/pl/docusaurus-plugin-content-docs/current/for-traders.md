---
slug: /for-traders
title: cMind dla traderów cTrader
description: Dlaczego trader cTrader powinien samodzielnie hostować cMind — posiadaj swój stos i dane, autor, backtest, uruchom i monitoruj cBoty w jednej konsoli napędzanej AI, na swoim laptopie, VPS lub telefonie.
keywords:
  - cTrader
  - Handel algorytmiczny
  - Samodzielnie hostowana platforma handlowa
  - Backtesting cBot
  - AI boty handlowe
  - Otwarte oprogramowanie handlowe
sidebar_position: 5
---

# cMind dla traderów cTrader 📈

Już handlujesz na cTrader. Już żongliruj edytorem kodu, backtestem, VPS i trzema kartami przeglądarki. **cMind zwija to wszystko w jedną ciemną, przyjazną klawiaturę konsolę, którą uruchamiasz sam** — i jest to otwarte oprogramowanie, więc nic o twoim przedzie, strategiach lub poświadczeniach nigdy nie opuszcza twojej skrzyni.

:::tip TL;DR
Samodzielnie hostuj cMind na laptopie, tanią VPS lub domowym serwerze. Autor, backtest, uruchom i monitoruj cBoty w jednym miejscu, z rdzeniem AI wykonującym prace. → [Uruchom go w 5 minut](./deployment/local.md)
:::

## Dlaczego samodzielnie hostować zamiast usługi hostowanej?

- **Posiadaj swój stos i dane.** Twoje cBoty, poświadczenia, tokeny i historia kapitału żyją na **twojej** infrastrukturze — brak strony trzeciej, brak lock-in, brak e-mailu "wycofujemy ten produkt".
- **To jest naprawdę twoje do zmiany.** C# 14 / .NET 10, ścisłe DDD, EF Core + PostgreSQL, serwer MCP — wszystko otwarte oprogramowanie i hackable. Rozwidlaj to, rozszerz to, wyślij PR.
- **Brak paywall na funkcję.** Przynieś swój własny klucz AI dla dowolnego dostawcy; każda funkcja AI jest włączona.

Wolisz nie uruchamiać serwerów? Firma hostingowa może dla ciebie uruchomić zarządzany cMind — zobacz [Dla dostawców chmury i VPS](./for-cloud-providers.md).

## Jedna konsola, brak żonglowania kartami

- **Autor** w rzeczywistym IDE Monaco (edytor VS Code), z szablonami C# **i** Python i piaskownicy `dotnet build` w jednorazowych kontenerach. → [Kompilacja i backtest](./features/build-and-backtest.md)
- **Backtest** w całej flocie węzłów i obserwuj krzywe kapitału przesyłane na żywo.
- **Uruchom** strategie na żywo i **monitoruj** je z jednego pulpitu. → [Pulpit](./features/dashboard.md)
- **Kopiuj** główne konto na wiele kont w brokerach i identyfikatorach cTrader, z pogodzeniem, które przetrwa upuszczone połączenia i obracające się tokeny. → [Kopiowanie transakcji](./features/copy-trading.md)

## AI, które robi prace, nie małą rozmowę

Przynieś swój własny klucz API (dowolny obsługiwany dostawca — chmura lub model lokalny) i uzyskaj zwykły angielski → rzeczywisty cBot z kompilacją z automatyczną naprawą, dostrajaniem parametrów, postmortem backtestów i strażnikiem ryzyka, który może auto-zatrzymać misbehaving bota. → [Poznaj rdzeń AI](./features/ai.md)

## Narzędzia na poziomie instytucji, dla jednego

Te same dochody, które biuro płaci za, na swojej własnej skrzyni:

- [Backtest integrity](./features/backtest-integrity.md) · [Position sizing](./features/position-sizing.md)
- [Strategy health](./features/strategy-health.md) · [Regime lab](./features/regime-lab.md)
- [Execution TCA](./features/execution-tca.md) · [Trading journal](./features/trading-journal.md)
- [Agent Studio](./features/agent-studio.md) · [Contrarian positioning](./features/contrarian-positioning.md)

## Uruchamia się gdzie ty

Zacznij na swoim laptopie z `docker compose up`, przejdź do taniego VPS lub domowego serwera, gdy będziesz gotowy, i sprawdzaj swoje boty z telefonu — cMind jest instalowalne, mobilne-pierwsze [PWA](./features/pwa.md). → [Uruchom go lokalnie](./deployment/local.md)

Chcesz, aby twój klient AI go prowadził? Istnieje wbudowany [serwer MCP](./features/mcp.md).

## Pomóż to ulepszyć

cMind jest otwarte oprogramowanie i MIT-licencjonowany — roadmap kształtuje społeczność:

- Zarabiaj problemy i żądania funkcji i głosuj na to, co ma znaczenie.
- Dodaj szablony cBot, adaptatory dostawcy AI lub tłumaczenia interfejsu użytkownika.
- Wyślij PR — trzy poziomy testów (jednostka + integracja + E2E) i ścisłe DDD utrzymują pasek wysoko, a [Przewodnik współtworzenia](./contributing.md) cię przeprowadzi.

Gotowy? → [Przeczytaj intro](./intro.md) potem [uruchom go lokalnie](./deployment/local.md).
