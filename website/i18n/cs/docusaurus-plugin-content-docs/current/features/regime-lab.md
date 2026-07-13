---
description: "Regime Lab — označí řadu výnosů jako klidné / normální / turbulentní volatilní režimy a hlásí výkonnost v každém režimu, plus Hurstův exponent (trendu vs mean-reversion). Deterministické."
---

# Regime Lab

Jeden Sharpe ratio skrývá pravdu, že většina výhod je podmíněná: skvělá v klidných, trendujících trzích a mrtvá v turbulenci (nebo naopak). Regime Lab rozbije historii strategie na volatilní režimy a ukazuje, jak si vedla v každém z nich — takže víte, *kdy* vaše výhoda skutečně funguje.

Otevřete **cBots → Regime Lab** (`/quant/regimes`).

## Co to dělá

Vzhledem k řadě výnosů (nebo křivce vlastního kapitálu, od nejstaršího), počítá:

- **klouzavou realizovanou volatilitu** v každém bodě a rozděluje historii na režimy **Klidný**, **Normální** a **Turbulentní** podle tercilů této volatility;
- **výkonnost v každém režimu** — pozorování, průměrný výnos, volatilita a Sharpe — takže vidíte, kde výhoda žije;
- odhaduje **Hurstův exponent** pomocí analýzy reskalovaného rozsahu (R/S): nad ~0,55 je řada **trendující / perzistentní**, pod ~0,45 je **mean-revertující** a kolem 0,5 je blízko náhodnému procházení.

```http
POST /api/quant/regimes
{ "returns": [...], "window": 10 }   // nebo { "equity": [...] }
```

## Proč je to spolehlivé

Čistý, deterministický doménový kód (`Core.Regimes`) bez závislosti na infrastruktuře a bez externích volání — unit testován pro separaci režimů (klidná vs turbulentní volatilita) a pro směr Hurstova exponentu (anti-perzistentní řada skóruje pod 0,5, perzistentní trend skóruje nad). Stejný režimový signál napájí reflexní smyčku autonomních agentů, takže agent se může opřít o režimy, kde je jeho výhoda skutečná.
