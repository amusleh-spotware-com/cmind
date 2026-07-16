---
description: "Build, run, backtest cTrader cBots (C# and Python, entrambi .NET) da Monaco IDE in-browser, eseguiti su immagine ufficiale ghcr.io/spotware/ctrader-console."
---

# Build & backtest cBots

Build, run, backtest cTrader cBots (C# **e** Python, entrambi .NET) da Monaco
IDE in-browser, eseguiti sull'immagine ufficiale `ghcr.io/spotware/ctrader-console`.

## Build

- La pagina **Builder** host Monaco editor; `CBotBuilder` compila il progetto con
  `dotnet build` **in container monouso** (`AppOptions.BuildImage`, work dir bind-mount
  a `/work`), così i target MSBuild non trusted dell'utente non raggiungono l'host. Il restore NuGet è cached
  attraverso i build via volume condiviso. L'host Web necessita accesso al Docker socket.
- I starter template C# + Python vivono in `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = gerarchia stato TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). La transizione rimpiazza l'entità (cambio id),
  il container id è portato over.
- `NodeScheduler` sceglie il nodo eleggibile meno caricato; `ContainerDispatcherFactory` instrada all'
  agente nodo HTTP remoto o al dispatcher Docker locale.
- I poller di completamento riconciliano i container usciti (i container backtest escono da soli via
  `--exit-on-stop`); report presente → completed (memorizza `ReportJson`), mancante → failed.
- I log container live stream al browser su SignalR; le curve equity del backtest sono parsificate dal
  report + charted.

## Note CLI cTrader Console

I backtest necessitano `--data-mode` (default `m1`), date come `dd/MM/yyyy HH:mm`, e
`params.cbotset` JSON positional arg; `run` rifiuta `--data-dir` (solo backtest). Vedere
`ContainerCommandHelpers`.

## Nodi e scale

La capacità di esecuzione scala aggiungendo agenti nodo (auto-register + heartbeat). Vedere
[node discovery](../operations/node-discovery.md) e [scaling](../deployment/scaling.md).

## È richiesto un account di trading

Eseguire o backtestare un cBot necessita di un account di trading cTrader a cui connettersi. Finché non ne
aggiungi uno sotto **Trading accounts**, i pulsanti **Run New cBot** / **Backtest New cBot** sono
disabilitati (con una tooltip) e la pagina mostra un prompt che linka al setup dell'account — non ricevi
più un raw errore `stream connect failed` da un bot senza account.

## Eseguire dall'editor di codice

Fare clic su **Esegui** nell'editor di codice apre una finestra di dialogo invece di avviare un'esecuzione cieca e cablata:

- **Conto di trading** (obbligatorio) — il conto cTrader a cui si connette il cBot.
- **Set di parametri** (facoltativo) — scegli un set esistente oppure lascialo vuoto per eseguire con i **valori dei parametri predefiniti** del cBot. Un pulsante **+** accanto al selettore crea un nuovo set di parametri in linea (vedi sotto) e lo seleziona.
- **Simbolo / Timeframe** sono per impostazione predefinita `EURUSD` / `h1` e modificabili; **Annulla** o **Esegui**.

All'**Esecuzione** l'editor salva e compila il codice sorgente corrente, avvia l'istanza sul conto scelto con i parametri scelti, quindi segue i log del container in tempo reale. (Il flusso di log inoltra il cookie di autenticazione dell'utente connesso all'hub SignalR `/hubs/logs`, così si connette invece di fallire con `Invalid negotiation response received`.)

## Set di parametri

Un **set di parametri** è un insieme denominato e riutilizzabile di override dei parametri del cBot, memorizzato come oggetto JSON piatto che associa ogni nome di parametro a un valore scalare, ad es. `{"Period": 14, "Label": "trend"}`. Al momento dell'esecuzione/backtest viene trasformato nel file cTrader `params.cbotset` (`{ "Parameters": { … } }`). Puoi creare/modificare un set come JSON grezzo dalla finestra di dialogo **Set di parametri** del cBot o in linea dalla finestra di dialogo Esegui.

Il JSON viene **validato** al salvataggio: deve essere un singolo oggetto piatto i cui valori siano tutti scalari (stringa / numero / bool). Una radice non-oggetto, un array, un oggetto annidato, un valore `null` o JSON malformato viene rifiutato (errore chiaro nella finestra di dialogo, `400 Bad Request` nell'API). Un oggetto vuoto `{}` è consentito e significa «nessun override».

## Controlli del ciclo di vita dell'istanza

Ogni riga di istanza (e la sua pagina di dettaglio) ha controlli corretti in base allo stato. Un'istanza **attiva** mostra **Arresta**; una **terminale** (Arrestata / Completata / Fallita) mostra **Avvia (▶)** per rilanciarla con lo stesso cBot, conto, simbolo, timeframe, set di parametri e immagine (un'esecuzione riparte come esecuzione, un backtest come backtest). Facendo clic su Arresta compare un avviso «Arresto in corso…» e l'icona viene disabilitata finché non si risolve; un'esecuzione appena creata compare subito nell'elenco, senza ricaricare la pagina.

I log della console vengono **conservati quando un'istanza termina** — sia per un'esecuzione (all'arresto) sia per un **backtest** (al completamento) — così i log dell'ultima esecuzione restano visibili nella pagina di dettaglio e scaricabili tramite l'icona **Scarica log**, anche dopo che il container è sparito.
