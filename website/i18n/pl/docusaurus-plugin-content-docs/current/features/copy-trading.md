---
description: "Duplikuj główne konto cTrader na jedno lub więcej kont podrzędnych — między brokerami, między cID — z kontrolą na poziomie każdego celu i uzgodnieniem na poziomie banku."
---

# Kopiowanie transakcji

Duplikuj **główne** konto cTrader na jedno lub więcej kont **podrzędnych** — między brokerami, między cID — z kontrolą na poziomie każdego celu i uzgodnieniem na poziomie banku.

## Koncepty

- **Profil kopiowania** — jedno główne (`SourceAccountId`) + jedno lub więcej **celów**. Cykl życia: `Draft → Running → Paused → Stopped` (`Error` w przypadku błędu). Root agregatu: `CopyProfile` (posiada `CopyDestination`).
- **Cel** — jedno konto podrzędne + pełny zestaw reguł określających sposób kopiowania głównego na niego. Cała konfiguracja na poziomie celu, więc jedno główne może zasilać konserwatywne i agresywne konta podrzędne jednocześnie.
- **Host silnika kopiowania** — działający worker dla profilu (`CopyEngineHost`). Subskrybuje strumień wykonania głównego, stosuje każde zdarzenie do każdego celu.
- **Nadzorca** — `CopyEngineSupervisor`, usługa w tle na każdym węźle. Hostuje przypisane profile, samo się naprawia w całym klastrze (patrz [skalowanie](../deployment/scaling.md)).

## Co się kopiuje

| Zdarzenie główne | Akcja podrzędna |
|--------------|--------------|
| Otwieranie pozicji na rynku / w zakresie rynku | Otwiera kopię o określonej wielkości (oznaczoną identyfikatorem pozycji źródła) |
| Oczekujące zlecenie limitowane / stop / stop-limit | Umieszcza pasujące oczekujące zlecenie, niosące stop-loss / take-profit głównego |
| Zmiana oczekującego zlecenia | Zmienia lustrzane oczekujące zlecenie na miejscu (w tym jego stop-loss / take-profit) |
| Anulowanie / wygaśnięcie oczekującego zlecenia | Anuluje lustrzane oczekujące zlecenie |
| Częściowe zamknięcie | Zamyka ten sam odsetek pozycji podrzędnej |
| Scale-in (wzrost wolumenu) | Otwiera dodany wolumin (opcjonalnie) |
| Zmiana stop-loss / trailing-stop | Zmienia ochronę pozycji podrzędnej |
| Pełne zamknięcie | Zamyka kopię podrzędną |

Każda kopia **oznaczona identyfikatorem pozycji/zlecenia źródła**. Po ponownym połączeniu host odbudowuje stan z uzgodnienia: otwiera kopie, które główny przechowuje, ale podrzędnemu brakuje, zamyka „sieroty" podrzędne, których główny już nie przechowuje — **bez duplikowania transakcji**.

## Tworzenie profilu

**Nowy profil** otwiera dedykowany **formularz na pełną stronę** (`/copy-trading/new`), nie dialog — zestaw opcji jest wystarczająco duży, aby strona czytała się lepiej na telefonie i komputerze stacjonarnym. Zbiera wszystko z góry: nazwę profilu, konto źródło (główne), konta docelowe (podrzędne) (wieloselekt z przyciskiem **Wybierz wszystkie**; wybrane główne wykluczone z listy podrzędnych), + pełny zestaw opcji na poziomie każdego celu. **Każdy kontroler ma tooltip pomocy** wyjaśniający, co robi i jak go używać. Strukturalne wejścia używają **właściwych zwalidowanych kontrolerów** — liczby/procenty poprzez pola numeryczne, tryby/kierunek/filtr poprzez selecty, filtr symbolu poprzez listę chipów do dodawania/usuwania symboli, a mapę symboli poprzez tabelę do dodawania/usuwania wierszy `Źródło → Cel (× mnożnik)` — nigdy nie blobem tekstu rozdzielanego przecinkami. Wszystkie wejścia **zwalidowane przed zapisaniem** — brakująca nazwa/źródło/cel, nizsumowy parametr grubości, ujemne/niespójne granice partii, procent drawdown poza zakresem, brak włączonego typu zlecenia, lub pusty filtr symbolu pojawia się jako lista błędów + blokuje zapis. Przy tworzeniu profil jest tworzony + każdy wybrany podrzędny jest dodawany z wybranymi ustawieniami, a następnie strona powraca do listy Kopiowania transakcji.

