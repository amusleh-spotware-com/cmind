---
description: "Compila, esegui, effettua backtest di cBot cTrader (C# e Python, entrambi .NET) dall'IDE Monaco in-browser, esegui su immagine ufficiale ghcr.io/spotware/ctrader-console."
---

# Compila e backtest dei cBot

Compila, esegui, effettua backtest di cBot cTrader (C# **e** Python, entrambi .NET) dall'IDE Monaco in-browser, esegui su immagine ufficiale `ghcr.io/spotware/ctrader-console`.

## Compilazione

- La pagina **Builder** ospita l'editor Monaco; `CBotBuilder` compila il progetto con `dotnet build` **in un container temporaneo** (`AppOptions.BuildImage`, directory di lavoro bind-mount a `/work`), quindi gli obiettivi MSBuild dell'utente non autorizzato non raggiungono l'host. Il ripristino NuGet è memorizzato nella cache tra le compilazioni tramite un volume condiviso. L'host web ha bisogno dell'accesso al socket Docker.
- I modelli iniziali C# + Python vivono in `src/Nodes/Builder/Templates/`.

## Esecuzione e backtest

- **Istanze** = gerarchia di stato TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). La transizione sostituisce l'entità (modifica id), l'id container è portato avanti.
- `NodeScheduler` seleziona il nodo idoneo meno carico; `ContainerDispatcherFactory` instrada a un agente HTTP del nodo remoto o al dispatcher Docker locale.
- I poller di completamento riconciliano i container chiusi (i container di backtest si chiudono autonomamente tramite `--exit-on-stop`); report presente → completato (memorizza `ReportJson`), mancante → fallito.
- I log live del container si trasmettono al browser su SignalR; le curve di equità del backtest sono analizzate dal report e rappresentate in grafici.

## I dati del mercato del backtest sono memorizzati nella cache per account

La console cTrader scarica i dati di tick/bar storici nel suo `--data-dir`. Quella directory è una **cache stabile e persistente codificata nell'account di trading** (il suo numero di account) — bind-mount dal disco del nodo nel suo percorso container (`/mnt/data`), un **mount separato e non annidato** dalla directory di lavoro per istanza. Quindi ogni backtest sullo stesso account **riutilizza** i dati già scaricati invece di scaricarli nuovamente ad ogni esecuzione. (In precedenza la directory dei dati si trovava sotto la directory di lavoro per istanza, il cui id cambia ad ogni esecuzione, il che ha forzato un download nuovo ad ogni backtest.) La directory di lavoro per istanza effimera contiene ancora l'algoritmo, i parametri, la password e il report; la cache dati condivisa è conteggiata nell'uso dei dati di backtest di un nodo e cancellata dall'azione di pulizia del nodo.

## Impostazioni di backtest

La finestra di dialogo **Backtest** espone le impostazioni di backtest della console cTrader regolabili dall'utente, quindi non devi mai toccare una riga di comando:

- **Simbolo / Timeframe** — il timeframe è un **dropdown di ogni periodo cTrader** (`t1`…`t1000`, `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1`, e i periodi Renko/Range/Heikin), nella maiuscola canonica della console, quindi scegli sempre un `--period` valido.
- **Da / A** — la finestra di backtest (`--start` / `--end`).
- **Modalità dati** — una delle tre modalità cTrader (`--data-mode`): **Tick data** (`tick`, accurato), **barre m1** (`m1`, veloce), o **Solo prezzi di apertura** (`open`, più veloce).
- **Saldo iniziale** — per impostazione predefinita `10000` (`--balance`). Un **saldo di 0 non effettua alcun trade e fa emettere a cTrader un report vuoto su cui poi si arresta in modo anomalo** ("Message expected"), quindi viene sempre inviato un saldo non zero.
- **Commissione** — `--commission`.
- **Spread** — `--spread`, un **campo numerico in pip che non può scendere al di sotto di 0**. È **nascosto in modalità Tick data**, dove cTrader deriva lo spread dai dati tick stessi (non viene inviato alcun `--spread`).

