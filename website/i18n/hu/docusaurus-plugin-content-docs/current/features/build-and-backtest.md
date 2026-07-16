---
title: Epites es backtest
description: "A cMind Epites és Backtest rendszere lehetővé teszi cBot-ok létrehozását, fordításását és backtest-elését egy sandboxolt konténerben, Monaco IDE-val a böngészőben."
---

# Epites es Backtest

A cMind Epites és Backtest rendszere lehetővé teszi cBot-ok létrehozását, fordításását és backtest-elését egy sandboxolt konténerben, Monaco IDE-val a böngészőben.

## Mi az a cBot

A **cBot** automatizált kereskedési robot, amit a cTrader platformon futtatsz. A cMind lehetővé teszi, hogy C# vagy Python nyelven írj cBot-okat, és azokat a platformon keresztül futtasd vagy backtest-eld.

## cBot letrehozasa

1. Menj a **cBots → Epites** oldalra.
2. Válaszd ki a **nyelvet** (C# vagy Python).
3. Válassz egy **sablont** vagy kezd üres projekttel.
4. Kattints a **Létrehozás** gombra.

## Koderosztaly (Monaco IDE)

A beépített Monaco IDE minden szükséges funkciót biztosít:
- **Szintaxis kiemelés** - C# és Python
- **IntelliSense** - kód kiegészítés, hibák jelzése
- **Debug** - töréspontok, változók nézegetése
- **Sablonok** - gyakori stratégiák

## Forditas

A `CBotBuilder` egy sandboxolt MSBuild konténerben fordítja a kódot. Az image az `AllowedImagePrefix` által korlátozott; az ügynök nem éri el a host FS/jhálózatát.

```http
POST /api/cbots/{id}/build
```

Fordítási log: `GET /api/cbots/{id}/build/logs`

## Backtest

A backtest egy teljes historikus futás, szabályozott kezdeti feltételekkel:

1. **Konfiguráció** - symbol, időtartam, kezdeti egyenleg, adat mód (M1/M5/H1).
2. **Paraméterek** - szabadon állítható input paraméterek.
3. **Indítás** - konténer indul a cTrader Console CLI-vel.

```http
POST /api/backtest
{
  "cbotId": "...",
  "symbol": "EURUSD",
  "startDate": "2024-01-01",
  "endDate": "2024-12-31",
  "initialBalance": 10_000,
  "dataMode": "M1",
  "parameters": { "period": 14, "threshold": 0.5 }
}
```

## Eredmenyek

A backtest eredménye:
- **Equity görbe** - nyereség/veszteség az időben
- **Teljesítmény metrikák** - Sharpe, max drawdown, win rate, avg R:R
- **Keresések** - minden megnyitott/zárt pozíció
- **Log** - a cBot kimenete

## Integrity Lab

Minden befejezett backtest automatikusan elérhető az **Integrity Lab**-ban, ahol statisztikai elemzés fut rajta (PSR, DSR, PBO) - lásd [backtest-integrity.md](./backtest-integrity.md).

## Kapcsolodo

- **[Backtest Integrity Lab](./backtest-integrity.md)**
- **[Strategy Health](./strategy-health.md)**
- **[Position Sizing](./position-sizing.md)**

## Futtatás a kódszerkesztőből

A kódszerkesztőben a **Futtatás** gombra kattintva egy párbeszédablak nyílik meg a vak, rögzített futtatás helyett:

- **Kereskedési számla** (kötelező) — a cTrader-számla, amelyhez a cBot csatlakozik.
- **Paraméterkészlet** (opcionális) — válasszon meglévő készletet, vagy hagyja üresen a cBot **alapértelmezett paraméterértékeivel** való futtatáshoz. A választó melletti **+** gomb helyben új paraméterkészletet hoz létre (lásd lent), és kiválasztja azt.
- **Szimbólum / Időkeret** alapértelmezetten `EURUSD` / `h1`, és módosítható; **Mégse** vagy **Futtatás**.

**Futtatáskor** a szerkesztő menti és lefordítja az aktuális forráskódot, elindítja a példányt a kiválasztott számlán a kiválasztott paraméterekkel, majd élőben követi a konténer naplóit. (A naplófolyam továbbítja a bejelentkezett felhasználó hitelesítési sütijét a `/hubs/logs` SignalR-hubnak, így csatlakozik ahelyett, hogy `Invalid negotiation response received` hibával hiúsulna meg.)

## Paraméterkészletek

A **paraméterkészlet** a cBot paraméter-felülbírálásainak elnevezett, újrafelhasználható halmaza, amelyet lapos JSON-objektumként tárolunk, amely minden paraméternevet egy skalárértékhez rendel, pl. `{"Period": 14, "Label": "trend"}`. Futtatáskor/backtesteléskor a cTrader `params.cbotset` fájllá (`{ "Parameters": { … } }`) alakul. A készletet nyers JSON-ként a cBot **Paraméterkészletek** párbeszédablakából vagy helyben a Futtatás párbeszédablakból hozhatja létre/szerkesztheti.

A JSON mentéskor **érvényesítésre** kerül: egyetlen lapos objektumnak kell lennie, amelynek minden értéke skalár (szöveg / szám / bool). A nem objektum gyökér, tömb, beágyazott objektum, `null` érték vagy hibás JSON elutasításra kerül (érthető hiba a párbeszédablakban, `400 Bad Request` az API-nál). Az üres objektum `{}` engedélyezett, és azt jelenti, hogy „nincs felülbírálás".

## A példány életciklusának vezérlői

Minden példánysor (és a részletező oldala) állapothelyes vezérlőkkel rendelkezik. Egy **aktív** példány a **Leállítás** gombot mutatja; egy **végállapotú** (Leállítva / Befejezve / Sikertelen) a **Indítás (▶)** gombot mutatja, hogy ugyanazzal a cBottal, számlával, szimbólummal, időkerettel, paraméterkészlettel és képpel újraindítsa (a futtatás futtatásként, a backteszt backtesztként indul újra). A Leállításra kattintva „Leállítás…" értesítés jelenik meg, és letiltja az ikont, amíg be nem fejeződik; az újonnan létrehozott futtatás azonnal megjelenik a listában — oldalújratöltés nélkül.

A konzolnaplók **a példány befejeződésekor megőrződnek** — futtatásnál (leállításkor) és **backtesztnél** (befejezéskor) egyaránt —, így az utolsó futtatás naplói láthatók maradnak a részletező oldalon, és a **Naplók letöltése** ikonnal letölthetők, még azután is, hogy a konténer megszűnt.
