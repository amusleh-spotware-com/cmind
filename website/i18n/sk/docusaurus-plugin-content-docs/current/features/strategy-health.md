---
description: "Strategy Health & Alpha Decay — deterministická detekcia decay, ktorá porovnáva recent Sharpe stratégie s jej skorším záznamom a lokalizuje najväčší mean-shift (CUSUM change-point), vracia verdikt Healthy / Degrading / Decayed."
---

# Strategy Health & Alpha Decay

Každá výhoda decayuje — výskum je nekompromisný, že half-life kvantovej stratégie sa skrátila z rokov na
mesiace, takže *adaptácia poráža objav*. Strategy Health monitor vám hovorí, z vlastnej histórie výnosov stratégie,
či je výhoda stále tam.

Otvorte **cBots → Strategy Health** (`/quant/health`).

## Čo to robí

Pri danej sérii výnosov (alebo equity curve, najstaršie prvé),:

- rozdeľuje históriu na ** skoršiu** a **nedávnu** polovicu a porovnáva ich Sharpe ratio;
- spúšťa **CUSUM change-point** sken pre lokalizáciu pozorovania, kde sa mean najjasnejšie posunul (a
  regime break), reportované iba ak je odchýlka štatisticky pozoruhodná;
- vracia verdikt:

| Verdikt | Význam |
|---|---|
| **Healthy** | Nedávna výkonnosť je v línii so (alebo lepšia ako) skorší záznam. |
| **Degrading** | Nedávny Sharpe je citeľne slabší ako skorší záznam — sledujte pozorne. |
| **Decayed** | Výhoda efektívne zmizla v nedávnom okne — zvážte pozastavenie. |
| **Unknown** | Nedostatok histórie na súdenie. |

```http
POST /api/quant/health
{ "returns": [...] }   // alebo { "equity": [...] }
```

## Prečo je spoľahlivý

Čistý, deterministický doménový kód (`Core.Health`) bez infraštruktúrnej závislosti a bez externých
volaní — unit-testovaný pre decayed, degrading, healthy a príliš krátke prípady a pre change-point
lokalizáciu. Je manuálnym sprievodcom k always-on health checks, ktoré backupujú autonómnych agentov:
rovnaké štatistiky hýbu ističom, ktorý de-riskuje live stratégiu, ktorej výhoda mizne.