**Import / eksport.** Cały blok ustawień można **wyeksportować do pliku JSON** i **ponownie zaimportować** w celu wstępnego wypełnienia formularza, aby tuning można było ponownie wykorzystać na profilach bez ponownego wpisywania. Mapa symboli może być podobnie **eksportowana / importowana jako plik CSV** (`Source,Destination,VolumeMultiplier`) — przygotuj dużą mapę symboli brokera w arkuszu kalkulacyjnym i załaduj ją w jednym kroku. Te same kontrolery symboli i import/eksport CSV są również dostępne w dialogu celu na stronie Kopiowania transakcji.

Akcje wierszy szanują cykl życia: **Start** włączony tylko, gdy nie jest uruchomiony, **Stop** + **Pauza** tylko gdy jest uruchomiony, **Usunięcie** wyłączone, gdy jest uruchomiony + pyta potwierdzenie przed usunięciem profilu + celów.

Nowo uruchomiony profil przez krótki czas pokazuje status **Starting** (nie zielony *Running*), podczas gdy jego host ładuje dane referencyjne i uruchamia pierwszą resynchronizację — nie mirroring jeszcze zleceń między celami. Przełącza się do **Running** w momencie, gdy ta pierwsza resynchronizacja się zakończy i silnik będzie mógł kopiować. Starting jest traktowany jako uruchomiony dla kontrolek wiersza (Start wyłączony, Stop i live-logs włączone, Edit/Delete zablokowane), więc rozgrzewający się profil nie może być ponownie uruchomiony ani edytowany podczas startu. Faza rozgrzewania jest śledzona w procesie na węźle hostującym profil; profil hostowany na innej replice (lub taki, który nie może być hostowany — jego konta źródłowe/docelowe nie są powiązane poprzez Open API) pokazuje swój zwykły status.

## Opcje na poziomie każdego celu

Ustawiane na stronie Nowy profil, w dialogu celu na stronie Kopiowania transakcji, lub poprzez `POST /api/copy/profiles/{id}/destinations`:

