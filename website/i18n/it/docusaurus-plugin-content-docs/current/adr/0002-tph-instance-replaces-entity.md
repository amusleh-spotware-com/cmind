---
title: 0002 — Lo stato dell'istanza è TPH; una transizione sostituisce l'entità
description: Perché l'id di un'istanza cambia man mano che si muove attraverso il suo ciclo di vita e perché l'id del contenitore è la chiave stabile.
---

# 0002 — Lo stato dell'istanza è TPH; una transizione sostituisce l'entità

## Contesto

Un'istanza di esecuzione/backtest si muove attraverso stati (pending → scheduled → starting → running → terminal). Modelliamo lo stato con EF Core **Table-Per-Hierarchy (TPH)**: ogni stato è un sottotipo (`StartingRunInstance`, `RunningRunInstance`, …). La colonna discriminatore TPH di EF **non può cambiare** su una riga esistente.

## Decisione

Una transizione di stato **sostituisce l'entità** con una nuova istanza di sottotipo piuttosto che mutare un campo di stato. Poiché la riga viene sostituita, l'**id dell'istanza cambia** attraverso starting → running → terminal. L'**id del contenitore è stabile** e viene portato attraverso le transizioni; l'agente del nodo HTTP è indexato per id del contenitore per status/report/stop/log.

## Conseguenze

- Ogni stato è un tipo distinto con solo i campi e i metodi validi in quello stato — le transizioni illegittime e l'accesso ai campi privi di significato sono errori di compilazione, non controlli in runtime.
- I chiamanti **non devono** memorizzare nella cache un id di istanza attraverso una transizione; usare l'id del contenitore come handle stabile per qualsiasi cosa che attraversa gli stati.
- La logica di transizione risiede in `InstanceTransitions`; il cambiamento di id è intenzionale, non un bug.
