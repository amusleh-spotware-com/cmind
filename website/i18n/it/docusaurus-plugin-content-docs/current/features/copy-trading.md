---
description: "Specchia l'account cTrader master su uno+ account slave — cross-broker, cross-cID — con controllo per-destinazione + riconciliazione money-grade."
---

# Copy trading

Specchia **master** account cTrader su uno+ account **slave** — cross-broker, cross-cID — con controllo per-destinazione + riconciliazione money-grade.

## Concetti

- **Profilo di copia** — un master (`SourceAccountId`) + uno+ **destinazioni**. Ciclo di vita: `Draft → Running → Paused → Stopped` (`Error` in caso di fallimento). Aggregate root: `CopyProfile` (possiede `CopyDestination`).
- **Destinazione** — un account slave + set completo di regole per come il master viene copiato su di esso. Tutta la config per-destinazione, così un master alimenta slave conservativi e aggressivi allo stesso tempo.
- **Copy engine host** — worker in esecuzione per il profilo (`CopyEngineHost`). Si iscrive al flusso di esecuzione del master, applica ogni evento a ogni destinazione.
- **Supervisore** — `CopyEngineSupervisor`, servizio in background su ogni nodo. Ospita profili assegnati, auto-guarisce tra cluster (vedi [scaling](../deployment/scaling.md)).

## Cosa viene specchiato