- **Grubość** (`MoneyManagementMode` + parametr): stała partia, partia/notional mnożnik, proporcjonalna równowaga/akcje/wolne marża, stały odsetek ryzyka, stała dźwignia, auto-proporcjonalna, **ryzyko-%-z-stop** (M7). Ponadto min/max granice partii + siła-min-partii. **Ryzyko-z-stop** zmienia cel, aby ryzykował skonfigurowany procent *jego własnej* równowagi, pochodzi ze **odległości stop-loss głównego** (`główny ryzyka 2% → podrzędny automatycznie-ryzyka 2%`): `partie = równowaga×% ÷ (odległość stop × wielkość kontraktu)`. Główne otwarte **bez** stop-loss nie ma odległości do grubości względem → używa skonfigurowanej **maksymalnej partii fallback ryzyka** (M7) jeśli jest ustawiona, w przeciwnym razie pominięte (`no_stop_loss`) nie zgadywane. Proporcjonalna-**akcje**/**wolna marża** rozmiaru poza rzeczywistym kontem **akcje** (`równowaga + Σ pływające P&L`, pochodzi na cTrader Open API, która nie dostarcza akcji), a nie zwykła równowaga — więc główny siedzący na otwartym zysku/stracie zmienia rozmiar kopii prawidłowo. Używana marża nie jest ujawniana przez API uzgodnienia, dlatego wolna marża traktowana jest jako akcje (uczciwy zastępnik dostępnych funduszy); inne tryby czytają równowagę + pomijają dodatkową runę rewaloryzacji.
- **Filtr kierunku**: zarówno / tylko długie / tylko krótkie. **Odwrotnie**: odwracaj stronę (+ swap SL↔TP) do przeciwnego kopiowania.
- **Zarządzaj-tylko** (Ignoruj-Nowe-Transakcje / Tylko-Zamknięcie): lustrzane zamknięcia, częściowe zamknięcia + zmiany ochrony na już skopiowanych pozycjach, ale otwieranie **brak** nowych pozycji/oczekujących zleceń (pominięte `manage_only`). Używaj do zmniejszania celu bez cięcia istniejących kopii.
- **Sync-Open-on-start** / **Sync-Closed-on-start** (domyślnie włączone): przy **pierwszej** resynchronizacji profilu, czy otworzyć kopie dla istniejących wcześniej pozycji głównego, + czy zamknąć kopie zamknięte głównego, gdy profil był zatrzymany. Oba stosują się tylko na start — ponowne połączenie w trakcie biegu zawsze w pełni uzgadnia, więc desynchronizacja odzyskuje niezależnie.
- **Mapa symboli** + **filtr symbolu** (lista białych/czarnych). Każdy wpis mapy symboli zawiera opcjonalny **mnożnik wolumenu na symbol** (cMAM przesłonięcie na symbol) skalujący rozmiar kopii dla tego symbolu na górze grubości celu (1 = bez zmian). Cała mapa importuje/eksportuje się jako **CSV** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; kolumny `Source,Destination,VolumeMultiplier`) — każdy wiersz zwalidowany poprzez domeny obiekty wartości, więc zdeformowany plik nie może wytworzyć nieprawidłowej mapy.
- **Okno godzin handlowych** (C18) — okno dzienne UTC dla każdego celu (`start`/`end` minuty dnia, koniec wyłączny; `start == end` = cały dzień). Nowe otwierania poza oknem pominięte (`trading_hours`); okno z `start > end` opakowuje przeszłą północ (np. 22:00–06:00). Istniejące pozycje pozostają zarządzane.
- **Filtr etykiety źródła** (C18, cTrader odpowiednik MT magicznego filtra numerów) — gdy jest ustawiony, kopiuj tylko transakcje główne, których etykieta dokładnie pasuje (np. transakcje jednego cBota, lub etykieta ręczna); w innym wypadku pominięte (`source_label`). Pusty = kopiuj wszystkie. Przeniesione na `ExecutionEvent.SourceLabel` z pozycji/zlecenia głównego `TradeData.Label`, honorowane przy resynchronizacji również.
- **Ochrona konta** (ZuluGuard / Globalna ochrona konta) — obserwuj **żywe akcje** celu (`równowaga + Σ pływające P&L`, ankietowane co `CopyDefaults.EquityGuardInterval`) wobec podłogi `StopEquity` i/lub opcjonalnego sufitu `TakeEquity`. Przy naruszeniu, zastosuj tryb: **CloseOnly** (zatrzymaj nowe kopie, zarządzaj istniejącymi), **Frozen** (zatrzymaj otwieranie), **SellOut** (zamknij **każdą** kopię na celu natychmiast). Po wyzwoleniu, cel zatrzaśnięty — brak nowych otwarć do restartowania hosta — + alert `CopyAccountProtectionTriggered` wyświetlony. `SellOut` wymaga `StopEquity`; `TakeEquity` musi siedzieć powyżej `StopEquity`. **Brak gwarancji caveat:** sell-out używa wykonania rynkowego — jak każdy równoległy, nie może gwarantować ceny wypełnienia na szybkim/gapowanym rynku.
- **Flatten-All panic button** (C8) — `POST /api/copy/profiles/{id}/flatten` natychmiast zamyka **każdą** skopiowaną pozycję na każdym celu + blokuje przed nowymi otwieciami. Kierowane poprzez proces: API ustawia flagę, nadzorca dostarcza uruchomionym hostom (ponownie używając kanału rotacji tokenu), które spłaszczają się na miejscu; flaga wyczyszczona, aby wyzwoliła się dokładnie raz (`CopyFlattenAll` alert). Użytkownik następnie pauzuje/zatrzymuje profil.
- **Prop-firm rule guard** (C7) — egzekwowanie prop-firm copier użytkownikami. Na cel, **dzienne ograniczenie strat** (strata ze zmienną otwierającą dzień) i/lub **trailing-drawdown** limit (strata ze zmienną szczytu), oba w walucie depozytu. Przy naruszeniu cel **auto-spłaszczony** (każda kopia zamknięta) + **zablokowany** reszta dnia UTC (nowe otwierania pominięte `prop_lockout`); alert `CopyPropRuleBreached`. Blokada czyszczona, gdy dzień UTC się zmienia (świeża linia bazowa/szczyt brana). Dzieli tę samą ankietę live-equity, co ochrona konta.
- **Jitter wykonania** (C11, domyślnie wyłączone) — losowe opóźnienie `0..N` ms przed umieszczeniem każdej kopii, aby zdekrelować prawie identyczne znaczniki czasu zlecenia na własnych kontach użytkownika **własnych**. **Compliance caveat:** pomoc dla firm prop, które *zezwalają* na kopiowanie — **nie** narzędzie do uniknięcia firmy, która je zabrania; pozostanie w granicach zasad firmy jest twoją odpowiedzialnością.
- **Config lock** (C9) — zamraź ustawienia celu na okres (`POST …/destinations/{id}/lock` z minutami). Gdy zablokowana, cel nie może być usunięty (agregat odrzuca z `CopyDestinationConfigLocked`) — celowa ochrona przed impulsywnymi zmianami podczas drawdown. Blokada wygasa automatycznie w swoim znaczniku czasu.
- **Consistency pre-alert** (C10) — ostrzegaj (raz na dzień UTC), gdy **dzienny zysk** celu osiąga skonfigurowany procent zmiennej otwierającej dzień (`CopyConsistencyThresholdApproaching`), więc reguła spójności prop-firm poszanowana *przed* wyzwoleniem. Strona zysku, niezależna od blokady strony strat; działa poza tą samą linią bazową dnia, co prop-rule guard.
- **Filtr typu zlecenia** — wybierz dokładnie, które główne typy zleceń kopiować: rynek, zakres rynku, limit, stop, stop-limit (`CopyOrderTypes` flagi; domyślnie wszystkie). Selektywność w stylu cMAM.
- **Kopiuj SL / Kopiuj TP** — lustrzaj stop-loss / take-profit główny, lub zarządzaj ochroną niezależnie. Stosuje się do **zarówno** otwartych pozycji **jak i** resting pending orders — limit/stop/stop-limit kopia jest umieszczana i zmieniana za pomocą SL/TP głównego zlecenia (wymieniane pod **Reverse**), więc ochrona jest dołączana w momencie, gdy pending wypełnia, nie tylko później.
- **Kopiuj trailing stop**, **lustrzaj częściowe zamknięcie**, **lustrzaj scale-in** — każde niezależnie przełączalne.
- **Kopiuj wygasanie oczekującego** (domyślnie włączone) — lustrzaj znacznik czasu wygasania Good-Till-Date zlecenia oczekującego głównego.
- **Kopiuj slippage główny** (domyślnie włączone) — dla zlecenia zakresu rynku + stop-limit, umieść zlecenie podrzędne z dokładnym slippage-w-pipsach głównego (cena bazowa pobierana ze żywej plamki podrzędnej).
- **Ochrony**: maksymalny procent drawdown, dzienne ograniczenie strat, maksymalne opóźnienie kopii, filtr slippage (pomiń kopię, jeśli cena podrzędna przesunęła się poza N pipsów od wpisu głównego). **Maksymalne opóźnienie kopii** zmierzone względem rzeczywistego znacznika czasu serwera zdarzenia głównego (`ExecutionEvent.ServerTimestamp`) poprzez wtryskiwany `TimeProvider`: sygnał starszy niż maksymalny lag skonfigurowany pominięty, więc stara kopia nigdy nie umieszczona late (poprzednio opóźnienie zawsze zero + ochrona martwa).
- **Normalizacja precyzji SL/TP** (M6) — skopiowane ceny stop-loss/take-profit zaokrąglone do **precyzji cyfrowej** symbolu celu przed zmianą (na pozycjach **i** pending-order placement/amend), więc główna cena przy fiszej precyzji (lub niedopasowanie cyfrowe między brokerami) nigdy nie wyzwala serwera `INVALID_STOPLOSS_TAKEPROFIT`.
- **Obwód wyłącznika odrzucenia / Follower Guard** (G8) — cel odrzucający `CopyDefaults.RejectionBudget` otwarcia w rzędzie jest **wyzwolony**: brak nowych otwarć dla okna cooldown (`CopyDestinationTripped` alert wyzwolony), zatrzymując burzę odrzucenia przed uderzaniem (prop-firm) konta. Istniejące pozycje nadal zarządzane + zamknięte wyzwolone; wyłącznik automatycznie resetuje się po cooldown + powodzenie kopii czyszcza licznik.
- **Sufit rozsądności partii** (C14) — absolutny maksymalny rozmiar kopii i/lub wielokrotność głównej czepka. Obliczona kopia przekraczająca absolutny cap, lub przekraczająca `N×` własną partię głównego, **hard-blocked** (wyświetlana jako `lot_sanity` pomiń, zliczana na `cmind.copy.skipped`) nie umieszczona — broni przed katastrofalną klasą przerwania (0.23-lot główny zamieniający się w 3 partie na każdym odbiorcy poprzez mnożnik uciekający lub bug zaokrąglenia). Oba wymiary domyślnie `0` (wyłączone).

## Niezawodność i przypadki graniczne

Silnik zbudowany na rzeczywistości, że wszystko może się nie powieść w każdej chwili:

- **Timeout korelacji wypełnienia oczekującego podrzędnego** (C13) — lustrzane oczekujące podrzędne, którego główne zniknęło (ani nie leży ani nie zostało świeżo wypełnione) anulowane po timeout korelacji, więc kopia podrzędna nie może wypełnić bez korelacji do niezarządzanej pozycji (`CopyPendingTimedOut`). Resynchronizacja również czyszcza sierotę wypełnioną oczekującą oznaczoną identyfikatorem zlecenia.
- **Wyścig wypełnienia pending między brokerami** — własne pending podrzędnego może się wypełnić (jego cena uderzyła) w małym oknie zanim zdarzenie wypełnienia/anulowania głównego będzie przetwarzane. To pozostawia pozycję podrzędną oznaczoną **identyfikatorem zlecenia** źródła, którą ścieżki kanoniczne zamknięcia/SL-TP (zarządzane przez **identyfikator pozycji** źródła) mogą pominąć. Na **wypełnieniu** głównym wczesne wypełnienie podrzędnego jest wycofywane i zastępowane jedną kanonicznie-oznaczoną kopią rynkową — więc cel kończy się dokładnie **jedną** kopią, nigdy podwojoną pozycją; na **anulowaniu** głównym jest natychmiast zamknięty (główny nigdy nie podjął transakcji). Oba działają natychmiast, nie tylko przy następnej resynchronizacji. Trafienie SL/TP po stronie podrzędnego, które zamyka kopię, którą główny nadal posiada, jest sterowane źródłem i ponownie otwierane przy następnym uzgodnieniu (silnik mirroring **zdarzenia główne**; nie konsumuje wykonania po stronie docelowej).
- **Solidne zamknięcie/spłaszczenie** (M8) — zamknięcie sieroty przy resynchronizacji, lub spłaszczenie przy naruszeniu ochrony, toleruje pozycję brokera już zamkniętą (`POSITION_NOT_FOUND`): każde zamknięcie działa niezależnie, więc jeden stary id nigdy nie przerywa resynchronizacji ani nie pozostawia reszty konta bez spłaszczeń.

- **Start z głównym już w transakcjach** — na start host uzgadnia + otwiera kopie dla istniejących pozycji głównych.
- **Upadek połączenia / desynchronizacja** — przy ponownym połączeniu host uzgadnia: otwiera brakujące kopie, zamyka sieroty, re-etykietuje oczekujące. Brak zduplikowanych zleceń.
- **Błąd umieszczenia zlecenia** — błąd na jednym celu zarejestrowany, nigdy nie blokuje inne cele.
- **Pojedynczy ważny token na cID** — cTrader unieważnia stary token dostępu cID moment, gdy nowy został wydany. cMind zamiania token uruchomionego hosta **na miejscu** (re-auth na żywym gnieździe) tak, aby kopiowanie kontynuowało się bez porzucania strumienia. Patrz [cykl życia tokenu](token-lifecycle.md).

## Audytowalność

Każda akcja emituje strukturalne, wygenerowane źródłem zdarzenie dziennika (`LogMessages`) z id profilu, cID celu, id zleceń/pozycji, + wartościami — zlecenie umieszczone/pominięte (z powodem), częściowe zamknięcie, zastosowana ochrona, zastosowany trailing, oczekujące umieszczone/zmienione/anulowane, wygaśnięcie lustrzane, slippage zakresu rynku lustrzane, token zmieniony, podsumowanie resynchronizacji. To jest szlak audytu dla compliance + rozwiązywania sporów.

Obok dzienników, silnik emituje **metryki OpenTelemetry** na mierze `cMind.Copy` (zarejestrowanej w współdzielonej rurze OTel, eksportowanej poprzez OTLP / do Azure Monitor jak reszta): `cmind.copy.latency` (główne zdarzenie → dispatch, ms), `cmind.copy.dispatch.duration` (fan-out do wszystkich celów, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (otagowane przez cel), `cmind.copy.skipped` (otagowane powodem), + `cmind.copy.failed`. To czyni regresję latencji/slippage mierzalną, a nie tylko widoczną w linii dziennika — live suite asertuje je przeciwko budżetowi.

## API

- `GET /api/copy/profiles` — lista.
- `POST /api/copy/profiles` — stwórz (z opcjonalnymi id kont docelowych).
- `GET /api/copy/profiles/{id}` — pełny szczegół incl. każdą opcję celu.
- `POST /api/copy/profiles/{id}/destinations` — dodaj cel z pełnym zestawem opcji.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — usuń.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — cykl życia.

## Testy

- **Unit** (`tests/UnitTests/CopyTrading`) — tryby grubości, filtry decyzji, filtr typu zlecenia, kopia wygasania, slippage zakresu rynku/stop-limit, przełączniki SL/TP, częściowe zamknięcie, zmiana/anulowanie oczekującego, start-z-otwarciem, disconnect→desynchronizacja→resynchronizacja, in-place zmiana tokenu, unieważnienie cross-cID. Działa przeciwko `FakeTradingSession`, cTrader-faithful symulatorze w pamięci.
- **Integracja** (`tests/IntegrationTests/CopyLive`) — afinność węzła/roszczeń dzierżawy, propagacja wersji tokenu na rzeczywistym Postgres.
- **E2E** (`tests/E2ETests`) — zaokrąglenie opcji celu poprzez API + interfejs użytkownika, pełny cykl życia.
- **Stress / DST** (`tests/StressTests`) — deterministic-simulation testing: obsługi pracowników losowych + iniekcja błędów (socket flap, odrzucenie zlecenia, odrzucenie zakresu rynku, rotacja tokenu, śmierć węzła) dysk `CopyEngineHost` do quiescence + assert spójności wariantów. Patrz [testing/stress-testing.md](../testing/stress-testing.md). Ta suite wyświetliła + naprawiła rzeczywisty wyścig startowy: `OnReconnected` przewód przed ładunkiem referencji początkowej + resynchronizacja, więc flap gniazda podczas startu mógł uruchomić drugą resynchronizację jednocześnie + uszkodzić słowniki stanu hosta bez współbieżności — ładunek startup + pierwsza resynchronizacja teraz działają poniżej `_stateGate`.
- **Live** — rzeczywiste konta demo cTrader; patrz [testing/live-copy-trading.md](../testing/live-copy-trading.md).

Patrz [dev-credentials.md](../testing/dev-credentials.md) dla pojedynczego pliku poświadczeń live + E2E tiers czytają.

## Kontrole profilu i zarządzanie celami

Start/stop to przyciski ikon w każdym wierszu profilu (wyłączone, gdy akcja nie ma zastosowania). Źródło i
konta docelowe pokazywane są po ich **numeru konta**, nigdy wewnętrznemu id. Kliknięcie profilu
otwiera **dialog** do zarządzania kontami docelowymi (dodaj/usuń z pełnymi ustawieniami na poziomie każdego celu).
