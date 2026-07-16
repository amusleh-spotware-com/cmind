---
description: "Backtest Integrity Lab — determinisztikus, intézményi szintű overfitting-statisztika (Probabilisztikus és Deflált Sharpe, t-statisztika), amely egy nyers backtestet Robusztus / Törékenye / Túlillesztett ítéletre alakít, korrigálva a megpróbált konfigurációk számát."
---

# Backtest Integrity Lab

A kereskedelmi platformok megjelenítenek egy backtest Sharpe-ját vagy nettó nyereségét, és megállnak. Az intézmények soha nem bíznak egy nyers backtestben — azt kérdezik, hogy az eredmény túléli-e a **szelekcióbias és a megpróbált konfigurációk számának korrekciót**. A Backtest Integrity Lab ezt az ellenőrzést hozza el a cMind-be. Ez **determinisztikus matematika** (nincs AI, nincs külső hívások), így az ítélet reprodukálható és minden szám magyarázható.

Nyissa meg a **cBots → Integrity** (`/quant/integrity`) oldalon.

## Amit kiszámít

Egy hozamsort (vagy egy equity/balance görbét) és a megpróbált paraméterkészletek számát figyelembe véve, az analizátor jelentést készít:

- **Sharpe-arány** — periódusenkénti és éves szintű (az idő négyzetgyöke).
- **Probabilisztikus Sharpe-arány (PSR)** — annak az esélye, hogy a *valódi* Sharpe meghaladja a benchmarkot, figyelembe véve a track-record hosszát, a ferdességet és a lapítottságot (Bailey & López de Prado, 2012). Egy rövid vagy vastag farok csökkenti.
- **Deflált Sharpe-arány (DSR)** — PSR mérve egy **deflált benchmark** ellen: a Sharpe, amely az *N véletlen próbálkozás legjobbjából* esperálható a nulla hipotézis alatt (a False Strategy Theorem). Minél több konfigurációt próbálsz, annál magasabb az érték — ez az, ami az overfittinget eltéríti.
- **t-statisztika** az átlagos hozamból. Harvey, Liu & Zhu követésével egy valódi edge-nek **t ≥ 3.0**-t kell meghaladnia, nem a tankönyv 2.0-t.
- **Ferdség / Lapítottság** a hozamokból, amely PSR/DSR korrekciókat táplál.

## Az ítélet

| Ítélet | Jelentés | Szabály |
|---|---|---|
| **Robusztus** | Az edge túléli a megpróbált próbálkozásokat. | DSR ≥ 95% **és** PSR ≥ 95% **és** \|t\| ≥ 3.0 |
| **Törékenye** | Statisztikailag élő, de nem meggyőzően — ne méretezze fel erre egyedül. | a kettő között |
| **Túlillesztett** | Legvalószínűbb, hogy a szelekcióbias műterméke, nem valódi edge. | DSR < 90% |

Minden eredmény egy egyszerű angol nyelvű indoklást tartalmaz, így a "miért" soha nem rejtett.

## Backtest Túlillesztésének Valószínűsége (próbálkozások között)

Egy próbálkozás *száma* táplálása jó; a **megpróbált minden konfiguráció tényleges out-of-sample sorozatát** táplálni még jobb. Illessze be azokat az opcionális **trial grid**-be (egy sorozat soronként) és a cMind futtatja a **Kombinatorikusan-Szimmetrikus Kereszt-Validációt** (Bailey, Borwein, López de Prado & Zhu, 2015): felosztja a megfigyeléseket csoportokra, és a felét in-sampleként kiválasztott minden módszer esetén az in-sample legjobb konfigurációt választja és ellenőrzi, hogy az győztes az out-of-sample alsó felében landol-e. A **Backtest Túlillesztésének Valószínűsége (PBO)** azoknak az osztásoknak a hányada, ahol a győztes nem általánosított. A PBO közel 0-hoz azt jelenti, hogy a legjobb konfiguráció valóban a legjobb; a 0,5 vagy nagyobb PBO azt jelenti, hogy a kiválasztási folyamata zajt választ — az ítélet **Túlillesztett** lesz, függetlenül attól, hogy milyen jó volt a nyertes.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Amikor a natív cTrader Console optimizer megérkezik, a cMind automatikusan ide táplálja a teljes próbálkozási felületét.

## Próbálkozások — a szám, amely számít

A `Trials` a **hány paraméter-készletet tesztelt** mielőtt ezt kiválasztotta. Egy stratégia tesztelése és tízezer tesztelése és a legjobb megtartása drámaian különbözik: az utóbbi véletlenül keltesz egy magas in-sample Sharpe-t. Az őszinte próbálkozási szám táplálása az egész lényeg — megemeli a deflációt és egy "kiváló" backtestet **Túlillesztett**-re mozgathat. Amikor a natív cTrader Console optimizer megérkezik, a cMind automatikusan táplálja azt a sweep valódi rács-méretét.

## Bemenetek

- **Periodikus hozamok** — egy szám periódusenkénti (pl. `0.01` = +1%). Legalább kettő. A mező az Ön gépelésének megfelelően érvényesíti: számolja a valid számokat, megjelöli azokat a tokeneket, amelyek nem szám, és csak akkor engedélyezi az **Analyze**-t, ha legalább két tiszta érték jelen van (a próbálkozási grid akkor engedélyezi az **Assess overfitting**-et, ha két, négy vagy több szám sorozata kész).
- **Equity / balance görbe** — a cMind az egymást követő egyszerű hozamokat levezetette.
- **Közvetlenül egy backtest futásból — nincs copy-paste.** Minden befejezett backtest egy pajzs **Check backtest integrity** ikont mutat a **Backtest** lista sorában és annak instance detail nézetében; egy kattintás futtatja a Lab-ot az adott futás tárolt equity görbéjén és egy párbeszédablakban megjeleníti az ítéletet. Az ikon le van tiltva, amíg a backtest nem fejeződik be és nem készít jelentést, így soha nem egy inert vezérlő. Ezt a `POST /api/quant/integrity/backtest/{instanceId}` alatt működik, amely a tárolt jelentés equity görbéjét olvassa.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Visszaadja az ítéletet, az összes metrikát és az indoklást. A `POST /api/quant/integrity/backtest/{id}` ugyanezt az elemzést futtatja egy befejezett backtesten, amely az Önöé.

## Miért megbízható

A statisztika tiszta függvények a domain mag-ban (`Core.Quant`) nulla infrastruktúra-függőséggel — nem podem lecsuktatva egy hálózati akadályban, és rögzítve vannak a golden-vector egységtesztekkel a közzétett képletekkel szemben. A normál CDF/inverz zárt formájú közelítések (Abramowitz-Stegun / Acklam), így ugyanazok a bemenetek mindig ugyanazt az ítéletet adják.
