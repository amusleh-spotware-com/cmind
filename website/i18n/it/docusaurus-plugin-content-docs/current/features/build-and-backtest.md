---
description: "Crea, esegui, backtesta cBot cTrader (C# e Python, entrambi .NET) dall'editor Monaco integrato nel browser, esegui sull'immagine ufficiale ghcr.io/spotware/ctrader-console."
---

# Crea e backtesta cBot

Crea, esegui, backtesta cBot cTrader (C# **e** Python, entrambi .NET) dall'editor Monaco
integrato nel browser, esegui sull'immagine ufficiale `ghcr.io/spotware/ctrader-console`.

## Crea

- La pagina **Builder** ospita l'editor Monaco; `CBotBuilder` compila il progetto con
  `dotnet build` **in un container temporaneo** (`AppOptions.BuildImage`, directory di lavoro
  montata a `/work`), così i target MSBuild non autorizzati non raggiungono l'host. Il restore
  di NuGet viene memorizzato in cache tra le compilazioni tramite volume condiviso. L'host web
  necessita dell'accesso al socket Docker.
- I template di avvio C# e Python si trovano in `src/Nodes/Builder/Templates/`.

## Esegui e backtesta

- **Instance** = gerarchia di stato TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). La transizione sostituisce l'entità (il cambio id),
  l'id del container viene mantenuto.
- `NodeScheduler` sceglie il nodo idoneo meno carico; `ContainerDispatcherFactory` indirizza
  all'agente HTTP del nodo remoto o al dispatcher Docker locale.
- I poller di completamento riconciliano i container usciti (i container di backtest escono
  automaticamente tramite `--exit-on-stop`); report presente → completato (memorizza
  `ReportJson`), mancante → non riuscito.
- I log del container live trasmettono al browser tramite SignalR; le curve di equity del
  backtest vengono analizzate dal report e rappresentate in grafico.

## I dati di mercato del backtest vengono memorizzati in cache per account

cTrader Console scarica i dati storici tick/bar nella sua `--data-dir`. Quella directory è
una **cache stabile e persistente gestita dall'account di trading** (il suo numero di account) —
montata dal disco del nodo nel suo percorso del container (`/mnt/data`), un **montaggio separato
e non nidificato** dalla directory di lavoro per istanza. Quindi ogni backtest sullo stesso account
**riutilizza** i dati già scaricati anziché scaricarne di nuovi a ogni esecuzione. (In precedenza
la directory dei dati si trovava nella directory di lavoro per istanza, il cui id cambiava a ogni
esecuzione, il che costringeva a un nuovo download ad ogni backtest.) La directory di lavoro
per istanza temporanea contiene ancora l'algoritmo, i parametri, la password e il report; la cache
dei dati condivisa viene conteggiata nell'utilizzo dei dati di backtest di un nodo e cancellata
dall'azione di pulizia del nodo.

## Impostazioni del backtest

La finestra di dialogo **Backtest** espone ogni impostazione che l'interfaccia a riga di comando
del backtest di cTrader Console accetta, quindi non dovrai mai toccare una riga di comando:

- **From / To** — la finestra del backtest (`--start` / `--end`).
- **Data mode** — `m1` (barre di 1 minuto) o `tick` (`--data-mode`).
- **Starting balance** — per impostazione predefinita `10000` (`--balance`). Un **saldo pari a 0
  non piazza alcun trade e fa sì che cTrader generi un report vuoto su cui si arresta in modo
  anomalo** ("Message expected"), quindi un saldo diverso da zero viene sempre inviato.
- **Commission** e **Spread** (`--commission` / `--spread`, spread in pip).
- **Advanced options** — una casella `name=value` a forma libera per riga per qualsiasi altra
  opzione di backtest supportata da cTrader (ad es. `applyCommissionAutomatically=true`); ogni
  riga diventa un argomento CLI `--name value`.

## Pagina dei dettagli dell'istanza

L'apertura di un'istanza (`/instance/{id}`) mostra il suo stato live, i log e, per un backtest,
la curva di equity. Il **titolo della scheda del browser** riflette l'istanza specifica (**nome
cBot · tipo · simbolo**, ad es. `TrendBot · Backtest · EURUSD`), quindi una scheda di esecuzione
live e una scheda di backtest sono distinguibili a colpo d'occhio. Un'esecuzione e un backtest
dello stesso cBot vengono tracciati come **lignaggi** distinti (un id di lignaggio stabile mantenuto
tra le transizioni di stato), quindi la pagina segue esattamente un'istanza e non mescola mai i dati
di un'esecuzione con quelli di un backtest.

## Controlli del ciclo di vita dell'istanza

