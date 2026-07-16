---
description: "Buduj, uruchamiaj, testuj wstecz cBoty cTreadera (C# i Python, oba .NET) z edytora Monaco w przeglądarce, uruchamiaj na oficjalnym obrazie ghcr.io/spotware/ctrader-console."
---

# Build & backtest cBots

Buduj, uruchamiaj, testuj wstecz cBoty cTreadera (C# **i** Python, oba .NET) z edytora Monaco w przeglądarce, uruchamiaj na oficjalnym obrazie `ghcr.io/spotware/ctrader-console`.

## Build

- **Builder** strona hostuje edytor Monaco; `CBotBuilder` kompiluje projekt za pomocą
  `dotnet build` **w dyspozycyjnym kontenerze** (`AppOptions.BuildImage`, katalog roboczy bind-mount
  w `/work`), aby niezaufane cele MSBuild użytkownika nie mogły dosięgnąć hosta. Przywracanie NuGet jest buforowane
  w wielu kompilacjach za pomocą wspólnego wolumenu. Host sieciowy potrzebuje dostępu do gniazda Docker.
- Szablony startowe C# + Python znajdują się w `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = hierarchia stanów TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Przejście zastępuje jednostkę (zmiana id),
  identyfikator kontenera jest przenoszony.
- `NodeScheduler` wybiera najmniej obciążony kwalifikujący się węzeł; `ContainerDispatcherFactory` kieruje do
  zdalnego agenta HTTP węzła lub lokalnego dyspozytora Docker.
- Pollerów uzupełniających uzgadniają wyjścia z kontenerów (kontenery backtestu auto-wyjścia za pomocą
  `--exit-on-stop`); raport obecny → ukończony (przechowaj `ReportJson`), brakujący → nieudany.
- Dzienniki kontenerów na żywo strumieniują do przeglądarki przez SignalR; krzywe kapitałowe backtestu analizowane z
  raportu + wykreślane.

## Backtest market data is cached per account

Konsola cTreadera pobiera historyczne dane tick/bar do swojego `--data-dir`. Ten katalog to
**stabilna, trwała pamięć podręczna oparta na koncie handlowym** (jego numerze konta) — bind-mounted z
dysku węzła w jego własnej ścieżce kontenera (`/mnt/data`), **oddzielny, niezagnieżdżony mount** z
katalogiem roboczym dla każdej instancji. Dlatego każdy backtest na tym samym koncie **ponownie wykorzystuje** już pobrane dane
zamiast pobierać je ponownie w każdym uruchomieniu. (Wcześniej
katalog danych znajdował się w katalogu roboczym dla każdej instancji, którego id zmienia się w każdym uruchomieniu, co wymusiło świeży
pobór przy każdym backteście.) Efemeryczny katalog roboczy dla każdej instancji nadal zawiera algo, parametry, hasło
i raport; wspólna pamięć podręczna danych jest liczona w użyciu danych backtestu węzła i czyszczona przez
akcję node-clean.

## Backtest settings

Dialog **Backtest** udostępnia wszystkie ustawienia, które akceptuje CLI backtestu konsoli cTreadera, więc nigdy
nie musisz dotykać linii poleceń:

- **From / To** — okno backtestu (`--start` / `--end`).
- **Data mode** — jeden z trzech trybów cTreadera (`--data-mode`): **Tick data** (`tick`, dokładne),
  **m1 bars** (`m1`, szybkie), lub **Open prices only** (`open`, najszybsze).
- **Starting balance** — domyślnie `10000` (`--balance`). **Saldo 0 nie umieszcza żadnych transakcji i powoduje,
  że cTrader emituje pusty raport, na którym się załamuje** ("Message expected"), więc saldo niezerowe jest
  zawsze wysyłane.
- **Commission** i **Spread** — `--commission` / `--spread` (spread w pipsach).
- **Data file** (opcjonalnie) — ścieżka po stronie węzła do pliku danych historycznych (`--data-file`); pozostaw puste, aby
  użyć pobranych/buforowanych danych.
- **Expose environment variables** — toggle, który przekazuje zmienne środowiskowe hosta do cBota
  (flaga `--environment-variables`).

## Instance detail page

Otwarcie instancji (`/instance/{id}`) pokazuje jej status na żywo, dzienniki i — dla backtestu — krzywą
kapitałową. **Tytuł karty przeglądarki** odzwierciedla konkretną instancję (**nazwa cBota · rodzaj · symbol**, np.
`TrendBot · Backtest · EURUSD`), więc karta żywego uruchomienia i karta backtestu są rozróżnialne na pierwszy rzut oka.
Uruchomienie i backtest tego samego cBota są śledzone jako odrębne **linie** (stabilny identyfikator linii przenoszony
w przejściach stanów), więc strona śledzi dokładnie jedną instancję i nigdy nie miesza danych uruchomienia z
danymi backtestu.

## Instance lifecycle controls

Każdy rząd instancji (i jej strona szczegółów) ma sterowanie dostosowane do stanu. Aktywna **active** instancja pokazuje
**Stop**; **terminal** (Stopped / Completed / Failed) pokazuje **Start (▶)** aby ją ponownie uruchomić z
tym samym cBotem, kontem, symbolem, ramką czasową, zestawem parametrów i obrazem (uruchomienie restartuje się jako uruchomienie, a
backtest jako backtest). Kliknięcie Stop pokazuje zawiadomienie "Stopping…" i wyłącza ikonę do czasu rozwiązania, a
nowo utworzone uruchomienie pojawia się na liście natychmiast — bez przeładowania strony.

Dzienniki konsoli są **utrwalane gdy instancja się kończy** — dla uruchomienia (przy Stop) i dla
**backtestu** (po ukończeniu) — więc dzienniki ostatniego uruchomienia pozostają wyświetlane na stronie szczegółów i,
za pośrednictwem paska narzędzi dziennika, **skopiowane do schowka** (ikona Kopiuj dzienniki) lub **pobrane** (ikona Pobierz dzienniki)
nawet po usunięciu kontenera. Obie działają na pełnym dzienniku konsoli instancji, a nie tylko
widocznym ogonie.

Wgrany **`.algo`** nigdy nie został zbudowany tutaj, więc jego kolumna **Last Build** na stronie cBots pozostaje
pusta (pokazuje czas kompilacji tylko dla cBotów, które budujesz w przeglądarce).

## Edit & re-run a stopped instance

**Zatrzymana** instancja (uruchomienie lub backtest) ma kontrolę **Edit** — ikonę w jej wierszu na liście **i**
obok Start/Stop na jej stronie szczegółów — która otwiera dialog **wstępnie wypełniony** jej bieżącą konfiguracją.
Możesz zmienić **konto handlowe, symbol, ramkę czasową, zestaw parametrów i tag obrazu** (i, dla
backtestu, **okno i wszystkie ustawienia backtestu** powyżej), a następnie **Save & start** uruchamia ją ponownie z
nowymi ustawieniami (zastępując zatrzymaną instancję). Kontrola jest **wyłączona, gdy instancja jest aktywna** —
tylko zatrzymana instancja może być edytowana.

## Run from the code editor

Kliknięcie **Run** w edytorze kodu otwiera dialog zamiast uruchamiać ślepe, zakodowane na stałe uruchomienie:

- **Trading account** (wymagane) — konto cTreadera, z którym łączy się cBot.
- **Parameter set** (opcjonalnie) — wybierz istniejący zestaw, lub pozostaw puste, aby uruchomić z **domyślnymi wartościami parametrów** cBota.
  Przycisk **+** obok selektora tworzy nowy zestaw parametrów
  wbudowany (patrz poniżej) i go wybiera.
- **Symbol / Timeframe** domyślnie do `EURUSD` / `h1` i mogą być zmieniane; **Cancel** lub **Run**.

Na **Run** edytor zapisuje + kompiluje bieżące źródło, uruchamia instancję na wybranym koncie
z wybranymi parametrami, a następnie śledzi dzienniki kontenera na żywo. (Strumień dziennika przekazuje
ciasteczko auth zalogowanego użytkownika do hub SignalR `/hubs/logs`, aby się połączył zamiast wysyłać błąd
`Invalid negotiation response received`.)

## Parameter sets

**Parameter set** to nazwany, wielokrotnie używalny zestaw przesłonięć parametrów cBota przechowywany jako płaski obiekt JSON
mapujący każdą nazwę parametru na wartość skalarną, np. `{"Period": 14, "Label": "trend"}`. W
czasie uruchomienia/backtestu jest on konwertowany na plik `params.cbotset` cTreadera
(`{ "Parameters": { … } }`). Możesz tworzyć/edytować zestaw jako raw JSON z dialogu **Parameter
sets** cBota lub wbudowany z dialogu Run.

Każdy zestaw parametrów **należy do cBota**: dialog New Parameter Set wyświetla wszystkie twoje cBoty i musisz
**wybrać jeden** — tworzenie jest zablokowane, dopóki cBot nie zostanie wybrany. **Nazwa** zestawu jest **unikalna dla cBota**:
utworzenie lub zmiana nazwy zestawu na nazwę, którą inny zestaw tego samego cBota już używa, jest odrzucane (jasny
błąd w dialogu, `409 Conflict` w API). Tę samą nazwę można **ponownie** użyć na **innym** cBocie.

JSON jest **walidowany** przy zapisie: musi to być pojedynczy płaski obiekt, którego wartości są wszystkie skalarne
(string / number / bool). Root niebędący obiektem, tablica, zagnieżdżony obiekt, wartość `null`, lub źle sformułowany
JSON jest odrzucany (jasny błąd w dialogu, `400 Bad Request` w API). Pusty obiekt `{}`
jest dozwolony i oznacza "brak przesłonięć".

## cTrader Console CLI notes

Backtesty potrzebują `--data-mode` (domyślnie `m1`), dat jako `dd/MM/yyyy HH:mm`, i
`params.cbotset` JSON argument pozycyjny; `run` odrzuca `--data-dir` (tylko backtest). Patrz
`ContainerCommandHelpers`.

## Nodes & scale

Zdolność wykonawcza skaluje się poprzez dodawanie agentów węzłów (samorządowy rejestr + heartbeat). Patrz
[node discovery](../operations/node-discovery.md) i [scaling](../deployment/scaling.md).
## A trading account is required

Uruchomienie lub przetestowanie wstecz cBota wymaga konta handlowego cTreadera, do którego się podłączy. Dopóki
nie dodasz go w obszarze **Trading accounts**, przyciskami **Run New cBot** / **Backtest New cBot** są wyłączone (ze
wskazówką) a strona pokazuje ostrzeżenie łączące do ustawienia konta — nie trafisz już na surowy
błąd `stream connect failed` od bota bez konta.
