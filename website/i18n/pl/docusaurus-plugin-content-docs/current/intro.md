---
slug: /intro
title: Witamy w cMind
description: Przyjazne wprowadzenie do cMind — otwartej, samodzielnie hostowanej platformy operacji tradingowych dla cTrader.
sidebar_position: 1
---

# Witamy w cMind 👋

:::warning[Oprogramowanie alfa — niegotowe do produkcji]
cMind jest w trakcie aktywnego rozwoju. Spodziewaj się niedoskonałości, przełomowych zmian między wersjami i funkcji wciąż w trakcie realizacji. **Potrzebujemy testerów społeczności, zgłaszających błędy i wczesnych współtwórców**, aby pomóc go kształtować. Jeśli napotkasz problem, [zgłoś go](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) — Twoja opinia z realnego świata jest teraz najbardziej wartościową rzeczą, jaką możesz wnieść.
:::

A więc chcesz budować boty tradingowe, backtestować je bez topienia laptopa, uruchamiać je na kilku
maszynach, kopiować transakcje na kilkanaście kont i pozwolić SI pilnować ryzyka, gdy śpisz. **Trafiłeś
dokładnie we właściwe miejsce.**

cMind to **otwarta, samodzielnie hostowana platforma operacji tradingowych dla cTrader**. Pomyśl o niej
jak o całym swoim biurku tradingowym — tworzenie, wykonywanie, flota obliczeniowa, kopiowanie transakcji
i rdzeń SI — zapakowane w spokojną, ciemną, przyjazną dla urządzeń mobilnych aplikację, którą posiadasz
od początku do końca.

:::tip[W jednym zdaniu]
Buduj → backtestuj → uruchamiaj → kopiuj swoje strategie cTrader na dużą skalę, z wbudowaną SI, na
własnych serwerach i pod własną marką.
:::

## Co tak naprawdę potrafi?

| Chcesz… | cMind to robi | Więcej |
|---|---|---|
| Napisać cBota w przeglądarce | IDE Monaco + szablony C#/Python, kompilacje w piaskownicy | [Budowanie i backtest](./features/build-and-backtest.md) |
| Backtestować na wielu maszynach | Samonaprawiająca się flota węzłów wybiera najmniej obciążoną maszynę | [Skalowanie](./deployment/scaling.md) |
| Kopiować jedno konto na wiele | Solidne kopiowanie z resynchronizacją, bez podwójnych transakcji | [Kopiowanie transakcji](./features/copy-trading.md) |
| Zlecić SI czarną robotę | Generowanie strategii, samonaprawa, strażnik ryzyka, analizy powykonawcze | [Rdzeń SI](./features/ai.md) |
| Trzymać się reguł prop firmy | Śledzenie kapitału na żywo + symulacja reguł wyzwania | [Prop firma](./features/prop-firm.md) |
| Zwalidować przewagę backtestu | Korekcja PSR / DSR / t-stat na przeuczenie | [Backtest Integrity Lab](./features/backtest-integrity.md) |
| Zrozumieć własne nawyki | Wykrywanie wycieków behawioralnych + trener SI | [Dziennik tradingowy](./features/trading-journal.md) |
| Śledzić zdarzenia makro dla strategii | Kalendarz punkt-w-czasie, blokada wiadomości, API cBot | [Kalendarz ekonomiczny](./features/economic-calendar.md) |
| Oceniać siłę makro walut | Prognoza SI dla wszystkich par | [Siła waluty](./features/currency-strength.md) |
| Zabezpieczyć konta z 2FA | Aplikacja uwierzytelniająca TOTP + kody zapasowe | [Uwierzytelnianie dwuskładnikowe](./features/two-factor-auth.md) |
| Pozwolić właścicielom dostrajać w czasie wykonania | Każda opcja white-label na żywo w Ustawienia → Wdrożenie | [Ustawienia właściciela](./features/white-label-owner-settings.md) |
| Uruchamiać w dowolnym języku | 23 języki z RTL — build nie przechodzi przy brakującym kluczu | [Lokalizacja](./features/localization.md) |
| Wydać to jako *twój* produkt | Pełny white-label: nazwa, kolory, logo, favicon | [White-label](./features/white-label.md) |
| Uruchamiać na telefonie | Instalowalna, mobilna PWA | [PWA](./features/pwa.md) |
| Sterować z klienta SI | Wbudowany serwer MCP (HTTP + SSE) | [MCP](./features/mcp.md) |

## Ścieżka na 5 minut ⏱️

Jeśli masz Dockera i pięć minut, już teraz możesz pobawić się prawdziwą instancją cMind:

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
cp .env.example .env        # set OWNER_EMAIL + OWNER_PASSWORD
docker compose up --build
```

Następnie otwórz **<http://localhost:8080>**, zaloguj się i gotowe. Pełny przewodnik (wraz z
rozwiązywaniem problemów, gdy Docker nieuchronnie będzie miał swoje zdanie) znajdziesz w
**[Uruchamianie lokalnie](./deployment/local.md)**.

## Nowy tutaj? Podążaj żółtą ceglaną drogą 🟡

1. **[Dla kogo to jest?](./audience.md)** — upewnij się, że jesteś naszym rodzajem kłopotu.
2. **[Uruchamianie lokalnie](./deployment/local.md)** — postaw prawdziwą instancję.
3. **[Funkcje](./features/README.md)** — pełna wycieczka po tym, co jest w środku.
4. **[Wdrożenie na poważnie](./deployment/cloud.md)** — Docker, Kubernetes, Azure, AWS.
5. **[Uczyń to swoim](./white-label-for-business.md)** — nadaj white-label dla swojej firmy.
6. **[Współtwórz](./contributing.md)** — PR-y (ludzkie *i* wspomagane SI) mile widziane.

## Krótkie słowo o pieniądzach 💸

cMind obraca **prawdziwym kapitałem**. Traktujemy to poważnie — każda zmiana jest dostarczana z testami
jednostkowymi, integracyjnymi i end-to-end, wraz ze ścieżkami awarii (zerwane połączenia, odrzucone
zlecenia, martwe węzły). Ty też powinieneś traktować to poważnie: **najpierw testuj na koncie demo** i
przeczytaj [uwagi dotyczące zgodności](./features/compliance.md), zanim skierujesz to na cokolwiek
prawdziwego. Trading jest ryzykowny; to oprogramowanie jest narzędziem, a nie poradą finansową.

No dobrze — dość wstępu. Chodźmy coś zbudować. →