| Evento Master | Azione Slave |
|--------------|--------------|
| Posizione di mercato / market-range aperta | Apri una copia dimensionata (etichettata con l'id della posizione sorgente) |
| Ordine pending limit / stop / stop-limit | Piazza l'ordine pending corrispondente |
| Amend ordine pending | Amend l'ordine pending specchiato in posizione |
| Cancellazione ordine pending / scadenza | Cancella l'ordine pending specchiato |
| Chiusura parziale | Chiudi la stessa proporzione della posizione slave |
| Scale-in (aumento di volume) | Apri il volume aggiunto (opt-in) |
| Cambiamento stop-loss / trailing-stop | Amend la protezione della posizione slave |
| Chiusura completa | Chiudi la copia slave |

Ogni copia **etichettata con l'id della posizione/ordine sorgente**. Dopo la riconnessione, l'host ricostruisce lo stato da riconciliazione: apre copie che il master tiene ma lo slave manca, chiude "orfani" slave che il master non tiene più — **senza duplicare le operazioni**.

## Creazione di un profilo

La finestra di dialogo **Nuovo Profilo** sulla pagina Copy Trading raccoglie tutto in anticipo: nome del profilo, account sorgente (master), account destinazione (slave) (multi-select con pulsante **Seleziona tutto**; master scelto escluso dall'elenco slave), + set di opzioni completo per-destinazione di seguito. Tutti gli input **validati prima del salvataggio** — nome/sorgente/destinazione mancanti, parametro di dimensionamento non positivo, limiti di lotto negativi/incoerenti, drawdown % fuori intervallo, nessun tipo di ordine abilitato, filtro simbolo vuoto, o coppie di symbol-map malformate appaiono come elenco di errori + bloccano il salvataggio. Al momento della conferma, il profilo viene creato e ogni slave selezionato viene aggiunto con le impostazioni scelte.

Le azioni di riga rispettano il ciclo di vita: **Avvia** abilitato solo quando non in esecuzione, **Arresta** + **Pausa** solo quando in esecuzione, **Elimina** disabilitato durante l'esecuzione e chiede conferma prima di rimuovere il profilo + destinazioni.

## Opzioni per-destinazione

Imposta nella finestra di dialogo Nuovo Profilo, nel pannello per-destinazione della pagina Copy Trading, o tramite `POST /api/copy/profiles/{id}/destinations`:

- **Dimensionamento** (`MoneyManagementMode` + parametro): lotto fisso, lotto/moltiplicatore nozionale, bilancia proporzionale/equity/free-margin, rischio fisso %, leverage fisso, auto-proporzionale, **rischio-%-da-stop** (M7). Oltre a limiti di lotto min/max + forza-lotto-min. **Rischio-da-stop** dimensiona la destinazione in modo che rischi una percentuale configurata del *suo proprio* bilancio, derivato dalla **distanza dello stop-loss del master** (`il master rischia il 2% → lo slave auto-rischia il 2%`): `lotti = bilancio×% ÷ (distanzaStop × grandezzaContratto)`. Master aperto **senza** stop-loss non ha distanza su cui dimensionare → utilizza il **lotto fallback rischio-max configurato** (M7) se impostato, altrimenti saltato (`no_stop_loss`) non indovinato. Dimensionamento proporzionale-**equity**/**free-margin** fuori dall'**equity** dell'account reale (`bilancio + Σ P&L fluttuante`, derivato per cTrader Open API che non fornisce equity), non semplice bilancio — così il master seduto su profitto/perdita aperta dimensiona correttamente le copie. Il margine utilizzato non è esposto dall'API di riconciliazione, quindi il free-margin è trattato come equity (proxy di fondi disponibili onesto); altre modalità leggono il bilancio e saltano un round-trip di rivalutazione extra.
- **Filtro di direzione**: entrambi / solo long / solo short. **Inverti**: capovolgi il lato (+ scambia SL↔TP) per copia contrarian.
- **Solo-gestione** (Ignora-Nuove-Operazioni / Solo-Chiusura): specchia chiusure, chiusure parziali + cambiamenti di protezione su posizioni già copiate, ma apri **nessuna** nuova posizione/ordine pending (saltato `manage_only`). Usalo per ridimensionare la destinazione senza tagliare copie esistenti.
- **Sincronizza-Aperture-Inizio** / **Sincronizza-Chiusure-Inizio** (predefinito attivato): sulla **prima** risincronizzazione del profilo, se aprire copie per le posizioni pre-esistenti del master, + se chiudere copie che il master ha chiuso mentre il profilo era fermato. Entrambi si applicano solo all'inizio — il riconnessione a metà esecuzione sempre riconcilia completamente quindi la desincronizzazione si recupera comunque.
- **Symbol map** + **symbol filter** (whitelist / blacklist). Ogni voce di symbol-map porta un **moltiplicatore di volume per-simbolo** opzionale (override per-simbolo cMAM) dimensionamento della copia per quel simbolo in cima al dimensionamento della destinazione (1 = nessun cambiamento). L'intera mappa importa/esporta come **CSV** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; colonne `Source,Destination,VolumeMultiplier`) — ogni riga validata attraverso oggetti di valore di dominio, quindi un file malformato non può produrre una mappa non valida.
- **Finestra di orario di trading** (C18) — finestra UTC giornaliera per-destinazione (`inizio`/`fine` minuti-del-giorno, fine esclusiva; `inizio == fine` = tutto il giorno). Nuove aperture al di fuori della finestra saltate (`trading_hours`); finestra con `inizio > fine` si avvolge oltre la mezzanotte (ad esempio 22:00–06:00). Le posizioni esistenti rimangono gestite.
- **Filtro etichetta sorgente** (C18, equivalente cTrader del filtro magic-number MT) — quando impostato, copia solo operazioni master la cui etichetta corrisponde **esattamente** (ad esempio operazioni di un bot, o etichetta solo manuale); altrimenti saltate (`source_label`). Vuoto = copia tutto. Portato su `ExecutionEvent.SourceLabel` dall'etichetta di posizione/ordine master `TradeData.Label`, rispettato anche sulla risincronizzazione.
- **Protezione dell'account** (ZuluGuard / Protezione dell'Account Globale) — guarda l'**equity live** della destinazione (`bilancio + Σ P&L fluttuante`, sottoposto a polling ogni `CopyDefaults.EquityGuardInterval`) contro il pavimento `StopEquity` e/o ceiling `TakeEquity` opzionale. Su violazione, applica modalità: **SoloChiusura** (fermati nuove copie, continua a gestire le esistenti), **Congelato** (ferma aperture), **VendiTutto** (chiudi **ogni** copia sulla destinazione immediatamente). Una volta attivato, destinazione bloccata — nessuna nuova apertura finché l'host non si riavvia — + allerta `CopyAccountProtectionTriggered` sollevata. `VendiTutto` richiede `StopEquity`; `TakeEquity` deve stare sopra `StopEquity`. **Avvertenza senza garanzia:** la vendita di tutto utilizza l'esecuzione di mercato — come l'equivalente di ogni concorrente, non può garantire il prezzo di riempimento in mercato veloce/con gap.
- **Pulsante di panico Flatten-All** (C8) — `POST /api/copy/profiles/{id}/flatten` chiude immediatamente **ogni** posizione copiata su ogni destinazione + si blocca contro nuove aperture. Instradato cross-processo: l'API imposta il flag, il supervisore lo consegna all'host in esecuzione (riutilizzando il canale di rotazione token), che appiattisce in posizione; il flag è cancellato in modo che si attiva esattamente una volta (allerta `CopyFlattenAll`). L'utente quindi mette in pausa/ferma il profilo.
- **Guard regola prop-firm** (C7) — enforcement che gli utenti di copier prop-firm chiedono. Per destinazione, **limite di perdita giornaliera** (perdita dal bilancio di apertura del giorno) e/o **limite di drawdown trailing** (perdita dal bilancio di picco corrente), entrambi in valuta di deposito. Su violazione, destinazione **auto-appiattita** (ogni copia chiusa) + **bloccata** per il resto del giorno UTC (nuove aperture saltate `prop_lockout`); allerta `CopyPropRuleBreached` si accende. Il blocco si cancella quando il giorno UTC passa (nuovo baseline/picco preso). Condivide lo stesso sondaggio di equity live della protezione dell'account.
- **Jitter di esecuzione** (C11, predefinito disattivato) — ritardo casuale da `0..N` ms prima di piazzare ogni copia, per de-correlare timestamp di ordini quasi identici negli **propri** account dell'utente. **Avvertenza di conformità:** aiuto per le prop firm che *consentono* la copia — **non** strumento per eludere la firm che la vieta; stare dentro le regole della tua firm è tua responsabilità.
- **Blocco config** (C9) — congela le impostazioni della destinazione per il periodo (`POST …/destinations/{id}/lock` con minuti). Mentre bloccato, la destinazione non può essere rimossa (l'aggregate rifiuta con `CopyDestinationConfigLocked`) — guardia deliberata contro cambiamenti impulsivi durante il drawdown. Il blocco scade automaticamente al suo timestamp.
- **Pre-allerta di consistenza** (C10) — avvisa (una volta al giorno UTC) quando il **profitto giornaliero** della destinazione raggiunge la percentuale configurata del bilancio di apertura del giorno (`CopyConsistencyThresholdApproaching`), così la regola di consistenza della prop-firm è rispettata *prima* che si attivi. Dal lato del profitto, indipendente dal blocco dal lato della perdita; gira dal stesso baseline del giorno del guard della regola prop.
- **Filtro tipo di ordine** — scegli esattamente quali tipi di ordine master copiare: market, market-range, limit, stop, stop-limit (flag `CopyOrderTypes`; predefinito tutti). Selettività stile cMAM.
- **Copia SL / Copia TP** — specchia lo stop-loss / take-profit del master, o gestisci la protezione in modo indipendente.
- **Copia trailing stop**, **specchia chiusura parziale**, **specchia scale-in** — ognuno indipendentemente attivabile.
- **Copia scadenza pending** (predefinito attivato) — specchia il timestamp di scadenza Good-Till-Date dell'ordine pending master.
- **Copia slippage master** (predefinito attivato) — per ordini market-range + stop-limit, piazza l'ordine slave con il slippage-in-punti esatto del master (prezzo base preso dal spot live slave).
- **Guard**: max drawdown %, limite di perdita giornaliera, ritardo massimo copia, filtro slippage (salta copia se il prezzo slave si è mosso oltre N pip da entry master). **Il ritardo massimo copia** misurato contro il timestamp del server reale dell'evento master (`ExecutionEvent.ServerTimestamp`) tramite `TimeProvider` iniettato: segnale più vecchio del ritardo massimo configurato saltato, quindi copia stale non piazzata mai tardi (prima il ritardo era sempre zero + guard morto).
- **Normalizzazione precisione SL/TP** (M6) — prezzi di stop-loss/take-profit copiati arrotondati alla **precisione di cifre** del simbolo di destinazione prima dell'amend, così il prezzo master a precisione più fine (o disallineamento di cifra cross-broker) non urta mai il `INVALID_STOPLOSS_TAKEPROFIT` del server.
- **Circuit breaker di rifiuto / Follower Guard** (G8) — la destinazione che rifiuta `CopyDefaults.RejectionBudget` aperture di fila è **attivata**: nessuna nuova apertura per la finestra di cooldown (allerta `CopyDestinationTripped` si accende), fermando la tempesta di rifiuto dall'martellare l'account (prop-firm). Le posizioni esistenti sono ancora gestite e chiuse mentre attivate; il breaker auto-resetta dopo il cooldown + copia riuscita cancella il contatore.
- **Soffitto sanità lotto** (C14) — massimo assoluto della dimensione copia e/o limite multiplo del master. La copia calcolata che supera il limite assoluto, o supera `N×` la dimensione lotto del master stesso, è **duramente bloccata** (resa superficiale come skip `lot_sanity`, contata su `cmind.copy.skipped`) non piazzata — si difende dalla classe di sovradimensionamento catastrofico (master 0,23 lotti che si trasforma in 3 lotti su ogni ricevente tramite moltiplicatore fuggente o bug di arrotondamento). Entrambe le dimensioni predefin `0` (disattivato).

## Affidabilità e casi limite

Il motore è costruito per la realtà che qualsiasi cosa può fallire in qualsiasi momento:

- **Timeout di correlazione fill pending slave** (C13) — pending slave specchiato il cui pending master è scomparso (né riposa né appena riempito) cancellato dopo il timeout di correlazione, così la copia slave non può riempirsi in modo scorrelato in una posizione non gestita (`CopyPendingTimedOut`). La risincronizzazione pulisce anche l'ordine riempito orfano etichettato con id.
- **Chiusura/appiattimento robusto** (M8) — chiusura orfano sulla risincronizzazione, o appiattimento su violazione guardia, tollera la posizione che il broker ha già chiuso (`POSITION_NOT_FOUND`): ogni chiusura gira in modo indipendente, quindi un id non aggiornato non interrompe mai la risincronizzazione o lascia il resto dell'account non appiattito.

- **Avvia con master già nelle operazioni** — all'avvio, l'host riconcilia + apre copie per le posizioni esistenti del master.
- **Interruzioni di connessione / desincronizzazione** — alla riconnessione, l'host riconcilia: apre copie mancanti, chiude orfani, re-etichette pending. Nessun ordine duplicato.
- **Fallimento del piazzamento dell'ordine** — il fallimento su una destinazione è registrato, non blocca mai altre destinazioni.
- **Token valido singolo per cID** — cTrader invalida il vecchio token di accesso del cID nel momento in cui uno nuovo è emesso. cMind scambia il token dell'host in esecuzione **in posizione** (ri-auth sul socket live) in modo che la copia continui senza rilasciare il flusso. Vedi [ciclo di vita token](token-lifecycle.md).

## Verificabilità

Ogni azione emette un evento di log strutturato e generato dalla sorgente (`LogMessages`) con id profilo, cID di destinazione, id di ordine/posizione, + valori — ordine piazzato/saltato (con motivo), chiusura parziale, protezione applicata, trailing applicato, pending piazzato/emendato/cancellato, scadenza specchiata, slippage di market-range specchiato, token scambiato, riepilogo risincronizzazione. Questo è l'audit trail per conformità + risoluzione delle controversie.

Insieme ai log, il motore emette **metriche OpenTelemetry** sul contatore `cMind.Copy` (registrato nella pipeline OTel condivisa, esportato su OTLP / a Azure Monitor come il resto): `cmind.copy.latency` (evento master → dispatch, ms), `cmind.copy.dispatch.duration` (fan-out a tutte le destinazioni, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (taggato per destinazione), `cmind.copy.skipped` (taggato per motivo), + `cmind.copy.failed`. Questi rendono regressione di latenza/slippage misurabile, non solo visibile nella riga di log — la suite live asserisce loro contro budget.

## API

- `GET /api/copy/profiles` — elenco.
- `POST /api/copy/profiles` — crea (con id account destinazione opzionali).
- `GET /api/copy/profiles/{id}` — dettaglio completo incl. ogni opzione destinazione.
- `POST /api/copy/profiles/{id}/destinations` — aggiungi una destinazione con il set di opzioni completo.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — rimuovi.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — ciclo di vita.

## Test

- **Unità** (`tests/UnitTests/CopyTrading`) — modalità di dimensionamento, filtri di decisione, filtro tipo di ordine, copia di scadenza, slippage di market-range/stop-limit, toggle SL/TP, chiusura parziale, amend/cancel pending, avvia-con-aperture, disconnessione→desincronizzazione→risincronizzazione, scambio token in posizione, invalidazione cross-cID. Funziona su `FakeTradingSession`, simulatore in memoria fedele a cTrader.
- **Integrazione** (`tests/IntegrationTests/CopyLive`) — affinità nodo/reclamo lease, propagazione versione token su Postgres reale.
- **E2E** (`tests/E2ETests`) — round-trip opzione destinazione tramite API + UI, ciclo di vita completo.
- **Stress / DST** (`tests/StressTests`) — deterministic-simulation testing: carichi di lavoro randomizzati seed + fault injection (socket flap, rifiuto ordine, rifiuto market-range, rotazione token, morte nodo) guidare `CopyEngineHost` verso quiescenza + asserire invarianti di convergenza. Vedi [testing/stress-testing.md](../testing/stress-testing.md). Questa suite ha scoperto e risolto una vera gara di startup: `OnReconnected` cablato prima del caricamento di riferimento iniziale + risincronizzazione, quindi il socket flap durante l'avvio potrebbe eseguire una seconda risincronizzazione contemporaneamente e corrompere i dizionari di stato non simultanei dell'host — il caricamento di avvio + la prima risincronizzazione ora vengono eseguiti sotto `_stateGate`.
- **Live** — account demo cTrader reali; vedi [testing/live-copy-trading.md](../testing/live-copy-trading.md).

Vedi [dev-credentials.md](../testing/dev-credentials.md) per il file di credenziali singolo live + i tier E2E leggono.
