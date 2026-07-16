---
description: "Budowanie, uruchamianie i testowanie wstecz cBotów cTradera (C# i Python, oba .NET) z wbudowanego edytora Monaco w przeglądarce, uruchamianie na oficjalnym obrazie ghcr.io/spotware/ctrader-console."
---

# Build & backtest cBotów

Budowanie, uruchamianie i testowanie wstecz cBotów cTradera (C# **i** Python, oba .NET) z wbudowanego edytora Monaco w przeglądarce, uruchamianie na oficjalnym obrazie `ghcr.io/spotware/ctrader-console`.

## Build

- **Builder** page hostuje edytor Monaco; `CBotBuilder` kompiluje projekt za pomocą
  `dotnet build` **w kontenerze jednorazowym** (`AppOptions.BuildImage`, katalog pracy bind-mount
  w `/work`), więc niezaufane cele MSBuild użytkownika nie osiągają gospodarza. Przywracanie NuGet jest buforowane
  między kompilacjami poprzez wolumin współdzielony. Host sieciowy potrzebuje dostępu do gniazda Docker.
- Szablony startowe C# + Python znajdują się w `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = hierarchia stanów TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Przejście zastępuje jednostkę (zmiana id),
  container id jest przenoszona.
- `NodeScheduler` wybiera najmniej obciążony kwalifikujący się węzeł; `ContainerDispatcherFactory` trasuje do
  zdalnego agenta HTTP węzła lub lokalnego dyspozytora Docker.
- Pollewy uzupełniające uzgadniają wychodzące kontenery (kontenery backtest samoczynnie wychodzą przez
  `--exit-on-stop`); raport obecny → ukończony (przechowywanie `ReportJson`), brakujący → nieudany.
- Dzienniki kontenera na żywo przesyłają się do przeglądarki przez SignalR; krzywe kapitału testów wstecz analizowane z
  raportu + wykreślane.

## Dane rynkowe backtest są buforowane na konto

cTrader Console pobiera historyczne dane tick/bar do swojego `--data-dir`. Ten katalog jest
**stabilną, trwałą pamięcią podręczną opartą na koncie handlowym** (jego numerem konta) — bind-mounted z
dysku węzła na jego własnym zbiórze kontenerów (`/mnt/data`), **oddzielnym, niezagnieżdżonym montażem** od
katalogów pracy per-instance. Tak więc każdy backtest na tym samym koncie **ponownie wykorzystuje** już pobrane dane
zamiast ponownie pobierać je za każdym razem. (Wcześniej
katalog danych znajdował się w katalogu pracy per-instance, którego id zmienia się za każdym razem, co zmuszało do świeżego
pobrania każdego backtestu.) Efemeryczny katalog pracy per-instance nadal zawiera algorytm, parametry, hasło
i raport; buforowana pamięć danych jest liczona w użyciu danych backtest węzła i wyczyszczana przez
akcję czyszczenia węzła.

## Ustawienia backtest

Dialog **Backtest** ujawnia każde ustawienie, które akceptuje CLI backtest cTrader Console, więc nigdy
nie trzeba dotykać wiersza poleceń:

- **From / To** — okno backtestu (`--start` / `--end`).
- **Data mode** — `m1` (słupki 1-minutowe) lub `tick` (`--data-mode`).
- **Starting balance** — domyślnie `10000` (`--balance`). **Saldo 0 nie zawiera transakcji i sprawia, że
  cTrader emituje pusty raport, na którym się zawiesza** ("Message expected"), więc zawsze wysyłane jest saldo niezerowe.
- **Commission** i **Spread** (`--commission` / `--spread`, spread w pipach).
- **Advanced options** — pole formularza `name=value` na linię dla dowolnej innej opcji backtest, którą cTrader
  obsługuje (np. `applyCommissionAutomatically=true`); każda linia staje się argumentem CLI `--name value`.

## Strona szczegółów instancji

Otwarcie instancji (`/instance/{id}`) pokazuje jej status na żywo, dzienniki i — dla backtestu — krzywą kapitału.
**Tytuł karty przeglądarki** odzwierciedla konkretną instancję (**nazwa cBota · typ · symbol**, np.
`TrendBot · Backtest · EURUSD`), więc karta z bieżącym uruchomieniem i karta z backtest są rozróżnialne na pierwszy rzut oka.
Uruchomienie i backtest tego samego cBota są śledziane jako odrębne **linie** (stabilny id linii przenoszona
przez przejścia stanu), więc strona podąża dokładnie za jedną instancją i nigdy nie miesza danych przebiegu z
backtestem.

## Formanty cyklu życia instancji

Każdy wiersz instancji (i jego strona szczegółów) posiada formanty zgodne ze stanem. Instancja **aktywna** pokazuje
**Stop**; instancja **terminalna** (Stopped / Completed / Failed) pokazuje **Start (▶)**, aby ją ponownie uruchomić za
ten sam cBot, konto, symbol, timeframe, zestaw parametrów i obraz (uruchomienie restartuje się jako uruchomienie,
backtest jako backtest). Kliknięcie Stop pokazuje zawiadomienie "Stopping…" i wyłącza ikonę do czasu rozwiązania,
a nowo utworzony przebieg pojawia się na liście natychmiast — bez przeładowania strony.

Dzienniki konsoli są **utrwalane, gdy instancja kończy pracę** — zarówno dla przebiegu (na Stop), jak i dla
**backtestu** (na ukończenie) — więc dzienniki ostatniego przebiegu pozostają widoczne na stronie szczegółów i,
za pośrednictwem paska narzędzi dziennika, są **kopiowane do schowka** (ikona Kopiuj dzienniki) lub **pobierane**
(ikona Pobierz dzienniki) nawet po usunięciu kontenera. Obie działają na pełnym dzienniku konsoli instancji, nie tylko
na widocznym ogonie.

Przesłany plik `.algo` nigdy nie został tutaj zbudowany, więc jego kolumna **Last Build** na stronie cBotów jest
pusta (pokazuje czas kompilacji tylko dla cBotów, które budujesz w przeglądarce).

## Edytuj i ponownie uruchom zatrzymaną instancję

**Zatrzymana** instancja (uruchomienie lub backtest) ma formant **Edit** — ikonę w jej wierszu na liście **i**
obok Start/Stop na jej stronie szczegółów — która otwiera dialog **wstępnie wypełniony** z jej bieżącą konfiguracją.
Możesz zmienić **konto handlowe, symbol, timeframe, zestaw parametrów i tag obrazu** (i, dla backtestu,
**okno i wszystkie ustawienia backtest** powyżej), następnie **Save & start** ponownie uruchamia go
z nowymi ustawieniami (zastępując zatrzymaną instancję). Formant jest **wyłączony, gdy instancja jest aktywna** —
tylko zatrzymaną instancję można edytować.

## Uruchomienie z edytora kodu

Kliknięcie **Run** w edytorze kodu otwiera dialog zamiast uruchamiać ślepe, zakodowane uruchomienie:

- **Trading account** (wymagane) — konto handlowe cTradera, do którego łączy się cBot.
- **Parameter set** (opcjonalnie) — wybierz istniejący zestaw lub pozostaw go puste, aby uruchomić z
  **domyślnymi wartościami parametrów** cBota. Przycisk **+** obok selektora tworzy nowy zestaw parametrów
  w miejscu (patrz poniżej) i go wybiera.
- **Symbol / Timeframe** domyślnie ustawiane na `EURUSD` / `h1` i można je zmienić; **Cancel** lub **Run**.

Na **Run** edytor zapisuje + kompiluje bieżące źródło, uruchamia instancję na wybranym koncie
z wybranymi parametrami, a następnie śledzi dzienniki kontenera na żywo. (Strumień dziennika przekazuje
plik cookie uwierzytelniania zalogowanego użytkownika do hubu SignalR `/hubs/logs`, więc się łączy zamiast
nie powieść z `Invalid negotiation response received`.)

## Parameter sets

**Parameter set** to nazwany, wielokrotnie używany zestaw przesłonięć parametrów cBota przechowywany jako płaski obiekt JSON
mapujący każdą nazwę parametru na wartość skalarną, np. `{"Period": 14, "Label": "trend"}`. Podczas
uruchamiania/testowania wstecz jest zamieniany na plik cTradera `params.cbotset`
(`{ "Parameters": { … } }`). Możesz utworzyć/edytować zestaw jako surowy JSON z dialogu **Parameter
sets** cBota lub w miejscu z dialogu Run.

Każdy zestaw parametrów **należy do cBota**: dialog New Parameter Set wyświetla wszystkie Twoje cBoty i musisz
**wybrać jeden** — tworzenie jest zablokowane do czasu wybrania cBota. Nazwa zestawu jest **unikalna na cBota**:
utworzenie lub zmiana nazwy zestawu na nazwę, którą już używa inny zestaw tego samego cBota, jest odrzucane (wyraźny
błąd w dialogu, `409 Conflict` w API). Tę samą nazwę można ponownie wykorzystać na **innym** cBocie.

JSON jest **zatwierdzany** podczas zapisywania: musi być pojedynczym płaskim obiektem, którego wartości są wszystkie skalarne
(string / number / bool). Pierwiastek obiektu innego niż, tablica, obiekt zagnieżdżony, wartość `null` lub źle
sformułowany JSON jest odrzucany (wyraźny błąd w dialogu, `400 Bad Request` w API). Pusty obiekt `{}`
jest dopuszczony i oznacza "brak przesłonięć".

## cTrader Console CLI notes

Backtesty potrzebują `--data-mode` (domyślnie `m1`), dat jako `dd/MM/yyyy HH:mm` i
JSON `params.cbotset` argument pozycyjny; `run` odrzuca `--data-dir` (tylko backtest). Patrz
`ContainerCommandHelpers`.

## Nodes & scale

Pojemność wykonawcza skaluje się poprzez dodawanie agentów węzłów (samorejestracja + puls). Patrz
[node discovery](../operations/node-discovery.md) i [scaling](../deployment/scaling.md).
## A trading account is required

Uruchomienie lub testowanie wstecz cBota wymaga konta handlowego cTradera, z którym się łączy. Dopóki nie dodasz
konta w **Trading accounts**, przyciski **Run New cBot** / **Backtest New cBot** są wyłączone (z
etykietką narzędzia) i strona pokazuje monit łączący do konfiguracji konta — nie trafisz już na surowy
błąd `stream connect failed` z bota bez konta.
