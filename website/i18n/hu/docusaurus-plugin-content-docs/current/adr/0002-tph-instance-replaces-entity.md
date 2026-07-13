---
title: 0002 — Az instancia-állapot TPH; az átmenet helyettesíti az entitást
description: Miért változik meg az instancia azonosítója, ahogy végigmegy az életcikluson, és miért a konténer azonosítója a stabil kulcs.
---

# 0002 — Az instancia-állapot TPH; az átmenet helyettesíti az entitást

## Kontextus

Egy futtatási/backtest-instancia végigmegy az állapotokon (pending → scheduled → starting → running → terminal).
Az állapotot az EF Core **Table-Per-Hierarchy (TPH)** modellel reprezentáljuk: minden állapot egy altípus
(`StartingRunInstance`, `RunningRunInstance`, …). Az EF TPH megkülönböztetési oszlopa **nem változhat meg**
egy meglévő sorban.

## Döntés

Az állapot-átmenet **helyettesíti az entitást** egy új altípusú instanciával, ahelyett, hogy egy státusz-mezőt módosítana. 
Mivel a sor helyettesítésre kerül, az **instancia azonosítója megváltozik** az starting → running → terminal átmenetekben.
A **konténer azonosítója stabil** és átkerül az átmeneteken; a HTTP node-ügynök a konténer azonosítójával indexelve van az állapot/jelentés/leállítás/naplók számára.

## Következmények

- Minden állapot egy megkülönböztetett típus, csak az adott állapotban érvényes mezőkkel és metódusokkal — az illegális átmenetek és értelmetlen mezőhozzáférések fordítási hibák, nem futásidejű ellenőrzések.
- A hívónak **nem szabad** az instancia azonosítóját az átmenet között gyorstárolnia; a konténer azonosítóját kell stabil fogantyúnak használnia az állapotokon átnyúló műveleteknél.
- Az átmenet-logika az `InstanceTransitions`-ben található; az azonosító-változás szándékos, nem hiba.
