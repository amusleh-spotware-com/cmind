---
description: "Transaction Cost Analysis — měří kvalitu provádění (slippage v bazických bodech a nesplnění implementace) obchodu vůči jeho příchozí ceně, složený exekuční okraj, na kterém žijí banky. Deterministický."
---

# Transaction Cost Analysis (TCA)

Alfa provádění je malá na obchod a obrovská přes tisíce z nich — je to velká část toho, jak banky a prop desks udržují svůj okraj. TCA měří, jak moc se cena, kterou jste skutečně dosáhli, odchýlila od ceny, když jste se *rozhodli* obchodovat.

Otevřete **cBots → Execution Cost** (`/quant/tca`).

## Co měří

Vzhledem k **příchozí (rozhodovací) ceně**, **straně** a vašim **výplnám** (cena × množství), hlásí:

- **Průměrná cena výplně (VWAP)** — váženou cena, kterou jste skutečně dostali.
- **Slippage (bps)** — drift od příchodu do VWAP v bazických bodech, **podepsaný tak, aby kladné číslo byla náklada** (nákup nad příchod nebo prodej pod ním) a záporné číslo je zlepšení ceny.
- **Nesplnění implementace** — tato náklada vyjádřená v ceně × množství: peníze, které drift stály na tomto obchodě.

```http
POST /api/quant/tca
{ "arrivalPrice": 1.1000, "side": "Buy",
  "fills": [ { "price": 1.1010, "quantity": 100 }, { "price": 1.1020, "quantity": 100 } ] }
```

## Inteligentní krájení (Almgren-Chriss)

Kromě měření nákladů, cMind může naplánovat velký obchod, aby to *minimalizoval*. **cBots → Execution Schedule** (`/quant/execution`) staví **Almgren-Chriss plán optimální exekuce**: vzhledem k celkovému množství, počtu řezů, vaši averzivnosti vůči riziku, volatilitě a dočasného tržního dopadu, vrací velikost pro obchodování v každém řezu. Vyšší averze vůči riziku **front-načítá** plán (snižuje časové riziko); nulová averze vůči riziku se vyrovnává na rovnoměrné **TWAP**. Řezy se vždy sčítají k celkovému.

```http
POST /api/quant/execution-schedule
{ "totalQuantity": 100, "slices": 5, "riskAversion": 2, "volatility": 0.02, "temporaryImpact": 0.1 }
```

## Proč je spolehlivý

Čisté, deterministické doménové kódy (`Core.Execution`) bez infrastrukturní závislosti a bez externích hovorů — testovány jednotkami pro znaménko nákladu buy/sell, zlepšení ceny, nulový slippage, agregaci VWAP a ochranu vstupů. Toto je měřící polovina kvality provádění; jedná se o stejnou metriku nesplnění, kterou kopírující engine používá k posouzení (a s inteligentním krájením snížení) nákladů na zrcadlené obchody.
