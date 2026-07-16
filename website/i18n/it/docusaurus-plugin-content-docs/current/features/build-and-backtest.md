---
description: "Crea, esegui, esegui backtest di cBot cTrader (C# e Python, entrambi .NET) dall'IDE Monaco integrato nel browser, eseguito su immagine ghcr.io/spotware/ctrader-console ufficiale."
---

# Build & backtest cBots

Crea, esegui, esegui backtest di cBot cTrader (C# **e** Python, entrambi .NET) dall'IDE Monaco
integrato nel browser, eseguito su immagine ufficiale `ghcr.io/spotware/ctrader-console`.

## Build

- **Builder** page ospita l'editor Monaco; `CBotBuilder` compila il progetto con
  `dotnet build` **in un container temporaneo** (`AppOptions.BuildImage`, work dir bind-mount
  in `/work`), così i target MSBuild non attendibili non raggiungono l'host. Il restore di NuGet è memorizzato nella cache
  tra i build tramite volume condiviso. Il web host ha bisogno dell'accesso al socket Docker.
- I template di avvio C# + Python si trovano in `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = gerarchia di stato TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). La transizione sostituisce l'entità (cambio di id),
  l'id del container è conservato.
- `NodeScheduler` sceglie il nodo idoneo meno carico; `ContainerDispatcherFactory` indirizza
  all'agente HTTP del nodo remoto o al dispatcher Docker locale.
- I poller di completamento riconciliano i container chiusi (i container di backtest si chiudono autonomamente tramite
  `--exit-on-stop`); report presente → completato (memorizza `ReportJson`), assente → non riuscito.
- I log del container live si trasmettono al browser su SignalR; le curve di equità del backtest sono analizzate dal
  report e tracciate su grafici.

## Backtest market data is cached per account

Il cTrader Console scarica i dati storici tick/bar nella sua `--data-dir`. Quella directory è una
**cache stabile e persistente basata sull'account di trading** (il suo numero di account) — bind-mount dal
disco del nodo al suo percorso container (`/mnt/data`), un **mount separato e non annidato** dalla
directory di lavoro per istanza. Quindi ogni backtest sullo stesso account **riusa** i dati già scaricati
invece di scaricarli di nuovo ad ogni esecuzione. (In precedenza
la directory dei dati si trovava nella directory di lavoro per istanza, il cui id cambia ad ogni esecuzione, che forzava un
download fresh ad ogni backtest.) La directory di lavoro per istanza effimera conserva ancora l'algo, i parametri, la password
e il report; la cache di dati condivisa viene conteggiata nell'utilizzo dei dati di backtest di un nodo e cancellata dall'azione
node-clean.

## Backtest settings

La finestra di dialogo **Backtest** espone ogni impostazione che la CLI di backtest di cTrader Console accetta, così non dovrai
mai toccare una linea di comando:

- **From / To** — la finestra di backtest (`--start` / `--end`).
- **Data mode** — una delle tre modalità cTrader (`--data-mode`): **Tick data** (`tick`, accurato),
  **m1 bars** (`m1`, veloce), o **Open prices only** (`open`, più veloce).
- **Starting balance** — predefinito a `10000` (`--balance`). Un **balance di 0 non effettua alcuno trade e fa sì che
  cTrader emetta un report vuoto su cui poi si arresta** ("Message expected"), quindi un balance diverso da zero è
  sempre inviato.
- **Commission** e **Spread** — `--commission` / `--spread` (spread in pip).
- **Data file** (opzionale) — un percorso sul nodo a un file di dati storici (`--data-file`); lascia vuoto per
  usare i dati scaricati/memorizzati nella cache.
- **Expose environment variables** — un toggle che passa le variabili di ambiente dell'host al cBot
  (il flag `--environment-variables`).

## Instance detail page

L'apertura di un'istanza (`/instance/{id}`) mostra il suo stato live, i log e — per un backtest — la curva
di equità. Il **titolo della scheda del browser** riflette l'istanza specifica (**nome cBot · tipo · simbolo**, ad es.
`TrendBot · Backtest · EURUSD`) così una scheda di esecuzione live e una scheda di backtest sono distinguibili a colpo d'occhio.
Un'esecuzione e un backtest dello stesso cBot sono tracciati come **lineages** distinti (un id lineage stabile conservato
attraverso le transizioni di stato), quindi la pagina segue esattamente un'istanza e non mescola mai i dati di un'esecuzione con quelli di un
backtest.

## Instance lifecycle controls

Ogni riga di istanza (e la sua pagina di dettaglio) ha controlli corretti per lo stato. Un'istanza **attiva** mostra
**Stop**; una **terminale** (Stopped / Completed / Failed) mostra **Start (▶)** per rilanciarla con
lo stesso cBot, account, simbolo, timeframe, set di parametri e immagine (un'esecuzione si riavvia come esecuzione, un
backtest come backtest). Fare clic su Stop mostra un avviso "Stopping…" e disabilita l'icona fino a quando non si
risolve, e un'esecuzione appena creata appare immediatamente nell'elenco — nessun ricaricamento della pagina.

