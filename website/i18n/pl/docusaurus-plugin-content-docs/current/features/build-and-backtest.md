---
description: "Buduj, uruchamiaj, backtest cTrader cBots (C# i Python, oba .NET) z in-browser Monaco IDE, uruchamiaj na oficjalnym ghcr.io/spotware/ctrader-console obrazie."
---

# Budowanie i backtest cBots

Buduj, uruchamiaj, backtest cTrader cBots (C# **i** Python, oba .NET) z in-browser Monaco
IDE, uruchamiaj na oficjalnym `ghcr.io/spotware/ctrader-console` obrazie.

## Budowanie

- Strona **Builder** hostuje editor Monaco; `CBotBuilder` kompiluje projekt z
  `dotnet build` **w jednorazowym kontenerze** (`AppOptions.BuildImage`, work dir bind-mount
  na `/work`), więc nieufny target użytkownika MSBuild nie dochodzi do host. NuGet restore cached
  między buildami przez wspólny volumen. Web host potrzebuje dostępu do Docker socket.
- Szablony startowe C# + Python żyją w `src/Nodes/Builder/Templates/`.

## Uruchamianie i backtest

- **Instances** = TPH state hierarchy (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Transition zastępuje entity (zmiana id),
  container id przeniosity.
- `NodeScheduler` bierze least-loaded eligible node; `ContainerDispatcherFactory` rozsyła do
  remote node HTTP agent lub localny Docker dispatcher.
- Completion pollers reconcilią exited containers (backtest containers self-exit przez
  `--exit-on-stop`); report present → completed (store `ReportJson`), missing → failed.
- Live container logs stream do browser nad SignalR; backtest equity curves parsed z
  report + charted.

## cTrader Console CLI notatki

Backtesty potrzebują `--data-mode` (domyślnie `m1`), daty jako `dd/MM/yyyy HH:mm`, i
`params.cbotset` JSON positional arg; `run` reject `--data-dir` (backtest-only). Zobacz
`ContainerCommandHelpers`.

## Nodes i skalowanie

Capacity egzekucji skaluje się przez dodanie node agents (self-register + heartbeat). Zobacz
[node discovery](../operations/node-discovery.md) i [scaling](../deployment/scaling.md).

## Uruchamianie z edytora kodu

Kliknięcie **Uruchom** w edytorze kodu otwiera okno dialogowe zamiast uruchamiać ślepe, zakodowane na stałe uruchomienie:

- **Konto handlowe** (wymagane) — konto cTrader, z którym łączy się cBot.
- **Zestaw parametrów** (opcjonalnie) — wybierz istniejący zestaw lub pozostaw puste, aby uruchomić z **domyślnymi wartościami parametrów** cBota. Przycisk **+** obok selektora tworzy nowy zestaw parametrów w miejscu (patrz niżej) i go wybiera.
- **Symbol / Interwał** domyślnie `EURUSD` / `h1` i można je zmienić; **Anuluj** lub **Uruchom**.

Po **Uruchom** edytor zapisuje i kompiluje bieżący kod źródłowy, uruchamia instancję na wybranym koncie z wybranymi parametrami, a następnie śledzi na żywo logi kontenera. (Strumień logów przekazuje ciasteczko uwierzytelniania zalogowanego użytkownika do huba SignalR `/hubs/logs`, dzięki czemu łączy się, zamiast kończyć błędem `Invalid negotiation response received`.)

## Zestawy parametrów

**Zestaw parametrów** to nazwany, wielokrotnego użytku zestaw nadpisań parametrów cBota, przechowywany jako płaski obiekt JSON mapujący każdą nazwę parametru na wartość skalarną, np. `{"Period": 14, "Label": "trend"}`. W czasie uruchomienia/backtestu jest przekształcany w plik cTrader `params.cbotset` (`{ "Parameters": { … } }`). Zestaw można utworzyć/edytować jako surowy JSON z okna **Zestawy parametrów** cBota lub w miejscu z okna Uruchom.

JSON jest **walidowany** przy zapisie: musi być pojedynczym płaskim obiektem, którego wszystkie wartości są skalarne (ciąg / liczba / bool). Korzeń niebędący obiektem, tablica, zagnieżdżony obiekt, wartość `null` lub nieprawidłowy JSON są odrzucane (czytelny błąd w oknie, `400 Bad Request` w API). Pusty obiekt `{}` jest dozwolony i oznacza „brak nadpisań".

## Sterowanie cyklem życia instancji

Każdy wiersz instancji (i jej strona szczegółów) ma elementy sterujące zgodne ze stanem. **Aktywna** instancja pokazuje **Zatrzymaj**; **terminalna** (Zatrzymana / Ukończona / Nieudana) pokazuje **Uruchom (▶)**, aby uruchomić ją ponownie z tym samym cBotem, kontem, symbolem, interwałem, zestawem parametrów i obrazem (uruchomienie startuje jako uruchomienie, backtest jako backtest). Kliknięcie Zatrzymaj pokazuje komunikat „Zatrzymywanie…" i wyłącza ikonę do czasu zakończenia; nowo utworzone uruchomienie pojawia się na liście natychmiast — bez przeładowania strony.

Logi konsoli są **zachowywane, gdy instancja się kończy** — zarówno dla uruchomienia (przy zatrzymaniu), jak i dla **backtestu** (po ukończeniu) — dzięki czemu logi ostatniego uruchomienia pozostają widoczne na stronie szczegółów i możliwe do pobrania za pomocą ikony **Pobierz logi**, nawet po zniknięciu kontenera.