La directory dei dati (`--data-file` / `--data-dir`) è gestita dall'app stessa (una cache per account, vedi sopra), non esposta nella finestra di dialogo.

:::note cTrader si arresta in modo anomalo su un backtest vuoto
Se un backtest produce **nessun risultato** — nessun trade, o nessun dato di mercato per le date/simbolo scelte — lo scrittore di report della console cTrader genera `Message expected` e esce senza un report. L'app non può risolvere quel bug upstream, ma lo rileva e contrassegna l'istanza **Failed** con un motivo azionabile ("nessun risultato di backtest per l'intervallo selezionato…") invece di una traccia dello stack non elaborata. Scegli un intervallo di date più ampio che abbia dati di mercato disponibili e riprova.
:::

## Pagina dei dettagli dell'istanza

Aprire un'istanza (`/instance/{id}`) mostra il suo stato live, i log e — per un backtest — la curva di equità. Il **titolo della scheda del browser** riflette l'istanza specifica (**nome cBot · tipo · simbolo**, ad es. `TrendBot · Backtest · EURUSD`) quindi una scheda di esecuzione live e una scheda di backtest sono distinguibili a prima vista. Un'esecuzione e un backtest dello stesso cBot sono tracciati come **lineage** distinti (un id di lineage stabile portato avanti attraverso le transizioni di stato), quindi la pagina segue esattamente un'istanza e non mescola mai i dati di un'esecuzione con quelli di un backtest.

## Controlli del ciclo di vita dell'istanza

Ogni riga di istanza (e la sua pagina di dettaglio) ha controlli corretti per lo stato. Un'istanza **attiva** mostra **Stop**; una **terminale** (Stopped / Completed / Failed) mostra **Start (▶)** per rilanciarla con lo stesso cBot, account, simbolo, timeframe, ParamSet e immagine (un'esecuzione viene riavviata come esecuzione, un backtest come backtest). Fare clic su Stop mostra un avviso "Stopping…" e disabilita l'icona finché non si risolve, e un'esecuzione appena creata appare immediatamente nell'elenco — nessun ricaricamento della pagina.

I log della console sono **persistiti quando un'istanza termina** — per un'esecuzione (su Stop) e per un **backtest** (al completamento) allo stesso modo — quindi i log dell'ultima esecuzione rimangono visualizzabili nella pagina dei dettagli e, tramite la barra degli strumenti dei log, **copiati negli appunti** (icona Copia log) o **scaricati** (icona Scarica log) anche dopo che il container è scomparso. Entrambi agiscono sul log della console completo dell'istanza, non solo sulla coda sullo schermo.

