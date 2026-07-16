---
description: "Budowanie, uruchamianie i testowanie wstecz cBotów cTradera (C# i Python, oba .NET) z wbudowanego edytora Monaco w przeglądarce, uruchamiane na oficjalnym obrazie ghcr.io/spotware/ctrader-console."
---

# Budowanie i testowanie wstecz cBotów

Budowanie, uruchamianie i testowanie wstecz cBotów cTradera (C# **i** Python, oba .NET) z wbudowanego edytora Monaco w przeglądarce, uruchamiane na oficjalnym obrazie `ghcr.io/spotware/ctrader-console`.

## Budowanie

- Strona **Builder** hostuje edytor Monaco; `CBotBuilder` kompiluje projekt za pomocą `dotnet build` **w efemerycznym kontenerze** (`AppOptions.BuildImage`, katalog roboczy montowany jako bind-mount w `/work`), aby wiarygodne obiekty docelowe MSBuild użytkownika nie mogły uzyskać dostępu do hosta. Przywracanie NuGet jest buforowane w całych kompilacjach za pośrednictwem udostępnionego wolumenu. Host sieciowy wymaga dostępu do gniazda Docker.
- Szablony startowe C# i Python znajdują się w `src/Nodes/Builder/Templates/`.

## Uruchamianie i testowanie wstecz

- **Instancje** = hierarchia stanów TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). Przejście zastępuje encję (zmiana id), id kontenera jest przeniesione.
- `NodeScheduler` wybiera najmniej obciążony kwalifikujący się węzeł; `ContainerDispatcherFactory` kieruje do zdalnego agenta HTTP węzła lub lokalnego dyspozytora Docker.
- Detektory ukończenia uzgadniają wychodzące kontenery (kontenery testu wstecz wychodzą samoczynnie poprzez `--exit-on-stop`); raport obecny → ukończony (przechowywanie `ReportJson`), brak → nieudany.
- Dzienniki kontenerów na żywo są przesyłane do przeglądarki przez SignalR; krzywe kapitału testu wstecz są analizowane z raportu i wykreślane.

## Dane rynkowe testu wstecz są buforowane na konto

Konsola cTradera pobiera historyczne dane tików/słupków do swojego katalogu `--data-dir`. Ten katalog jest **stabilną, trwałą pamięcią podręczną opartą na koncie handlowym** (jego numerze konta) — montowany na bind-mount z dysku węzła na jego własnej ścieżce kontenera (`/mnt/data`), **oddzielny, niezagnieżdżony montaż** z katalogiem roboczy dla instancji. Tak więc każdy test wstecz na tym samym koncie **ponownie wykorzystuje** już pobrane dane zamiast ponownego pobrania za każdym razem. (Wcześniej katalog danych znajdował się w katalogu roboczy dla instancji, którego id zmienia się za każdym razem, co wymusiło świeże pobranie każdego testu wstecz.) Efemeryczny katalog roboczy dla instancji nadal zawiera algorytm, parametry, hasło i raport; udostępniona pamięć podręczna danych jest liczona w użyciu danych testu wstecz węzła i czyszczona przez akcję czyszczenia węzła.

## Ustawienia testu wstecz

Dialog **Backtest** ukazuje ustawienia testu wstecz konsoli cTradera dostosowywalne przez użytkownika, aby nigdy nie trzeba było dotykać wiersza poleceń:

