---
slug: /features
title: Funkcje — pełna wycieczka
description: Wszystko, co cMind może robić — kopiowanie transakcji, AI, budowanie i backtesting, strażniki prop-firm, white-label, PWA, MCP i więcej.
sidebar_label: Przegląd
---

# Funkcje — pełna wycieczka 🧭

Witaj na wielkiej wycieczce. cMind pakuje *dużo* w jedną aplikację, więc oto mapa. Każda możliwość ma swój własny dokument deep-dive — kliknij na cokolwiek, co cię drażni.

## 🔁 Kopiowanie transakcji

Klejnot korony. Lustrzane główne konto na wiele i utrzymuj je zsynchronizowane nawet, gdy internet się misbehaves.

- **[Kopiowanie transakcji](./copy-trading.md)** — rdzeń: lustrzane, typy zamówień, SL/TP, poślizgi, desync/resync.
- **[Przezroczystość wykonania](./copy-execution-transparency.md)** — zobacz dokładnie, co skopiowano, kiedy i dlaczego.
- **[Opłaty za wydajność](./copy-performance-fees.md)** — pobieraj za swój sygnał, styl high-water-mark.
- **[Rynek dostawcy](./copy-provider-marketplace.md)** — pozwól handlowcom odkrywać i śledzić dostawców.
- **[Powiadomienia](./copy-notifications.md)** — otrzymaj wiadomość, gdy coś ciebie potrzebuje.
- **[Rekomendant kopii AI](./ai-copy-recommender.md)** — pozwól AI zasugerować kogo kopiować.
- **[Cykl życia tokenu Open API](./token-lifecycle.md)** — jak cMind utrzymuje dokładnie jeden ważny token na cID.

## 📊 Twoja baza domowa

- **[Pulpit](./dashboard.md)** — na żywo, mobilny-pierwszy centrum dowodzenia: KPI'y ze sparklines, wykres aktywności, pierścień statusu, na żywo feed i (dla administratorów) zdrowie klastra. Odświeża się sam.

## 🧠 Rdzeń AI

Nie chat box przybolcowany z boku — AI, który naprawdę *robi pracę*.

- **[Asystent AI, agent, strażnik ryzyka i alerty](./ai.md)** — generowanie strategii, samodzielnie naprawiające kompilacje, strażnik ryzyka w tle, który może auto-zatrzymać boty i smart alerty.

## 🛠️ Buduj i uruchamiaj

- **[Kompiluj i testuj wstecz cBoty](./build-and-backtest.md)** — IDE Monaco wewnątrz przeglądarki, szablony C#/Python, kompilacje w piaskownicy i na żywo krzywe kapitału.
- **[Serwer MCP](./mcp.md)** — ujawnij narzędzia cMind przez HTTP + SSE, aby klienci AI mogą je prowadzić.

## 🏢 Uruchamiaj jako biznes

- **[White-label / branding](./white-label.md)** — rebranding każdej powierzchni przez config.
- **[Symulacja wyzwania prop-firm](./prop-firm.md)** — egzekwuj reguły dziennego straty, drawdown i celu z kapitałem na żywo.
- **[Przełączniki funkcji](./feature-toggles.md)** — zdecyduj, co każde wdrażanie/dzierżawca widzi.
- **[Zgodność / prawo](./compliance.md)** — ścieżka audytu i powierzchnia prawna.

## 📱 Doświadczenie

- **[Instalowalna aplikacja (PWA)](./pwa.md)** — mobilny-pierwszy, shell offline, dodaj do ekranu głównego.
- **[System projektowania interfejsu użytkownika i mobilny-pierwszy](../ui-guidelines.md)** — tokeny projektowe i reguły za wyglądem.

## ⚙️ Pod maską

Operacyjne bity, które utrzymują to wszystko uruchomione:

- **[Flota węzłów i odkrycie](../operations/node-discovery.md)** — jak węzły samodzielnie rejestrują się i leczą.
- **[Skalowanie poziome](../deployment/scaling.md)** — dodaj repliki, brak koordynatora zewnętrznego potrzebny.
- **[Logowanie i audyt](../operations/logging.md)** — strukturalne dzienniki + OpenTelemetry.
- **[Wdrażanie](../deployment/local.md)** — uruchom go wszędzie.

:::note Utrzymanie dokumentów uczciwie
Każdy dokument funkcji jest utrzymywany w synchronizacji z kodem — zmień zachowanie, zaktualizuj dokument, ten sam commit. Jeśli kiedykolwiek zauważysz dryf, to jest błąd: proszę [otwórz issue](https://github.com/amusleh-spotware-com/cmind/issues/new/choose) lub wyślij PR. 🙏
:::
