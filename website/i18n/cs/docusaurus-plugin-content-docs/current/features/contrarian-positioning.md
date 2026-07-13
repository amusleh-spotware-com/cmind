---
description: "Contrarian Retail Positioning — změní % prodejců na dlouho na kontrarní zkreslení (selhání davu, když je vychýleno), plus point-in-time signální objekty hodnoty, které chrání před zkreslením look-ahead."
---

# Kontrarní Retail Positioning

Maloobchodní dav je jedním z mála skutečně užitečných signálů sentimentu v FX — jako **kontrarní** indikátor. Když je velká většina maloobchodních obchodníků na dlouho, cena historicky měla tendenci klesat a naopak. Tento nástroj změní pozicování davu na lze jednat čtení.

Otevřete **cBots → Contrarian Positioning** (`/quant/positioning`).

## Co dělá

Zadejte **% maloobchodních obchodníků na dlouho** (ze stránky sentimentu vašeho makléře nebo kanálu, jako je FXSSI) a vrátí:

- **Kontrarní zkreslení** — **Bearish** když ≥ 60% je na dlouho (dav příliš dlouho), **Bullish** když ≤ 40% je na dlouho (dav příliš krátko), **Neutral** v pásmu 40–60% nejistoty;
- **Síla** — jak je dav vychýlen (0 = vyvážený, 1 = zcela jednostranný), aby se vážil signál.

```http
POST /api/quant/positioning
{ "longPercent": 72 }
```

## Point-in-time konstrukcí

Pod kapotou signální vrstva (`Core.Signals`) modeluje `PointInTimeSignal`, který je **razítkem s momentem, kdy to bylo poznable** a odmítá být konstruován bez něj. Libovolný backtest nebo autonomní agent, který konzumuje signál, kontroluje `IsKnownAt(decisionTime)` — takže budoucí data nemohou nikdy prosakovat do historického rozhodnutí. Look-ahead zkreslení je největším vrahem reprodukovatelnosti v kvantitativních financích; doménový model to činí strukturálně nemožným.

## Proč je spolehlivý

Čisté, deterministické doménové kódy bez infrastrukturní závislosti — kontrarní prahové hodnoty a guard point-in-time jsou testovány jednotkami, včetně hranic 40/60 a zamítnutí mimo rozsah.
