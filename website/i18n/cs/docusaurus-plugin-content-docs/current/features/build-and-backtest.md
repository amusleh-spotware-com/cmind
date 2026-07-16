---
description: "Build, run, backtest cTrader cBots (C# i Python, oboje .NET) z in-browser Monaco IDE, běží na oficiálním ghcr.io/spotware/ctrader-console image."
---

# Build & backtest cBots

Build, run, backtest cTrader cBots (C# **i** Python, oboje .NET) z in-browser Monaco
IDE, běží na oficiálním `ghcr.io/spotware/ctrader-console` image.

## Build

- **Builder** stránka hostuje Monaco editor; `CBotBuilder` kompiluje projekt s
  `dotnet build` **v throwaway container** (`AppOptions.BuildImage`, work dir bind-mount
  at `/work`), takže untrusted user MSBuild targets nedosáhnou hosta. NuGet restore cached
  across builds přes shared volume. Web host potřebuje Docker socket access.
- C# + Python starter templaty žijí v `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = TPH state hierarchy (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Transition replace entity (id change),
  container id carried over.
- `NodeScheduler` pick nejméně zatížený eligible node; `ContainerDispatcherFactory` route to
  remote node HTTP agent nebo local Docker dispatcher.
- Completion pollers reconcile exited containers (backtest containers self-exit via
  `--exit-on-stop`); report present → completed (store `ReportJson`), missing → failed.
- Živé container logy streamují do prohlížeče přes SignalR; backtest equity curves parsované z
  report + charted.

## cTrader Console CLI poznámky

Backtesty potřebují `--data-mode` (default `m1`), datumy jako `dd/MM/yyyy HH:mm`, a
`params.cbotset` JSON positional arg; `run` reject `--data-dir` (backtest-only). Viz
`ContainerCommandHelpers`.

## Nodes & scale

Execution capacity scale přidáváním node agentů (samo-registrace + heartbeat). Viz
[node discovery](../operations/node-discovery.md) a [scaling](../deployment/scaling.md).

## Spuštění z editoru kódu

Kliknutí na **Spustit** v editoru kódu otevře dialog místo slepého, napevno zakódovaného spuštění:

- **Obchodní účet** (povinné) — účet cTrader, ke kterému se cBot připojuje.
- **Sada parametrů** (volitelné) — vyberte existující sadu, nebo ponechte prázdné pro spuštění s **výchozími hodnotami parametrů** cBota. Tlačítko **+** vedle výběru vytvoří novou sadu parametrů přímo zde (viz níže) a vybere ji.
- **Symbol / Časový rámec** jsou výchozí `EURUSD` / `h1` a lze je změnit; **Zrušit** nebo **Spustit**.

Při **Spustit** editor uloží a sestaví aktuální zdrojový kód, spustí instanci na zvoleném účtu se zvolenými parametry a poté sleduje živé logy kontejneru. (Proud logů přeposílá autentizační cookie přihlášeného uživatele do SignalR hubu `/hubs/logs`, takže se připojí místo selhání s `Invalid negotiation response received`.)

## Sady parametrů

**Sada parametrů** je pojmenovaná, opakovaně použitelná sada přepisů parametrů cBota, uložená jako plochý objekt JSON mapující každý název parametru na skalární hodnotu, např. `{"Period": 14, "Label": "trend"}`. Při spuštění/backtestu je převedena na soubor cTrader `params.cbotset` (`{ "Parameters": { … } }`). Sadu lze vytvořit/upravit jako čisté JSON z dialogu **Sady parametrů** cBota nebo přímo z dialogu Spustit.

JSON je při uložení **validován**: musí to být jediný plochý objekt, jehož všechny hodnoty jsou skalární (řetězec / číslo / bool). Kořen, který není objekt, pole, vnořený objekt, hodnota `null` nebo poškozené JSON jsou odmítnuty (jasná chyba v dialogu, `400 Bad Request` v API). Prázdný objekt `{}` je povolen a znamená „žádné přepisy".

## Ovládání životního cyklu instance

Každý řádek instance (a její stránka s detaily) má ovládací prvky odpovídající stavu. **Aktivní** instance zobrazuje **Zastavit**; **terminální** (Zastavená / Dokončená / Selhala) zobrazuje **Spustit (▶)** pro opětovné spuštění se stejným cBotem, účtem, symbolem, časovým rámcem, sadou parametrů a image (běh se spustí jako běh, backtest jako backtest). Kliknutí na Zastavit zobrazí upozornění „Zastavování…" a deaktivuje ikonu, dokud se operace nedokončí; nově vytvořený běh se v seznamu objeví okamžitě — bez opětovného načtení stránky.

Protokoly konzole se **při ukončení instance uchovávají** — jak pro běh (při zastavení), tak pro **backtest** (při dokončení) — takže protokoly posledního běhu zůstávají viditelné na stránce s detaily a stažitelné pomocí ikony **Stáhnout protokoly** i poté, co kontejner zmizí.
