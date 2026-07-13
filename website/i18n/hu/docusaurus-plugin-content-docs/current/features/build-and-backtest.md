---
description: "A cBot-szerzői az egy szöveg-szerkesztő az Monaco-ban az SDK-t, az forráskódot, a rögzítési paramétert és az backtests az összes intézmény felületén nyitott. Az Kontextus-menü a paraméter-értékeket szerkeszti, az előzményt visszaállít."
---

# cBot-szerzői & backtest

A cBot-szerzői az egy szöveg-szerkesztő az Monaco-ban az SDK-t, az forráskódot és az rögzítési paramétereket. A backtest az egy teljes kontextusban futó — adatok, szimbólumok, paraméterek — az a cTrader-konzol.

## cBot-szerkesztés

Az felhasználó az egy .csproj-projekt felépítésű cBot-ot hozza létre vagy módosítja. Az szerkesztő az Monaco-ban futón, az szintaxisbetöltésekkel, az debug-szimbólumokkal. Az "Építés" gomb az CBotBuilder-t futtatja az web-gazdagépen.

## Backtest-konfigurálás

Az felhasználó az egy backtest-munkáját hoz létre az a paramétereket megadásával:

- **Szimbólum** — az kereskedési szimbólum.
- **Periódus** — az időkeret (M1, H1 stb).
- **Kezdeti dátuma** — az backtest-kezdet.
- **Befejezési dátuma** — az backtest-vég.
- **Paraméterek** — az cBot-paraméterek (dinamikus az paramset-ből).

## Háttérben futó munkák

Az BuildJob az web-gazdagépen futón, az egy Docker-konténer belül. Az BacktestJob az egy node-ügynökön futón az `--data-mode=m1` az cTrader-konzol által.

## Tesztek

- **Integráció** — `IntegrationTests/BuildAndBacktestTests.cs`: az építés sikeres, az backtest futón, az eredményt adja vissza.
- **E2E** — `E2ETests/BuildAndBacktestTests.cs`: az szerzői az editorban, az építés, az backtest az parameterrel.
