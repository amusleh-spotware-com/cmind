# Commitment of Traders (COT)

cMind zawiera wbudowany raport **Commitment of Traders** — cotygodniowy podział CFTC dotyczący tego, kto
jest w długiej i krótkiej pozycji na rynku futures USA (handlowcy komercyjni, wielcy spekulanci, fundusze),
z interaktywnymi wykresami historycznymi, znormalizowanym **indeksem COT**, uwierzytelnionym API REST
dla botów cBot i narzędziami MCP dla klientów AI. Dane pochodzą bezpośrednio z **publicznych zestawów danych CFTC Socrata** —
brak klucza API, brak agregata. Jak kalendarz ekonomiczny, jest to moduł niezależny, który można wyłączyć bez
wpływu na rdzeń handlowy.

## Czego to daje

- **Wszystkie trzy rodziny raportów, tylko futures i futures + opcje razem:**
  - **Legacy** — Non-Commercial (wielcy spekulanci), Commercial (hedgerzy), Non-Reportable.
  - **Disaggregated** — Producer/Merchant, Swap Dealers, Managed Money, Other Reportables.
  - **Traders in Financial Futures (TFF)** — Dealer, Asset Manager, Leveraged Funds, Other Reportables.
- **Wyselekcjonowany katalog rynków** — pary walutowe forex, złoto/srebro/miedź, ropa naftowa & gaz ziemny,
  obligacje skarbowe, indeksy giełdowe, kryptowaluty i główne zboża/towary miękkie — każda zmapowana do
  jej stabilnego kodu kontraktu CFTC i, gdzie jednoznaczne, do symbolu handlowego (np. Euro FX → `EURUSD`, Złoto → `XAUUSD`).
- **Indeks COT (0–100)** — gdzie bieżąca netto pozycja spekulanta znajduje się w ramach jej zakresu historycznego
  (domyślnie ~3-letni lookback). Odczyty bliskie ekstremom sygnalizują zatłoczenie pozycji, które często
  poprzedza odwrócenie; raport oznacza **długą ekstremę** (≥80) lub **krótką ekstremę** (≤20).
- **Poprawność punktu w czasie.** Raport tygodniowy mierzony jest we wtorek, ale становится publiczny dopiero w piątek;
  każdy odczyt honoruje ten moment publikacji, więc sygnał pozycjonowania w backteście nigdy nie widzi raportu
  przed jego opublikowaniem (bez look-ahead).

## Korzystanie ze strony

Otwórz **Commitment of Traders** z lewej nawigacji. Wybierz **rynek**, **typ raportu** (Legacy /
Disaggregated / Financial) i przełącz **Futures + opcje**, aby przełączać się między samymi futures
a wariantem połączonym. Strona pokazuje:

- **Pozycjonowanie netto w czasie** — interaktywny wykres liniowy pozycji netto (long − short) każdej kategorii
  handlowca w oknie historii.
- **Indeks COT** — wykres liniowy indeksu 0–100 z najnowszym odczytem i jego etykietą ekstremalną.
- **Najnowsza migawka** — tabela long / short / net / % otwartych odsetek na kategoriję handlowca, plus
  całkowite otwarte odsetki i datę raportu.

Każdy wykres zawiera przyciski paska narzędzi **powiększenia / pomniejszenia** (i resetowania), a możesz przeciągać wzdłuż osi czasu, aby powiększyć. **Export CSV** pobiera pełną cotygodniową historię wybranego rynku i typu raportu jako plik gotowy do arkusza kalkulacyjnego. Użyj **Compare markets**, aby nałożyć kilka rynków na jednym wykresie — wykresy porównawcze wykreślają netto pozycję spekulantów każdego wybranego rynku i indeks COT obok siebie, abyś mógł przeglądać pozycjonowanie na rynkach na pierwszy rzut oka.

## Jak przepływają dane

Baza danych jest pamięcią podręczną. Cotygodniowy pracownik ingestion pobiera sześć zestawów danych CFTC dla śledzonych rynków, aktualizuje katalog rynków i dodaje każdy nowy raport **idempotentnie** (ponowne uruchomienie nigdy nie duplikuje migawki). Ponadto dane są **ładowane na żądanie**: po raz pierwszy żądany rynek jest pobierany ze źródła CFTC i przechowywany, a każde kolejne żądanie jest obsługiwane bezpośrednio z bazy danych. Pamięć podręczna **odświeża się w miarę publikacji nowych cotygodniowych raportów** — raz gdy najnowszy przechowywany raport ma więcej niż jeden tydzień, następne żądanie transparentnie pobiera i dołącza najnowsze dane (ograniczone, aby źródło nie było nigdy zalewane). Pierwsze ładowanie wstecz wypełnia kilka lat historii; awaria źródła pogarsza się do podawania najlepszych buforowanych danych. Wszystko działa z pudełka bez klucza; opcjonalny token aplikacji Socrata tylko podnosi limit szybkości.

## Konfiguracja

Wszystkie klucze znajdują się w `App:Cot` (zobacz [włączniki funkcji](./feature-toggles.md) i
[ustawienia właściciela white-label](./white-label-owner-settings.md)):

| Klucz | Domyślnie | Cel |
|-----|---------|---------|
| `IngestionEnabled` | `true` | Czy cotygodniowy pracownik ingestion działa. |
| `PollInterval` | `6h` | Jak często pracownik odpytuje zestawy danych CFTC. |
| `BackfillYears` | `5` | Lata historii pobrane przy pierwszym uruchomieniu. |
| `ReconcileLookbackWeeks` | `4` | Ostatnie tygodnie ponownie zsynchronizowane każdy cykl, aby złapać poprawki. |
| `SocrataAppToken` | — | Opcjonalny token, który podnosi anonimowy limit szybkości. |
| `CotIndexLookbackWeeks` | `156` | Cotygodniowe raporty używane jako zakres indeksu COT (~3 lata). |

## Gating

Widoczność to dwustopniowa brama, identyczna z kalendarzem ekonomicznym: twardostojna brama white-label
`App:Branding:EnableCot` (poziom budowy) **i** przełącznik funkcji runtime `App:Features:Cot`. Gdy którekolwiek jest wyłączone,
link nawigacyjny, strona, REST API i narzędzia MCP wszystkie znikają (API zwraca `404`). Ponieważ źródło danych
jest bezklucze, nie ma bramy klucza źródła danych — włączenie oznacza widoczne.

## Dla programistów

- Domena: `Core.Cot` — agregaty `CotMarket` i `CotReport`, obiekt wartości `CotPositions`, usługa domeny
  `CotIndexCalculator` i porty `ICotReports` / `ICotSource`.
- Infrastruktura: `Infrastructure.Cot` — parser anty-korupcji `CftcSocrataSource`, brama szybkości, usługa zapisu
  tylko dodawania, strona odczytów i cotygodniowy pracownik ingestion (schemat EF `cot`).
- Dostęp bota cBot & AI: [COT cBot API](./cot-cbot-api.md) (REST, `market:read` JWT) i narzędzia MCP
  `CotMarkets`, `CotLatest`, `CotHistory`, `CotHealth`.
