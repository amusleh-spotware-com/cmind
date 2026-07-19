# Commitment of Traders (COT)

cMind spedisce un report integrato **Commitment of Traders** — la ripartizione settimanale CFTC di chi è
long e short nel mercato dei futures statunitensi (hedger commerciali, grandi speculatori, fondi), con
grafici storici interattivi, un **indice COT** normalizzato, un'API REST autenticata per cBot e
strumenti MCP per client AI. I dati provengono direttamente dai **dataset pubblici CFTC Socrata** — nessuna
chiave API, nessun aggregatore. Come il calendario economico è un modulo disaccoppiato che può essere
disabilitato senza alcun effetto sul nucleo di trading.

## What it gives you

- **Tutte e tre le famiglie di report, solo futures e futures+opzioni combinati:**
  - **Legacy** — Non-Commercial (grandi speculatori), Commercial (hedger), Non-Reportable.
  - **Disaggregated** — Producer/Merchant, Swap Dealers, Managed Money, Other Reportables.
  - **Traders in Financial Futures (TFF)** — Dealer, Asset Manager, Leveraged Funds, Other Reportables.
- **Un catalogo di mercati curato** — coppie di valute FX, oro/argento/rame, petrolio & gas naturale, Treasury, indici azionari, crypto e i principali cereali/soft — ognuno mappato al suo codice di contratto CFTC stabile e, dove inequivocabile, a un simbolo negoziabile (ad es. Euro FX → `EURUSD`, Gold → `XAUUSD`).
- **L'indice COT (0–100)** — dove la posizione netta attuale dello speculatore si trova dentro il suo intervallo storico (lookback predefinito ~3 anni). Le letture vicino agli estremi segnalano posizionamento affollato che spesso precede un'inversione; il report etichetta un **estremo long** (≥80) o **estremo short** (≤20).
- **Correttezza point-in-time.** Un report settimanale è misurato di martedì ma diventa pubblico solo il venerdì seguente; ogni lettura onora quell'istante di rilascio, quindi un segnale di posizionamento backtestato non vede mai un report prima che fosse pubblicato (no look-ahead).

## Using the page

Apri **Commitment of Traders** dalla navigazione sinistra. Scegli un **mercato**, un **tipo di report** (Legacy /
Disaggregated / Financial) e attiva **Futures + options** per passare tra solo futures e la variante
combinata. La pagina mostra:

- **Posizionamento netto nel tempo** — un grafico a linee interattivo della posizione netta (long − short) di ogni categoria di trader
  attraverso la finestra della storia.
- **Indice COT** — un grafico a linee dell'indice 0–100, con la lettura più recente e la sua etichetta di estremo.
- **Snapshot più recente** — una tabella di long / short / net / % dell'interesse aperto per categoria di trader, più
  interesse aperto totale e la data del report.

Ogni grafico ha pulsanti della barra degli strumenti **zoom in / out** (e reset), e puoi trascinare sull'asse temporale per ingrandire. **Export CSV** scarica la cronologia completa settimanale del mercato selezionato e del tipo di report come file pronto per il foglio di calcolo. Usa **Compare markets** per sovrapporre più mercati su un unico grafico — i grafici di confronto tracciano la posizione netta dello speculatore di ogni mercato selezionato e l'indice COT uno accanto all'altro, così puoi leggere il posizionamento cross-market a colpo d'occhio.

## How the data flows

Il database è la cache. Un worker di ingestion settimanale estrae i sei dataset CFTC per i mercati tracciati, fa upsert del catalogo di mercati e aggiunge ogni nuovo report **idempotentemente** (rieseguire non duplica mai uno snapshot). Inoltre, i dati vengono **caricati su richiesta**: la prima volta che viene richiesto un mercato viene scaricato dalla fonte CFTC e memorizzato, e ogni richiesta successiva viene servita direttamente dal database. La cache **si aggiorna al rilascio di nuovi report settimanali** — una volta che il report memorizzato più recente è più di una settimana vecchio, la richiesta successiva estrae e aggiunge in modo trasparente i dati più recenti (limitato in modo che la fonte non venga mai martellata). Il carico iniziale riempie diversi anni di cronologia; un'interruzione della fonte si degrada a servire i migliori dati in cache. Tutto viene eseguito out of the box senza chiave; un token app Socrata opzionale solo aumenta il limite di velocità.

## Configuration

Tutte le chiavi vivono sotto `App:Cot` (vedi [feature toggles](./feature-toggles.md) e
[white-label owner settings](./white-label-owner-settings.md)):

| Key | Default | Purpose |
|-----|---------|---------|
| `IngestionEnabled` | `true` | Se il worker di ingestion settimanale viene eseguito. |
| `PollInterval` | `6h` | Quanto spesso il worker esegue il polling dei dataset CFTC. |
| `BackfillYears` | `5` | Anni di cronologia estratti alla prima esecuzione. |
| `ReconcileLookbackWeeks` | `4` | Settimane recenti risincronizzate ogni ciclo per catturare revisioni. |
| `SocrataAppToken` | — | Token opzionale che aumenta il limite di velocità anonimo. |
| `CotIndexLookbackWeeks` | `156` | Report settimanali usati come intervallo dell'indice COT (~3 anni). |

## Gating

La visibilità è un gate a due livelli, identico al calendario economico: il gate hard di white-label
`App:Branding:EnableCot` (a livello di build) **e** l'interruttore di feature runtime `App:Features:Cot`. Con uno qualsiasi spento il link di navigazione, la pagina, l'API REST e gli strumenti MCP scompaiono tutti (l'API restituisce `404`). Perché la fonte di dati è priva di chiavi, non c'è alcun gate di chiave di fonte di dati — abilitato significa visibile.

## For developers

- Domain: `Core.Cot` — `CotMarket` e `CotReport` aggregati, l'oggetto di valore `CotPositions`, il
  servizio di dominio `CotIndexCalculator`, e i port `ICotReports` / `ICotSource`.
- Infrastructure: `Infrastructure.Cot` — il parser anti-corruzione `CftcSocrataSource`, il rate gate,
  il servizio di scrittura append-only, il lato di lettura e il worker di ingestion settimanale (schema EF `cot`).
- cBot & AI access: l'[API cBot COT](./cot-cbot-api.md) (REST, JWT `market:read`) e gli strumenti MCP
  `CotMarkets`, `CotLatest`, `CotHistory`, `CotHealth`.
