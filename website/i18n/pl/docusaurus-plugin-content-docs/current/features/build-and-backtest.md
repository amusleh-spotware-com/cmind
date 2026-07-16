---
description: "Buduj, uruchamiaj i testuj cBoty cTrader (C# i Python, oba na .NET) z przeglądarki w edytorze Monaco, uruchamiaj na oficjalnym obrazie ghcr.io/spotware/ctrader-console."
---

# Budowanie i testowanie cBotów

Buduj, uruchamiaj i testuj cBoty cTrader (C# **i** Python, oba na .NET) z przeglądarki w edytorze Monaco, uruchamiaj na oficjalnym obrazie `ghcr.io/spotware/ctrader-console`.

## Budowanie

- Strona **Builder** obsługuje edytor Monaco; `CBotBuilder` kompiluje projekt za pomocą `dotnet build` **w kontenerii jednorazowym** (`AppOptions.BuildImage`, katalog roboczy bind-mount w `/work`), dzięki czemu niezaufane cele MSBuild użytkownika nie mogą dotrzeć do hosta. Przywracanie NuGet jest buforowane między kompilacjami za pośrednictwem udostępnionego wolumenu. Host sieciowy potrzebuje dostępu do gniazda Docker.
- Szablony początkowe dla C# i Python znajdują się w `src/Nodes/Builder/Templates/`.

## Uruchamianie i testowanie

- **Instances** = hierarchia stanów TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). Przejście zastępuje jednostkę (zmiana id), identyfikator kontenera jest przenoszony.
- `NodeScheduler` wybiera najmniej obciążony kwalifikujący się węzeł; `ContainerDispatcherFactory` kieruje do zdalnego agenta HTTP węzła lub lokalnego dyspozytora Docker.
- Poller uzupełniania uzgadnia zakończone kontenery (kontenery testów automatycznie się zamykają przez `--exit-on-stop`); raport obecny → ukończony (przechowuje `ReportJson`), brakujący → niepowodzenie.
- Dzienniki kontenera na żywo są przesyłane do przeglądarki przez SignalR; krzywe zysku testu są analizowane z raportu i wykreślane.

## Dane rynkowe testu są buforowane na konto

Konsola cTrader pobiera historyczne dane tick/bar do swojego katalogu `--data-dir`. Ten katalog to **stabilny, trwały bufor klucz na koncie handlowym** (numer konta) — bind-mount z dysku węzła na jego własnej ścieżce kontenera (`/mnt/data`), **osobny, zagnieżdżony mount** z katalogiem roboczym na instancję. Dzięki temu każdy backtest na tym samym koncie **ponownie wykorzystuje** już pobrane dane zamiast pobierać je ponownie za każdym razem. (Wcześniej katalog danych znajdował się pod katalogiem roboczym na instancję, którego id zmienia się z każdym uruchomieniem, co zmusiło do nowego pobrania za każdym razem.) Efemeryczny katalog roboczy na instancję nadal zawiera algorytm, parametry, hasło i raport; udostępniony bufor danych jest liczony w użykowaniu danych testowych węzła i wyczyszczony przez akcję czyszczenia węzła.

## Ustawienia testu

Dialog **Backtest** ujawnia ustawienia testów cTrader Console dostrajane przez użytkownika, dzięki czemu nigdy nie musisz dotykać wiersza poleceń:

- **Symbol / Timeframe** — timeframe to **rozwijana lista każdego okresu cTrader** (`t1`…`t1000`, `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1`, i okresy Renko/Range/Heikin), w kanonicznym pisaniu konsoli, dzięki czemu zawsze wybierasz ważny `--period`.
- **From / To** — okno testu (`--start` / `--end`).
- **Data mode** — jeden z trzech trybów cTrader (`--data-mode`): **Tick data** (`tick`, dokładne), **m1 bars** (`m1`, szybkie), lub **Open prices only** (`open`, najszybsze).
- **Starting balance** — domyślnie `10000` (`--balance`). **Saldo 0 nie umieszcza żadnych transakcji i powoduje, że cTrader emituje pusty raport, na którym się sypie** ("Message expected"), więc zawsze wysyłane jest saldo niezerowe.
- **Commission** i **Spread** — `--commission` / `--spread` (spread w pipach).

Katalog danych (`--data-file` / `--data-dir`) jest zarządzany przez samą aplikację (bufor na konto, patrz wyżej), a nie ujawniany w dialogu.

## Strona szczegółów instancji

Otwarcie instancji (`/instance/{id}`) pokazuje jej status na żywo, dzienniki i — dla testu — krzywą zysku. **Tytuł karty przeglądarki** odzwierciedla konkretną instancję (**nazwa cBota · rodzaj · symbol**, np. `TrendBot · Backtest · EURUSD`), dzięki czemu karta uruchomienia na żywo i karta testu są rozróżnialne na pierwszy rzut oka. Uruchomienie i test tego samego cBota są śledzone jako odrębne **linie dziedziczenia** (stabilny identyfikator linii dziedziczenia przeniesiony między przejściami stanów), dzięki czemu strona śledzi dokładnie jedną instancję i nigdy nie miesza danych uruchomienia z testem.

## Kontrole cyklu życia instancji

Każdy wiersz instancji (i jego strona szczegółów) ma kontrole odpowiadające stanowi. Instancja **aktywna** pokazuje **Stop**; **terminalna** (Stopped / Completed / Failed) pokazuje **Start (▶)**, aby ponownie ją uruchomić z tym samym cBotem, kontem, symbolem, timeframe, zestawem parametrów i obrazem (uruchomienie uruchamia się ponownie jako uruchomienie, test jako test). Kliknięcie Stop pokazuje powiadomienie "Stopping…" i wyłącza ikonę do czasu rozwiązania, a nowo utworzone uruchomienie pojawia się na liście natychmiast — bez przeładowania strony.

