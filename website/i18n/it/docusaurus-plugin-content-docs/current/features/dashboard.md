---
title: Dashboard
description: La dashboard di cMind — un centro di controllo live e mobile-first per le tue esecuzioni di cBot, backtest, risorse e cluster di nodi.
---

# Dashboard 📊

La prima cosa che vedi quando accedi, e onestamente la pagina che lascerai aperta tutto il giorno. La
pagina di destinazione (`/`, `Components/Pages/Index.razor`) è un **centro di controllo live e mobile-first** per l'attività
dell'utente che ha effettuato l'accesso su esecuzioni cBot, backtest, risorse e (per gli admin) il cluster di nodi. Si aggiorna da solo, ha un bell'aspetto su un telefono, e non ti obbliga mai a premere F5.

## Cosa mostra

Da cima a fondo, in ordine di priorità per un telefono (ogni blocco è un elemento stack a larghezza intera su mobile, una griglia responsive su tablet/desktop):

1. **Intestazione** — titolo, un indicatore live (un puntino che pulsa veramente; statico sotto `prefers-reduced-motion`), il
   tempo dell'ultimo aggiornamento, e un **toggle di periodo** (`1H · 24H · 7D · 30D`) che guida i KPI e il grafico.
2. **KPI Hero** — quattro schede leggibili a colpo d'occhio, ognuna con un numero grande + un SVG sparkline inline, e (dove
   significativo) un **delta rispetto al periodo precedente**:
   - **Attivo ora** — esecuzioni + backtest attualmente in avvio/esecuzione.
   - **Tasso di successo** — completati ÷ (completati + falliti) nel periodo; delta in punti percentuali.
   - **Completati** — esecuzioni/backtest finiti in questo periodo; delta vs periodo precedente.
   - **Falliti** — fallimenti in questo periodo; delta (meno è meglio, quindi un calo mostra verde).
3. **Grafico di attività** — una timeline di area ApexCharts di avviati / completati / falliti per bucket di tempo.
4. **Anello di stato dell'istanza** — un donut di esecuzioni in corso / backtest / in sospeso / completati / falliti, totale al
   centro.
5. **Backtest** — uno snapshot di tre tile (in esecuzione / completati / falliti), click-through a `/backtest`.
6. **Copy trading** — i tuoi profili di copy-trading con un puntino di stato live, conteggio destinazioni, e un badge **Live**
   su profili in esecuzione; click-through a `/copy-trading`.
7. **Agenti IA** — i tuoi agenti di trading guidati da persona con stato di esecuzione (archetipo · stato) e ora dell'ultima azione; click-through a `/agent-studio`.
8. **Feed di attività live** — i 20 eventi più recenti (più recenti prima) con un puntino colorato di stato e un
   timestamp relativo.
9. **Salute del cluster** (solo admin) — nodi attivi rispetto al totale e un gauge di capacità in uso.
10. **Tile di risorse** — cBots, account di trading, cTrader ID, chiavi MCP (click-through alle loro pagine).

## Personalizza la tua dashboard