- **Symbol / Timeframe** — timeframe to **lista rozwijana każdego okresu cTradera** (`t1`…`t1000`, `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1` i okresy Renko/Range/Heikin), w kanonicznym obudowaniu konsoli, dzięki czemu zawsze wybierzesz prawidłowy `--period`.
- **Od / Do** — okno testu wstecz (`--start` / `--end`).
- **Tryb danych** — jeden z trzech trybów cTradera (`--data-mode`): **Dane tików** (`tick`, dokładne), **słupki m1** (`m1`, szybkie) lub **Tylko ceny otwarcia** (`open`, najszybsze).
- **Saldo początkowe** — domyślnie `10000` (`--balance`). **Saldo równe 0 nie zawiera transakcji i powoduje, że cTrader emituje pusty raport, na którym następnie się zawala** ("Message expected"), więc zawsze wysyłane jest saldo niezerowe.
- **Prowizja** — `--commission`.
- **Spread** — `--spread`, **pole numeryczne w pipsach, które nie może być poniżej 0**. Jest **ukryte w trybie danych tików**, gdzie cTrader wyznacza spread na podstawie samych danych tików (nie wysyłane `--spread`).

Katalog danych (`--data-file` / `--data-dir`) jest zarządzany przez samą aplikację (pamięć podręczna dla każdego konta, patrz wyżej), nie ujawniany w dialogu.

:::note cTrader zawala się na pustym teście wstecz
Jeśli test wstecz produkuje **brak wyników** — żadnych transakcji lub żadnych danych rynkowych dla wybranych dat/symbolu — moduł pisania raportów konsoli cTradera wyrzuca `Message expected` i wychodzi bez raportu. Aplikacja nie może naprawić tego błędu upstream, ale go wykrywa i oznacza instancję jako **Nieudana** z przyczyn czytelną dla użytkownika ("brak wyników testu wstecz dla wybranego zakresu…") zamiast surowego śladu stosu. Wybierz szerszy zakres dat, który ma dostępne dane rynkowe i spróbuj ponownie.
:::

## Strona szczegółów instancji

Otwarcie instancji (`/instance/{id}`) pokazuje jej status na żywo, dzienniki i — dla testu wstecz — krzywą kapitału. Tytuł **karty przeglądarki** odzwierciedla konkretną instancję (**nazwa cBota · rodzaj · symbol**, np. `TrendBot · Backtest · EURUSD`), dzięki czemu karta uruchomienia na żywo i karta testu wstecz są odróżnialne na pierwszy rzut oka. Uruchomienie i test wstecz tego samego cBota są śledzone jako odrębne **linie** (stabilny id linii przeniesiony w przejściach stanów), więc strona podąża dokładnie jedną instancją i nigdy nie mieszają danych uruchomienia z testem wstecz.

## Kontrole cyklu życia instancji

Każdy wiersz instancji (i jego strona szczegółów) ma kontrole uwzględniające stan. Aktywna instancja pokazuje **Stop**; terminal (Zatrzymana / Ukończona / Nieudana) pokazuje **Start (▶)** ponownie uruchomić ją za pomocą tego samego cBota, konta, symbolu, timeframe'a, zestawu parametrów i obrazu (uruchomienie restartuje się jako uruchomienie, test wstecz jako test wstecz). Kliknięcie Stop wyświetla powiadomienie "Zatrzymywanie…" i wyłącza ikonę do czasu jej rozwiązania, a nowo utworzone uruchomienie pojawia się na liście natychmiast — bez przeładowania strony.

Dzienniki konsoli są **utrwalane po zakończeniu instancji** — zarówno dla uruchomienia (na Stop), jak i dla **testu wstecz** (po ukończeniu) — tak aby dzienniki ostatniego uruchomienia pozostają widoczne na stronie szczegółów i, poprzez pasek narzędzi dziennika, **skopiowane do schowka** (ikona Kopiuj dzienniki) lub **pobrane** (ikona Pobierz dzienniki) nawet po usunięciu kontenera. Oba działają na pełnym dzienniku konsoli instancji, a nie tylko na widocznym ogonie.

**Ukończony test wstecz** również utrwala swój **raport cTradera** w obu formatach — surowy **JSON** (ten sam, którego używają krzywa kapitału i analiza AI) i pełny raport **HTML**. Oba są dostępne do pobrania z wiersza testu wstecz **i** strony szczegółów za pośrednictwem dedykowanych ikon. Tylko **raporty ostatniego uruchomienia** są przechowywane, a ikony są **wyłączone** dla dowolnego testu wstecz, który nie został uruchomiony, jest uruchomiony lub nieudany (i nigdy nie są wyświetlane dla instancji uruchomienia) — tylko ukończony test wstecz ma raport do pobrania.