I log della console sono **persistiti quando un'istanza termina** — per un'esecuzione (su Stop) e per un
**backtest** (al completamento) allo stesso modo — così i log dell'ultima esecuzione rimangono visualizzabili sulla pagina di dettaglio e,
tramite la barra degli strumenti del log, **copiati negli appunti** (icona Copy logs) o **scaricati** (icona Download logs)
anche dopo che il container è scomparso. Entrambi agiscono sul log completo della console dell'istanza, non solo sulla
coda on-screen.

Un `.algo` **caricato** non è mai stato costruito qui, quindi la colonna **Last Build** sulla pagina dei cBot è
lasciata vuota (mostra un'ora di build solo per i cBot che crei nel browser).

## Edit & re-run a stopped instance

Un'istanza **arrestata** (esecuzione o backtest) ha un controllo **Edit** — un'icona sulla sua riga nell'elenco **e**
accanto a Start/Stop sulla sua pagina di dettaglio — che apre una finestra di dialogo **precompilata** con la sua configurazione attuale.
Puoi cambiare l'**account di trading, il simbolo, il timeframe, il set di parametri e il tag dell'immagine** (e, per un
backtest, la **finestra e tutte le impostazioni di backtest** sopra), quindi **Save & start** la rilancia con le
nuove impostazioni (sostituendo l'istanza arrestata). Il controllo è **disabilitato mentre l'istanza è attiva** —
solo un'istanza arrestata può essere modificata.

## Run from the code editor

Fare clic su **Run** nell'editor del codice apre una finestra di dialogo anziché lanciare un'esecuzione cieca e hardcoded:

- **Trading account** (richiesto) — l'account cTrader a cui il cBot si connette.
- **Parameter set** (opzionale) — seleziona un set esistente, o lascialo vuoto per eseguire con i **valori di parametri predefiniti** del cBot.
  Un pulsante **+** accanto al selettore crea un nuovo set di parametri
  inline (vedi sotto) e lo seleziona.
- **Symbol / Timeframe** predefiniti a `EURUSD` / `h1` e possono essere modificati; **Cancel** o **Run**.

Su **Run** l'editor salva + compila il sorgente attuale, avvia l'istanza sull'account scelto
con i parametri scelti, quindi traccia i log live del container. (Il flusso di log invia il cookie di autenticazione dell'utente connesso al
hub SignalR `/hubs/logs`, così si connette anziché non riuscire con
`Invalid negotiation response received`.)

## Parameter sets

Un **parameter set** è un set denominato e riutilizzabile di override dei parametri del cBot memorizzato come
oggetto JSON piatto che mappa ogni nome di parametro a un valore scalare, ad es. `{"Period": 14, "Label": "trend"}`. Al
momento dell'esecuzione/backtest viene trasformato nel file `params.cbotset` di cTrader
(`{ "Parameters": { … } }`). Puoi creare/modificare un set come JSON grezzo dalla finestra di dialogo **Parameter
sets** del cBot o inline dalla finestra di dialogo Run.

Ogni set di parametri **appartiene a un cBot**: la finestra di dialogo New Parameter Set elenca tutti i tuoi cBot e devi
**sceglierne uno** — la creazione è bloccata fino a quando non è selezionato un cBot. Il **nome di un set è univoco per cBot**:
la creazione o la ridenominazione di un set con un nome che un altro set dello stesso cBot usa già è rifiutata (un errore chiaro
nella finestra di dialogo, `409 Conflict` all'API). Lo stesso nome può essere riutilizzato su un **cBot diverso**.

Il JSON è **convalidato** al salvataggio: deve essere un singolo oggetto piatto i cui valori sono tutti scalari
(string / number / bool). Una radice non-object, un array, un oggetto annidato, un valore `null`, o JSON
malformato è rifiutato (un errore chiaro nella finestra di dialogo, `400 Bad Request` all'API). Un oggetto vuoto `{}`
è consentito e significa "nessun override".

## cTrader Console CLI notes

I backtest richiedono `--data-mode` (predefinito `m1`), date come `dd/MM/yyyy HH:mm`, e
argomento posizionale JSON `params.cbotset`; `run` rifiuta `--data-dir` (solo backtest). Vedi
`ContainerCommandHelpers`.

## Nodes & scale

La capacità di esecuzione scala aggiungendo agenti nodo (auto-registrazione + heartbeat). Vedi
[node discovery](../operations/node-discovery.md) e [scaling](../deployment/scaling.md).

## A trading account is required

Eseguire o eseguire il backtest di un cBot richiede un account di trading cTrader a cui connettersi. Fino a quando non ne aggiungi uno in
**Trading accounts**, i pulsanti **Run New cBot** / **Backtest New cBot** sono disabilitati (con un
tooltip) e la pagina mostra un prompt che collega alla configurazione dell'account — non ricevi più un errore grezzo
`stream connect failed` da un bot senza account.
