---
description: "Crea, esegui e fai il backtest dei cBot cTrader (C# e Python, entrambi .NET) dall'IDE Monaco integrato nel browser, esegui sull'immagine ufficiale ghcr.io/spotware/ctrader-console."
---

# Crea & fai il backtest dei cBot

Crea, esegui e fai il backtest dei cBot cTrader (C# **e** Python, entrambi .NET) dall'IDE Monaco integrato nel browser, esegui sull'immagine ufficiale `ghcr.io/spotware/ctrader-console`.

## Crea

- La pagina **Builder** ospita l'editor Monaco; `CBotBuilder` compila il progetto con
  `dotnet build` **in un container monouso** (`AppOptions.BuildImage`, directory di lavoro bind-mount
  in `/work`), così i target MSBuild dell'utente non autorizzato non raggiungono l'host. La cache di restore di NuGet è condivisa
  tra le build tramite volume condiviso. L'host web ha bisogno dell'accesso al socket di Docker.
- I template iniziali C# e Python si trovano in `src/Nodes/Builder/Templates/`.

## Esegui & fai il backtest

- **Instances** = gerarchia di stato TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). La transizione sostituisce l'entità (cambio di id),
  l'id del container è mantenuto.
- `NodeScheduler` sceglie il nodo idoneo meno carico; `ContainerDispatcherFactory` instrada a
  un agente HTTP del nodo remoto o al dispatcher Docker locale.
- I poller di completamento riconciliano i container usciti (i container di backtest si autochiudono via
  `--exit-on-stop`); report presente → completato (archivia `ReportJson`), assente → fallito.
- I log del container in tempo reale vengono trasmessi al browser tramite SignalR; le curve di equità del backtest vengono analizzate dal
  report e rappresentate in un grafico.

## I dati del mercato del backtest sono memorizzati nella cache per account

cTrader Console scarica i dati cronologici di tick/bar nella sua directory `--data-dir`. Tale directory è una
**cache stabile e persistente identificata dall'account di trading** (dal suo numero di account) — bind-mount dal disco
del nodo nel suo percorso di container (`/mnt/data`), un **mount separato e non annidato** dalla
directory di lavoro per istanza. Così ogni backtest sullo stesso account **riutilizza** i dati già scaricati
invece di riscaricarlo ad ogni esecuzione. (Precedentemente la
directory dei dati si trovava sotto la directory di lavoro per istanza, il cui id cambia ad ogni esecuzione, il che forzava un nuovo
scaricamento ad ogni backtest.) La directory di lavoro per istanza effimera continua a contenere l'algoritmo, i parametri, la password
e il report; la cache dei dati condivisa viene conteggiata nell'utilizzo dei dati di backtest di un nodo e cancellata dall'azione
di pulizia del nodo.

## Impostazioni del backtest

La finestra di dialogo **Backtest** espone le impostazioni di backtest di cTrader Console modificabili dall'utente, così non devi mai
toccare la riga di comando:

- **Symbol / Timeframe** — il timeframe è un **dropdown di ogni periodo cTrader** (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1`, e i periodi Renko/Range/Heikin), nel
  case canonico della console, così scegli sempre un `--period` valido.
- **From / To** — la finestra di backtest (`--start` / `--end`).
- **Data mode** — una delle tre modalità cTrader (`--data-mode`): **Tick data** (`tick`, accurato),
  **m1 bars** (`m1`, veloce), o **Open prices only** (`open`, più veloce).
- **Starting balance** — impostazione predefinita `10000` (`--balance`). Un **saldo di 0 non inserisce alcuno trade e fa sì che
  cTrader emetta un report vuoto che quindi causa un crash** ("Message expected"), quindi viene sempre inviato un saldo diverso da zero.
- **Commission** — `--commission`.
- **Spread** — `--spread`, un **campo numerico in pip che non può scendere sotto 0**. È **nascosto in modalità Tick
  data**, dove cTrader deriva lo spread dai dati tick stessi (non viene inviato alcun `--spread`).

La directory dei dati (`--data-file` / `--data-dir`) è gestita dall'app stessa (una cache per account, vedi
sopra), non esposta nella finestra di dialogo.

:::note cTrader si arresta in crash su un backtest vuoto
Se un backtest produce **nessun risultato** — nessun trade, o nessun dato di mercato per le date/il simbolo scelti —
lo scrittore di report di cTrader Console emette `Message expected` e si chiude senza un report. L'app non può
risolvere quel bug a monte, ma lo rileva e contrassegna l'istanza come **Failed** con un motivo risolutivo
("no backtest results for the selected range…") invece di una traccia dello stack grezza. Scegli un intervallo di date più ampio
che dispone di dati di mercato disponibili e riprova.
:::

## Pagina dei dettagli dell'istanza

L'apertura di un'istanza (`/instance/{id}`) ne mostra lo stato in tempo reale, i log e — per un backtest — la curva di equità.
Il **titolo della scheda del browser** riflette l'istanza specifica (**nome del cBot · tipo · simbolo**, ad es.
`TrendBot · Backtest · EURUSD`) così una scheda di run in tempo reale e una scheda di backtest sono distinguibili a prima vista.
Un run e un backtest dello stesso cBot vengono tracciati come **lineage** distinti (un id di lineage stabile mantenuto
tra le transizioni di stato), così la pagina segue esattamente un'istanza e non mescola mai i dati di un run con quelli di un
backtest.

## Controlli del ciclo di vita dell'istanza

Ogni riga dell'istanza (e la sua pagina di dettagli) ha controlli corretti per stato. Un'istanza **attiva** mostra
**Stop**; una **terminale** (Stopped / Completed / Failed) mostra **Start (▶)** per riavviarla con
lo stesso cBot, account, simbolo, timeframe, set di parametri e immagine (un run si riavvia come run, un
backtest come backtest). Facendo clic su Stop viene visualizzato un avviso "Stopping…" e disabilita l'icona fino al suo
risolversi, e un run appena creato appare immediatamente nell'elenco — senza ricaricamento della pagina.

I log della console vengono **persistiti quando un'istanza termina** — per un run (su Stop) e per un
**backtest** (al completamento) allo stesso modo — così i log dell'ultimo run rimangono visibili sulla pagina di dettagli e,
tramite la barra degli strumenti del log, **copiati negli appunti** (icona Copia log) o **scaricati** (icona Scarica log)
anche dopo che il container è scomparso. Entrambi agiscono sul log completo della console dell'istanza, non solo sulla
coda visualizzata sullo schermo.

Un `.algo` **caricato** non è mai stato creato qui, quindi la sua colonna **Last Build** sulla pagina dei cBot è lasciata
vuota (mostra un'ora di build solo per i cBot che crei nel browser).

## Modifica e riesegui un'istanza interrotta

Un'istanza **interrotta** (run o backtest) ha un controllo **Edit** — un'icona sulla sua riga nell'elenco **e**
accanto a Start/Stop sulla sua pagina di dettagli — che apre una finestra di dialogo **precompilata** con la sua configurazione attuale.
Puoi cambiare l'**account di trading, il simbolo, il timeframe, il set di parametri e il tag dell'immagine** (e, per un
backtest, la **finestra e tutte le impostazioni di backtest** sopra), quindi **Save & start** la riavvia con le
nuove impostazioni (sostituendo l'istanza interrotta). Il controllo è **disabilitato mentre l'istanza è attiva** —
solo un'istanza interrotta può essere modificata.

## Esegui dall'editor di codice

Facendo clic su **Run** nell'editor di codice si apre una finestra di dialogo invece di avviare un run cieco e hardcoded:

- **Trading account** (obbligatorio) — l'account cTrader a cui il cBot si connette.
- **Parameter set** (opzionale) — scegli un set esistente, o lascialo vuoto per eseguire con i
  **valori dei parametri predefiniti** del cBot. Un pulsante **+** accanto al selettore crea un nuovo set di parametri
  in linea (vedi sotto) e lo seleziona.
- **Symbol / Timeframe** sono impostati per impostazione predefinita su `EURUSD` / `h1` e possono essere modificati; **Cancel** o **Run**.

Su **Run** l'editor salva e compila il codice sorgente attuale, avvia l'istanza sull'account scelto
con i parametri scelti, quindi accoda i log del container in tempo reale. (Il flusso del log inoltra il cookie di autenticazione dell'utente connesso all'hub SignalR `/hubs/logs`, così si connette invece di fallire con
`Invalid negotiation response received`.)

## Set di parametri

Un **parameter set** è un set denominato e riutilizzabile di override dei parametri del cBot archiviato come oggetto JSON flat
che mappa ogni nome di parametro a un valore scalare, ad es. `{"Period": 14, "Label": "trend"}`. Al
momento dell'esecuzione/backtest viene convertito nel file `params.cbotset` di cTrader
(`{ "Parameters": { … } }`). Puoi creare/modificare un set come JSON grezzo dalla finestra di dialogo **Parameter
sets** del cBot o in linea dalla finestra di dialogo Run.

Ogni set di parametri **appartiene a un cBot**: la finestra di dialogo New Parameter Set elenca tutti i tuoi cBot e devi
**sceglierne uno** — la creazione è bloccata fino a quando non è selezionato un cBot. Il **name** di un set è **univoco per cBot**:
la creazione o la ridenominazione di un set con un nome che un altro set dello stesso cBot già utilizza viene rifiutata (un errore chiaro
nella finestra di dialogo, `409 Conflict` all'API). Lo stesso nome può essere riutilizzato su un **cBot diverso**.

Il JSON viene **convalidato** al salvataggio: deve essere un singolo oggetto flat i cui valori sono tutti scalari
(stringa / numero / bool). Una radice non oggetto, un array, un oggetto annidato, un valore `null`, o JSON malformato
viene rifiutato (un errore chiaro nella finestra di dialogo, `400 Bad Request` all'API). Un oggetto vuoto `{}`
è consentito e significa "nessun override".

## Note sulla CLI di cTrader Console

I backtest hanno bisogno di `--data-mode` (impostazione predefinita `m1`), date come `dd/MM/yyyy HH:mm`, e
argomento posizionale JSON di `params.cbotset`; `run` rifiuta `--data-dir` (solo backtest). Vedi
`ContainerCommandHelpers`.

## Nodi & scala

La capacità di esecuzione scala aggiungendo agenti nodo (self-register + heartbeat). Vedi
[node discovery](../operations/node-discovery.md) e [scaling](../deployment/scaling.md).
## Un account di trading è obbligatorio

Per eseguire o fare il backtest di un cBot è necessario un account di trading cTrader a cui connettersi. Fino a quando non ne aggiungi uno in
**Trading accounts**, i pulsanti **Run New cBot** / **Backtest New cBot** sono disabilitati (con un
tooltip) e la pagina mostra un prompt che rimanda alla configurazione dell'account — non otterrai più un errore grezzo
`stream connect failed` da un bot senza account.