Przesłany `.algo` nigdy nie został tutaj zbudowany, więc jego kolumna **Last Build** na stronie cBotów jest pusta (pokazuje czas kompilacji tylko dla cBotów zbudowanych w przeglądarce).

## Edycja i ponowne uruchomienie zatrzymanej instancji

**Zatrzymana** instancja (uruchomienie lub test wstecz) ma kontrolę **Edit** — ikonę na jej wierszu na liście **i** obok Start/Stop na stronie szczegółów — która otwiera dialog **wstępnie wypełniony** jego bieżącą konfiguracją. Możesz zmienić **konto handlowe, symbol, timeframe, zestaw parametrów i tag obrazu** (i, dla testu wstecz, **okno i wszystkie powyższe ustawienia testu wstecz**), a następnie **Zapisz i uruchom** ponownie uruchamia go z nowymi ustawieniami (zastępując zatrzymaną instancję). Kontrola jest **wyłączona, gdy instancja jest aktywna** — tylko zatrzymana instancja może być edytowana.

## Run from the code editor

Clicking **Run** in the code editor opens a dialog instead of firing a blind, hard-coded run:

- **Trading account** (required) — the cTrader account the cBot connects to.
- **Parameter set** (optional) — pick an existing set, or leave it empty to run with the cBot's
  **default parameter values**. A **+** button next to the selector creates a new parameter set
  inline (see below) and selects it.
- **Symbol / Timeframe** default to `EURUSD` / `h1` and can be changed; **Cancel** or **Run**.

On **Run** the editor saves + builds the current source, starts the instance on the chosen account
with the chosen parameters, then tails the live container logs. (The log stream forwards the
signed-in user's auth cookie to the `/hubs/logs` SignalR hub, so it connects instead of failing with
`Invalid negotiation response received`.)

## Parameter sets

A **parameter set** is a named, reusable set of cBot parameter overrides stored as a flat JSON
object mapping each parameter name to a scalar value, e.g. `{"Period": 14, "Label": "trend"}`. At
run/backtest time it is turned into the cTrader `params.cbotset` file
(`{ "Parameters": { … } }`). You can create/edit a set as raw JSON from the cBot's **Parameter
sets** dialog or inline from the Run dialog.

Every parameter set **belongs to a cBot**: the New Parameter Set dialog lists all your cBots and you
**must pick one** — creation is blocked until a cBot is selected. A set's **name is unique per cBot**:
creating or renaming a set to a name another set of the same cBot already uses is rejected (a clear
error in the dialog, `409 Conflict` at the API). The same name may be reused on a **different** cBot.

The JSON is **validated** on save: it must be a single flat object whose values are all scalars
(string / number / bool). A non-object root, an array, a nested object, a `null` value, or malformed
JSON is rejected (a clear error in the dialog, `400 Bad Request` at the API). An empty object `{}`
is allowed and means "no overrides".

## cTrader Console CLI notes

Backtests need `--data-mode` (default `m1`), dates as `dd/MM/yyyy HH:mm`, and
`params.cbotset` JSON positional arg; `run` reject `--data-dir` (backtest-only). See
`ContainerCommandHelpers`.

## Nodes & scale

Execution capacity scale by adding node agents (self-register + heartbeat). See
[node discovery](../operations/node-discovery.md) and [scaling](../deployment/scaling.md).
## A trading account is required

Running or backtesting a cBot needs a cTrader trading account to connect to. Until you add one under
**Trading accounts**, the **Run New cBot** / **Backtest New cBot** buttons are disabled (with a
tooltip) and the page shows a prompt linking to account setup — you no longer hit a raw
`stream connect failed` error from a bot with no account.