Ogni riga di istanza (e la sua pagina di dettaglio) ha controlli corretti per lo stato. Un'istanza
**attiva** mostra **Stop**; una **terminale** (Stopped / Completed / Failed) mostra **Start (▶)**
per riavviarla con lo stesso cBot, account, simbolo, timeframe, set di parametri e immagine
(un'esecuzione si riavvia come esecuzione, un backtest come backtest). Facendo clic su Stop viene
visualizzato un avviso "Stopping…" e l'icona viene disabilitata fino alla risoluzione, e un'esecuzione
appena creata viene visualizzata immediatamente nell'elenco — nessun ricaricamento della pagina.

I log della console vengono **persistiti quando un'istanza si interrompe** — per un'esecuzione
(al click su Stop) e per un **backtest** (al completamento) — quindi i log dell'ultima esecuzione
rimangono visibili sulla pagina dei dettagli e, tramite la barra degli strumenti dei log, possono
essere **copiati negli appunti** (icona Copia log) o **scaricati** (icona Scarica log) anche dopo
che il container è scomparso. Entrambi agiscono sul log della console completa dell'istanza,
non solo sulla coda visualizzata sullo schermo.

Un `.algo` **caricato** non è mai stato creato qui, quindi la sua colonna **Last Build** nella
pagina dei cBot viene lasciata vuota (mostra un'ora di compilazione solo per i cBot che crei nel
browser).

## Modifica e riesecuzione di un'istanza arrestata

Un'istanza **arrestata** (esecuzione o backtest) ha un controllo **Edit** — un'icona sulla sua riga
nell'elenco **e** accanto a Start/Stop nella sua pagina di dettaglio — che apre una finestra di dialogo
**precompilata** con la sua configurazione corrente. Puoi cambiare l'**account di trading, simbolo,
timeframe, set di parametri e tag dell'immagine** (e, per un backtest, la **finestra e tutte le
impostazioni del backtest** sopra), quindi **Save & start** la riavvia con le nuove impostazioni
(sostituendo l'istanza arrestata). Il controllo è **disabilitato mentre l'istanza è attiva** — solo
un'istanza arrestata può essere modificata.

## Esegui dall'editor di codice

Facendo clic su **Run** nell'editor di codice si apre una finestra di dialogo anziché avviare
un'esecuzione cieca e hardcoded:

- **Trading account** (obbligatorio) — l'account cTrader a cui si connette il cBot.
- **Parameter set** (facoltativo) — scegli un set esistente, oppure lascialo vuoto per eseguire
  con i **valori dei parametri predefiniti** del cBot. Un pulsante **+** accanto al selettore crea
  un nuovo set di parametri inline (vedi sotto) e lo seleziona.
- **Symbol / Timeframe** per impostazione predefinita sono `EURUSD` / `h1` e possono essere modificati;
  **Cancel** o **Run**.

Su **Run** l'editor salva + compila il source corrente, avvia l'istanza sull'account scelto
con i parametri scelti, quindi visualizza i log del container live. (Il flusso del log invia il
cookie di autenticazione dell'utente accesso all'hub SignalR `/hubs/logs`, quindi si connette anziché
non riuscire con `Invalid negotiation response received`.)

## Set di parametri

Un **parameter set** è un set denominato e riutilizzabile di override dei parametri del cBot
memorizzati come oggetto JSON flat che associa ogni nome di parametro a un valore scalare,
ad es. `{"Period": 14, "Label": "trend"}`. Al momento dell'esecuzione/backtest viene trasformato
nel file `params.cbotset` di cTrader (`{ "Parameters": { … } }`). Puoi creare/modificare un set
come JSON non elaborato dalla finestra di dialogo **Parameter sets** del cBot o inline dalla
finestra di dialogo Run.

Ogni set di parametri **appartiene a un cBot**: la finestra di dialogo New Parameter Set elenca
tutti i tuoi cBot e **devi scegliere uno** — la creazione viene bloccata finché non viene selezionato
un cBot. Il **nome di un set è univoco per cBot**: la creazione o la ridenominazione di un set
con un nome già utilizzato da un altro set dello stesso cBot viene rifiutata (un errore chiaro
nella finestra di dialogo, `409 Conflict` all'API). Lo stesso nome può essere riutilizzato su un
**diverso** cBot.

Il JSON è **validato** al salvataggio: deve essere un singolo oggetto flat i cui valori sono
tutti scalari (string / number / bool). Una radice non-oggetto, un array, un oggetto nidificato,
un valore `null`, o JSON malformato viene rifiutato (un errore chiaro nella finestra di dialogo,
`400 Bad Request` all'API). Un oggetto vuoto `{}` è consentito e significa "nessun override".

## Note sulla CLI di cTrader Console

I backtest necessitano di `--data-mode` (per impostazione predefinita `m1`), date come
`dd/MM/yyyy HH:mm`, e argomento posizionale JSON `params.cbotset`; `run` rifiuta `--data-dir`
(solo per backtest). Vedi `ContainerCommandHelpers`.

## Nodi e scalabilità

La capacità di esecuzione si ridimensiona aggiungendo agenti nodi (auto-registrazione + heartbeat).
Vedi [node discovery](../operations/node-discovery.md) e [scaling](../deployment/scaling.md).

## È necessario un account di trading

L'esecuzione o il backtest di un cBot richiede un account di trading cTrader a cui connettersi.
Finché non ne aggiungi uno in **Trading accounts**, i pulsanti **Run New cBot** / **Backtest New cBot**
sono disabilitati (con un tooltip) e la pagina mostra un prompt con link alla configurazione dell'account
— non colpirai più un errore grezzo `stream connect failed` da un bot senza account.