Un **backtest completato** persiste anche il suo **report cTrader** in entrambi i formati — il **JSON** non elaborato (lo stesso che le curve di equità e l'analisi AI leggono) e il report completo **HTML**. Entrambi sono scaricabili dalla riga di backtest **e** dalla pagina dei dettagli tramite icone dedicate. Solo i **report dell'ultima esecuzione** vengono conservati, e le icone sono **disabilitate** per qualsiasi backtest che non è stato avviato, è in esecuzione o non riuscito (e non vengono mai mostrate per un'istanza di esecuzione) — solo un backtest completato ha un report da scaricare.

Un `.algo` **caricato** non è mai stato compilato qui, quindi la sua colonna **Last Build** nella pagina dei cBot è lasciata vuota (mostra un tempo di compilazione solo per i cBot che compili nel browser).

## Modifica e riesecuzione di un'istanza interrotta

Un'istanza **interrotta** (esecuzione o backtest) ha un controllo **Edit** — un'icona nella sua riga nell'elenco **e** accanto a Start/Stop nella sua pagina di dettaglio — che apre una finestra di dialogo **precompilata** con la sua configurazione corrente. Puoi cambiare l'**account di trading, simbolo, timeframe, ParamSet e tag immagine** (e, per un backtest, la **finestra e tutte le impostazioni di backtest** sopra), quindi **Salva e avvia** lo rilancia con le nuove impostazioni (sostituendo l'istanza interrotta). Il controllo è **disabilitato mentre l'istanza è attiva** — solo un'istanza interrotta può essere modificata.

## Esecuzione dall'editor di codice

Facendo clic su **Run** nell'editor di codice si apre una finestra di dialogo invece di attivare un'esecuzione cieca e codificata:

- **Account di trading** (obbligatorio) — l'account cTrader a cui si connette il cBot.
- **ParamSet** (facoltativo) — scegli un set esistente, o lascialo vuoto per eseguire con i **valori dei parametri predefiniti** del cBot. Un pulsante **+** accanto al selettore crea un nuovo ParamSet inline (vedi sotto) e lo seleziona.
- **Simbolo / Timeframe** per impostazione predefinita a `EURUSD` / `h1` e possono essere modificati; **Annulla** o **Run**.

Su **Run** l'editor salva + compila il sorgente corrente, avvia l'istanza sull'account scelto con i parametri scelti, quindi traccia i log live del container. (Il flusso di log inoltra il cookie di autenticazione dell'utente firmato all'hub SignalR `/hubs/logs`, quindi si connette invece di fallire con `Invalid negotiation response received`.)

## Parameter set

Un **parameter set** è un set denominato e riutilizzabile di override dei parametri del cBot memorizzati come oggetto JSON flat che mappa ogni nome di parametro a un valore scalare, ad es. `{"Period": 14, "Label": "trend"}`. Al momento dell'esecuzione/backtest, viene trasformato nel file `params.cbotset` di cTrader (`{ "Parameters": { … } }`). Puoi creare/modificare un set come JSON non elaborato dalla finestra di dialogo **Parameter sets** del cBot o inline dalla finestra di dialogo Run.

Ogni parameter set **appartiene a un cBot**: la finestra di dialogo Nuovo Parameter Set elenca tutti i tuoi cBot e **devi sceglierne uno** — la creazione è bloccata fino a quando non viene selezionato un cBot. Il **nome di un set è univoco per cBot**: la creazione o la ridenominazione di un set con un nome che un altro set dello stesso cBot già usa viene rifiutata (un errore chiaro nella finestra di dialogo, `409 Conflict` all'API). Lo stesso nome può essere riutilizzato su un **cBot diverso**.

Il JSON è **convalidato** al salvataggio: deve essere un singolo oggetto flat i cui valori sono tutti scalari (stringa / numero / booleano). Una radice non oggetto, un array, un oggetto annidato, un valore `null`, o JSON non valido viene rifiutato (un errore chiaro nella finestra di dialogo, `400 Bad Request` all'API). Un oggetto vuoto `{}` è consentito e significa "nessun override".

## Note sulla CLI della console cTrader

I backtest hanno bisogno di `--data-mode` (per impostazione predefinita `m1`), date come `dd/MM/yyyy HH:mm`, e argomento posizionale JSON `params.cbotset`; `run` rifiuta `--data-dir` (solo backtest). Vedi `ContainerCommandHelpers`.

## Nodi e scala

La capacità di esecuzione scala aggiungendo agenti di nodo (auto-registrazione + heartbeat). Vedi [scoperta dei nodi](../operations/node-discovery.md) e [ridimensionamento](../deployment/scaling.md).

## È necessario un account di trading

L'esecuzione o il backtest di un cBot richiede un account di trading cTrader a cui connettersi. Fino a quando non ne aggiungi uno in **Trading accounts**, i pulsanti **Run New cBot** / **Backtest New cBot** sono disabilitati (con un tooltip) e la pagina mostra un prompt che si collega alla configurazione dell'account — non colpirai più un errore `stream connect failed` non elaborato da un bot senza account.
