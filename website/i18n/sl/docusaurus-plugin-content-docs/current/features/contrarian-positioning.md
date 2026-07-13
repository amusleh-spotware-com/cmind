---
description: "Contrarian Maloprodaja Pozicioniranje — obr % od maloprodaja trgovci dolga v a kontrarian pristranskosti (fada je množica ko je asimetrična), plus točka-v-čas signal vrednosti objekti, ki varujejo proti videti-naprej pristranskosti."
---

# Contrarian Maloprodaja Pozicioniranje

Maloprodaja množica je ena od nekaj resnično-koristno sentiment signali v FX — kot a **kontrarian**
indikator. Ko je velika večina maloprodaja trgovci dolga, cena je zgodnja oskrbovane padajo,
in vice-versa. To orodja obr množica pozicioniranje v a dejanje branja.

Odprite **cBots → Contrarian Pozicioniranje** (`/quant/positioning`).

## Kaj naredi

Vnesite **% od maloprodaja trgovci dolga** (od vaš posredovalstvo sentiment stran ali a dovodite kot FXSSI) in
se vrne:

- **Contrarian pristranskosti** — **Bearish** ko ≥ 60% so dolga (množica preveč dolga), **Bullish** ko ≤ 40% so
  dolga (množica preveč kratka), **Nevtralna** v 40–60% odločitvi pasu;
- **Moč** — kako asimetrična je множica (0 = uravnoteženo, 1 = v celoti ena-stranski), do teža signal.

```http
POST /api/quant/positioning
{ "longPercent": 72 }
```

## Točka-v-čas z gradnjo

Pod pokrova signal sloj (`Core.Signals`) modeli a `PointInTimeSignal`, ki je **žigosan z na
moment je bilo spoznavno** in zavrača da bi se zgraditi brez njega. Vsak backtest ali avtonomna agent, ki
porabi signal preverka `IsKnownAt(decisionTime)` — zato prihodnost podatkov nikoli puščava v a zgodovini
odločitev. Videti-naprej pristranskosti je vrh ponovljivosti pokvarje v quant finančnega; domeni model ga naredi
strukturno mogoče.

## Zakaj je zanesljivo

Čisti, deterministična domeni koda z brez infrastrukture odvisnosti — kontrarian pragi in na
točka-v-čas varovanje so enota-testirani, vključno 40/60 meje in iz-obsega zavrnitev.