Ogni blocco sopra è un **widget che controlli**. Premi **Personalizza** (in alto a destra dell'intestazione) per aprire una
finestra di dialogo dove puoi **mostrare/nascondere** qualsiasi widget e **riordinarli** con frecce su/giù. **Reimposta impostazione predefinita**
ripristina l'ordine del catalogo. La tua scelta è **persistita server-side per utente**, quindi ti segue tra
browser e dispositivi — non solo questa scheda.

- I widget con feature-gate e solo admin (Copy trading, Agenti IA, Salute del cluster) appaiono nella
  finestra di dialogo solo quando il tuo deployment/ruolo può usarli.
- Il catalogo del widget è un'unica fonte di verità in `Core/Dashboard/DashboardWidgets.cs`; la presentazione
  (etichetta + icona + disponibilità) vive in `Components/Dashboard/DashboardWidgetMeta.cs`.

## Come rimane live

La pagina fa il polling di `GET /api/dashboard/overview?period=<1h|24h|7d|30d>` ogni 10 secondi e ri-renderizza i
widget in posizione — nessun ricaricamento manuale. Un fallimento di fetch transitorio viene inghiottito e ritentato al tick successivo;
il loop si ferma pulitamente al dispose. Il primo caricamento mostra uno scheletro; un fallimento persistente mostra una scheda di errore con **Riprova**; un utente senza dati vede KPI azzerati e copy di stato vuoto.

## Backend

- `Endpoints/DashboardEndpoints.cs` mappa `/overview` (e mantiene lo scalare `/stats` più vecchio). È
  per-utente e admin-gated tramite `ICurrentUser`; l'orologio viene da `TimeProvider`. Mappa anche
  `GET/PUT /api/dashboard/layout` — il layout del widget dell'utente, caricato all'avvio della pagina e salvato dalla
  finestra di dialogo Personalizza.
- **Persistenza del layout** è l'aggregato `UserDashboard` (`Core/Dashboard/UserDashboard.cs`): una board
  per utente (unica su `UserId`), possedere una lista ordinata di impostazioni widget (visibile + ordine) archiviata come una
  colonna `jsonb`. La lista ordinata è mutata solo tramite `Apply` / `Reset`, che convalidano ogni
  chiave rispetto al catalogo `DashboardWidgets` e mantengono la raccolta completa e de-duplicata. Le chiavi sconosciute
  sono rifiutate con un'eccezione `DomainException` → `400`.
- `Endpoints/DashboardQuery.cs` costruisce il modello di lettura composito `DashboardOverview`: uno snapshot di stato all-time
  (conteggi raggruppati), un set con finestra di istanze materializzate una volta, e conteggi di risorse/nodi.
  Lo stato dell'istanza e i timestamp terminali vivono su sottotipi TPH (non colonne), quindi le righe vengono lette in memoria
  tramite gli helper condivisi `InstanceEndpoints.GetStartedAt/GetStoppedAt`. Ora dell'evento =
  `stopped ?? started ?? created`.
- `Endpoints/DashboardModels.cs` contiene i DTO, il piano periodo→(finestra, conteggio-bucket), e
  `DashboardMath` — bucketing puro e deterministico + math KPI/delta (nessun I/O, `now` viene passato).

I delta KPI confrontano la finestra corrente rispetto a quella immediatamente precedente (la query recupera una doppia
finestra per questo). Non c'è un **feed P&L di account live** — la piattaforma ha solo equity per backtest e
tracciamento prop-firm — quindi la dashboard è deliberatamente *operazionale* (attività, throughput, tasso di successo),
non un ticker di bilancio di intermediario.

## Design e token

Tutti i colori provengono dai token di design (`var(--app-success|-warning|-error|-info|-primary|-text*)`), quindi una
tavolozza white-label scorre liberamente — incluso il grafico, i cui colori della serie vengono letti dai
token risolti a runtime tramite `window.appReadTokens` (SVG non può consumare direttamente le variabili CSS). Nessun
esadecimale hard-coded da nessuna parte nella dashboard. Vedi [../ui-guidelines.md](../ui-guidelines.md).

## Il link "Powered by cMind"

La dashboard mostra un piccolo e raffinato link **"Powered by cMind"** che punta a questo sito di documentazione. È
**mostrato per impostazione predefinita** — siamo orgogliosi del progetto e aiuta altri trader a trovarlo — ma
è interamente a tua scelta. I rivenditori che eseguono un'istanza completamente white-labeled capovolgono
`App:Branding:ShowSiteLink` a `false` e scompare. Vedi
[White-label branding](./white-label.md#powered-by-link).

## Test

- **Stile unitario** (`tests/IntegrationTests/DashboardMathTests.cs`) — bucketing, tasso di successo,
  delta del periodo precedente, parsing del periodo, vuoto/confine (evento a `now`, guardia divide-by-zero).
- **Unità** (`tests/UnitTests/Dashboard/UserDashboardTests.cs`) — l'aggregato `UserDashboard`: seme predefinito,
  applica ordine/visibilità, append-omesso, duplicate-collapse, rifiuto chiave sconosciuta, reset.
- **Integrazione** (`tests/IntegrationTests/DashboardQueryTests.cs`, `DashboardLayoutTests.cs`) — il modello di lettura
  su Postgres reale (stato/KPI/attività/risorse, salute del nodo admin, percorso utente vuoto), il
  nuovo backtest/copy-profiles/agenti sezioni, e un layout **round-trip** (salva layout personalizzato → ricarica →
  ordine + visibilità persistiti).
- **E2E** (`tests/E2ETests/DashboardTests.cs`, `DashboardCustomizeTests.cs`) — desktop + mobile: schede KPI,
  grafico, anello e feed renderizzano; il toggle di periodo commuta il periodo attivo e ricarica; un KPI
  si approfondisce verso `/run`; **nascondere un widget persiste su ricaricamento**, **Reset** lo riporta, e
  la finestra di dialogo Personalizza funziona su un telefono senza overflow orizzontale. `/` è anche in `PageSmokeTests`,
  `MobileLayoutTests` (shell + no-overflow) e `MobileJourneyTests`.
