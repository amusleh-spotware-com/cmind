---
title: 0002 – Instance-Status ist TPH; ein Übergang ersetzt die Entity
description: Warum eine Instance-ID sich ändert, während sie ihren Lebenszyklus durchläuft, und warum die Container-ID der stabile Schlüssel ist.
---

# 0002 – Instance-Status ist TPH; ein Übergang ersetzt die Entity

## Kontext

Eine Run/Backtest-Instance bewegt sich durch Zustände (pending → scheduled → starting → running → terminal). Wir modellieren State mit EF Core **Table-Per-Hierarchy (TPH)**: jeder Zustand ist ein Subtyp (`StartingRunInstance`, `RunningRunInstance`, ...). EF's TPH Diskriminator-Spalte **kann nicht geändert werden** auf einer bestehenden Row.

## Entscheidung

Ein State-Übergang **ersetzt die Entity** mit einer neuen Subtyp-Instanz statt ein Status-Feld zu mutieren. Weil die Row ersetzt wird, ändert sich die **Instance-ID** über starting → running → terminal. Die **Container-ID ist stabil** und wird über Übergänge getragen; der HTTP-Node-Agent wird durch Container-ID für Status/Report/Stop/Logs schlüsselt.

## Konsequenzen

- Jeder State ist ein unterschiedlicher Typ mit nur den Feldern und Methoden, die in diesem State gültig sind – illegale Übergänge und unsinniger Feldzugriff sind Compile Errors, nicht Runtime Checks.
- Aufrufer dürfen eine Instance-ID **nicht** über einen Übergang cachen; verwende die Container-ID als den stabilen Handle für alles, das sich über States erstreckt.
- Übergangslogik lebt in `InstanceTransitions`; die ID-Änderung ist absichtlich, nicht ein Bug.
