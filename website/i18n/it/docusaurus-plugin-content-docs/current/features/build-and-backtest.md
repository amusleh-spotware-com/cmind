---
description: "Compila, esegui, sottoponi a backtest i cBot cTrader (C# e Python, entrambi .NET) dall'IDE Monaco integrato nel browser, esegui sull'immagine ufficiale ghcr.io/spotware/ctrader-console."
---

# Compilazione e backtest dei cBot

Compila, esegui, sottoponi a backtest i cBot cTrader (C# **e** Python, entrambi .NET) dall'IDE Monaco
integrato nel browser, esegui sull'immagine ufficiale `ghcr.io/spotware/ctrader-console`.

## Compilazione

- La pagina **Builder** ospita l'editor Monaco; `CBotBuilder` compila il progetto con
  `dotnet build` **in un contenitore usa e getta** (`AppOptions.BuildImage`, directory di lavoro montata
  a `/work`), quindi le destinazioni MSBuild dell'utente non attendibile non raggiungono l'host. Il ripristino NuGet è memorizzato nella cache
  tra le compilazioni tramite volume condiviso. L'host Web ha bisogno dell'accesso al socket Docker.
- I modelli di avvio C# + Python si trovano in `src/Nodes/Builder/Templates/`.

## Esecuzione e backtest

- **Instances** = gerarchia di stato TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). La transizione sostituisce l'entità (cambio id),
  l'id del contenitore viene trasportato.
- `NodeScheduler` seleziona il nodo eleggibile meno carico; `ContainerDispatcherFactory` instrada a
  un agente HTTP del nodo remoto o al dispatcher Docker locale.
- I poller di completamento riconciliano i contenitori usciti (i contenitori di backtest escono automaticamente tramite
  `--exit-on-stop`); report presente → completato (memorizza `ReportJson`), mancante → fallito.
- I log dei contenitori in tempo reale vengono trasmessi al browser tramite SignalR; le curve di equità del backtest vengono analizzate dal
  report e tracciate.

## I dati di mercato per il backtest sono memorizzati nella cache per conto

La console cTrader scarica i dati storici di tick/bar nella sua `--data-dir`. Quella directory è una
**cache stabile e persistente associata all'account di trading** (il suo numero di conto) — montata da bind dal
disco del nodo sul suo percorso del contenitore (`/mnt/data`), un **mount separato e non annidato** dalla
directory di lavoro per istanza. Quindi ogni backtest dello stesso account **riutilizza** i dati già scaricati
invece di scaricarlo di nuovo ad ogni esecuzione. (In precedenza la
directory dei dati si trovava sotto la directory di lavoro per istanza, il cui id cambia ad ogni esecuzione, il che ha forzato un
scaricamento nuovo ad ogni backtest.) La directory di lavoro per istanza effimera contiene comunque l'algoritmo, i parametri, la password
e il report; la cache dei dati condivisa viene conteggiata nell'utilizzo dei dati di backtest di un nodo e cancellata dall'azione
node-clean.

## Impostazioni di backtest

La finestra di dialogo **Backtest** espone le impostazioni di backtest della console cTrader regolabili dall'utente, in modo da non dovere
mai toccare una riga di comando:

- **Symbol / Timeframe** — il timeframe è un **elenco a discesa di ogni periodo cTrader** (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1` e i periodi Renko/Range/Heikin), nel
  maiuscole canoniche della console, quindi scegli sempre un `--period` valido.
- **From / To** — la finestra di backtest (`--start` / `--end`).
- **Data mode** — una delle tre modalità cTrader (`--data-mode`): **Tick data** (`tick`, accurato),
  **m1 bars** (`m1`, veloce) o **Open prices only** (`open`, più veloce).
- **Starting balance** — impostazione predefinita `10000` (`--balance`). Un **saldo 0 non esegue operazioni e fa
  sì che cTrader emetta un report vuoto su cui poi si arresta in modo anomalo** ("Message expected"), quindi un saldo diverso da zero è
  sempre inviato.
- **Commission** e **Spread** — `--commission` / `--spread` (spread in pips).

La directory dei dati (`--data-file` / `--data-dir`) è gestita dall'app stessa (una cache per conto, vedere
sopra), non esposta nella finestra di dialogo.

## Pagina dei dettagli dell'istanza

L'apertura di un'istanza (`/instance/{id}`) mostra il suo stato in tempo reale, i log e — per un backtest — la curva di equità
. Il **titolo della scheda del browser** riflette l'istanza specifica (**nome del cBot · tipo · symbol**, ad es.
`TrendBot · Backtest · EURUSD`) in modo che una scheda di esecuzione in tempo reale e una scheda di backtest siano distinguibili a prima vista.
Un'esecuzione e un backtest dello stesso cBot vengono tracciati come **lineage** distinti (uno stable lineage id trasportato
attraverso transizioni di stato), quindi la pagina segue esattamente un'istanza e non mescola mai i dati di un'esecuzione con quelli di un
backtest.

## Controlli del ciclo di vita dell'istanza

Ogni riga di istanza (e la sua pagina di dettaglio) ha controlli corretti dello stato. Un'istanza **attiva** mostra
**Stop**; una **terminale** (Stopped / Completed / Failed) mostra **Start (▶)** per riavviarla con
lo stesso cBot, conto, symbol, timeframe, ParamSet e immagine (un'esecuzione si riavvia come esecuzione, un
backtest come backtest). Fare clic su Stop mostra un avviso "Stopping…" e disabilita l'icona finché non
si risolve, e un'esecuzione appena creata appare immediatamente nell'elenco — nessun ricaricamento della pagina.

I log della console vengono **persistiti quando un'istanza si termina** — per un'esecuzione (su Stop) e per un
**backtest** (al completamento) — in modo che i log dell'ultima esecuzione rimangono visualizzabili nella pagina di dettaglio e,
tramite la barra degli strumenti dei log, **copiati negli appunti** (icona Copia log) o **scaricati** (icona Scarica log)
anche dopo che il contenitore è scomparso. Entrambi agiscono sul log della console completo dell'istanza, non solo sulla
coda visualizzata sullo schermo.

Un `.algo` **caricato** non è mai stato compilato qui, quindi la sua colonna **Last Build** nella pagina dei cBot è
lasciata vuota (mostra un'ora di compilazione solo per i cBot che compili nel browser).

## Modifica e riesecuzione di un'istanza arrestata

Un'istanza **arrestata** (esecuzione o backtest) ha un controllo **Edit** — un'icona sulla sua riga nell'elenco **e**
accanto a Start/Stop nella sua pagina di dettaglio — che apre una finestra di dialogo **precompilata** con la sua configurazione corrente.
Puoi modificare il **conto di trading, symbol, timeframe, ParamSet e tag di immagine** (e, per un
backtest, la **finestra e tutte le impostazioni di backtest** di cui sopra), quindi **Save & start** la riavvia con le
nuove impostazioni (sostituendo l'istanza arrestata). Il controllo è **disabilitato mentre l'istanza è attiva** —
solo un'istanza arrestata può essere modificata.

## Esecuzione dall'editor di codice

Fare clic su **Run** nell'editor di codice apre una finestra di dialogo invece di eseguire un'esecuzione cieca e hardcoded:

- **Trading account** (obbligatorio) — il conto cTrader a cui si connette il cBot.
- **Parameter set** (facoltativo) — seleziona un set esistente o lascialo vuoto per eseguire con i **valori di parametro predefiniti** del cBot.
  Un pulsante **+** accanto al selettore crea un nuovo ParamSet in linea (vedere di seguito) e lo seleziona.
- **Symbol / Timeframe** default a `EURUSD` / `h1` e possono essere modificati; **Cancel** o **Run**.

Su **Run** l'editor salva e compila il codice sorgente corrente, avvia l'istanza sull'account scelto
con i parametri scelti, quindi traccia i log dei contenitori live. (Il flusso di log inoltra il
cookie di auth dell'utente connesso all'hub SignalR `/hubs/logs`, quindi si connette invece di fallire con
`Invalid negotiation response received`.)

## ParamSet

Un **parameter set** è un set denominato e riutilizzabile di override dei parametri del cBot archiviati come un
oggetto JSON flat che mappa il nome di ogni parametro a un valore scalare, ad es. `{"Period": 14, "Label": "trend"}`. Al
tempo di esecuzione/backtest viene trasformato nel file `params.cbotset` di cTrader
(`{ "Parameters": { … } }`). Puoi creare/modificare un set come JSON grezzo dalla finestra di dialogo **Parameter
sets** del cBot o inline dalla finestra di dialogo Run.

Ogni ParamSet **appartiene a un cBot**: la finestra di dialogo New Parameter Set elenca tutti i tuoi cBot e tu
**devi sceglierne uno** — la creazione è bloccata finché non viene selezionato un cBot. Il **nome di un set è univoco per cBot**:
la creazione o la ridenominazione di un set con un nome che un altro set dello stesso cBot utilizza già viene rifiutata (un errore chiaro
nella finestra di dialogo, `409 Conflict` nell'API). Lo stesso nome può essere riutilizzato su un **diverso** cBot.

Il JSON è **convalidato** al salvataggio: deve essere un singolo oggetto flat i cui valori sono tutti scalari
(string / number / bool). Una radice non-object, un array, un oggetto annidato, un valore `null` o un JSON
malformato viene rifiutato (un errore chiaro nella finestra di dialogo, `400 Bad Request` nell'API). Un oggetto vuoto `{}`
è consentito e significa "nessun override".

## Note sulla CLI della console cTrader

I backtest hanno bisogno di `--data-mode` (predefinito `m1`), date come `dd/MM/yyyy HH:mm` e
argomento JSON posizionale `params.cbotset`; `run` rifiuta `--data-dir` (solo backtest). Vedi
`ContainerCommandHelpers`.

## Nodi e scalabilità

La capacità di esecuzione si scala aggiungendo agenti di nodo (auto-registrazione + heartbeat). Vedi
[node discovery](../operations/node-discovery.md) e [scaling](../deployment/scaling.md).
## È richiesto un conto di trading

L'esecuzione o il backtest di un cBot richiede un conto di trading cTrader a cui connettersi. Finché non ne aggiungi uno in
**Trading accounts**, i pulsanti **Run New cBot** / **Backtest New cBot** sono disabilitati (con un
tooltip) e la pagina mostra un prompt che collega alla configurazione dell'account — non riceverai più un errore grezzo
`stream connect failed` da un bot senza conto.