Dzienniki konsoli są **utrwalane po zakończeniu instancji** — dla uruchomienia (po zatrzymaniu) i dla **testu** (po ukończeniu) — dzięki czemu dzienniki ostatniego uruchomienia pozostają dostępne na stronie szczegółów i, za pośrednictwem paska narzędzi dziennika, są **kopiowane do schowka** (ikona Kopiuj dzienniki) lub **pobierane** (ikona Pobierz dzienniki), nawet po usunięciu kontenera. Oba działają na pełnym dzienniku konsoli instancji, a nie tylko na widocznym ogonie.

Przesłany `.algo` nigdy nie został tutaj zbudowany, więc jego kolumna **Last Build** na stronie cBotów jest pozostawiona pusta (pokazuje czas kompilacji tylko dla cBotów budowanych w przeglądarce).

## Edycja i ponowne uruchomienie zatrzymanej instancji

**Zatrzymana** instancja (uruchomienie lub test) ma kontrolę **Edit** — ikonę na jej wierszu na liście **i** obok Start/Stop na stronie szczegółów — która otwiera dialog **wstępnie wypełniony** jego bieżącą konfiguracją. Możesz zmienić **konto handlowe, symbol, timeframe, zestaw parametrów i tag obrazu** (i, dla testu, **okno i wszystkie ustawienia testu** powyżej), a następnie **Save & start** uruchamia go ponownie z nowymi ustawieniami (zastępując zatrzymaną instancję). Kontrola jest **wyłączona, gdy instancja jest aktywna** — tylko zatrzymaną instancję można edytować.

## Uruchamianie z edytora kodu

Kliknięcie **Run** w edytorze kodu otwiera dialog zamiast uruchamiać ślepe, ustalone uruchomienie:

- **Trading account** (wymagane) — konto handlowe cTrader, z którym łączy się cBot.
- **Parameter set** (opcjonalnie) — wybierz istniejący zestaw lub pozostaw pusty, aby uruchomić z **domyślnymi wartościami parametrów** cBota. Przycisk **+** obok selektora tworzy nowy zestaw parametrów bezpośrednio w miejscu (patrz poniżej) i go wybiera.
- **Symbol / Timeframe** domyślnie do `EURUSD` / `h1` i można je zmienić; **Cancel** lub **Run**.

Po **Run** edytor zapisuje + buduje bieżące źródło, uruchamia instancję na wybranym koncie z wybranymi parametrami, a następnie śledzi dzienniki kontenera na żywo. (Strumień dziennika przekazuje ciasteczko auth zalogowanego użytkownika do centrum SignalR `/hubs/logs`, dzięki czemu połączenie się powiedzie zamiast się nie powieść z `Invalid negotiation response received`.)

## Zestawy parametrów

**Zestaw parametrów** to nazwana, wielokrotnie używana seria zastąpień parametrów cBota przechowywana jako płaski obiekt JSON mapujący każdą nazwę parametru na wartość skalarną, np. `{"Period": 14, "Label": "trend"}`. W czasie uruchomienia/testu jest zamieniany w plik cTrader `params.cbotset` (`{ "Parameters": { … } }`). Możesz utworzyć/edytować zestaw jako surowy JSON z dialogu **Parameter sets** cBota lub bezpośrednio z dialogu Run.

Każdy zestaw parametrów **należy do cBota**: dialog New Parameter Set wyświetla listę wszystkich twoich cBotów i musisz **wybrać jeden** — utworzenie jest blokowane do czasu wybrania cBota. **Nazwa zestawu jest unikalna na cBota**: tworzenie lub zmiana nazwy zestawu na nazwę, którą już używa inny zestaw tego samego cBota, jest odrzucane (wyraźny błąd w dialogu, `409 Conflict` w API). Ta sama nazwa może być ponownie użyta na **innym** cBocie.

JSON jest **zatwierdzany** przy zapisywaniu: musi być pojedynczym płaskim obiektem, którego wartości są wszystkie skalarne (string / number / bool). Root nie-obiektu, tablica, zagnieżdżony obiekt, wartość `null` lub nieprawidłowy JSON jest odrzucany (wyraźny błąd w dialogu, `400 Bad Request` w API). Pusty obiekt `{}` jest dozwolony i oznacza „brak zastąpień".

## Notatki dotyczące wiersza poleceń cTrader Console

Testy wymagają `--data-mode` (domyślnie `m1`), daty jako `dd/MM/yyyy HH:mm`, i JSON `params.cbotset` jako argument pozycyjny; `run` odrzuca `--data-dir` (tylko dla testu). Patrz `ContainerCommandHelpers`.

## Węzły i skala

Pojemność wykonania skaluje się poprzez dodawanie agentów węzłów (samorejestracja + bicie serca). Patrz [odkrywanie węzłów](../operations/node-discovery.md) i [skalowanie](../deployment/scaling.md).

## Wymagane jest konto handlowe

Uruchomienie lub testowanie cBota wymaga konta handlowego cTrader, z którym się połączy. Dopóki nie dodasz go w sekcji **Trading accounts**, przyciski **Run New cBot** / **Backtest New cBot** są wyłączone (z podpowiedzią) i strona pokazuje monit łączący do konfiguracji konta — nie otrzymasz już surowego błędu `stream connect failed` z bota bez konta.
