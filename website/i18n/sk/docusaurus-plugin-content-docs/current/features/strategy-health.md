---
description: "Strategy Health & Alpha Decay — deterministická detekcia rozpadu, ktorá porovnáva nedávny Sharpe stratégie s jej staršou históriou a lokalizuje najväčšiu zmenu priemeru (CUSUM bod zmeny), vracia Healthy / Degrading / Decayed verdict."
---

# Strategy Health & Alpha Decay

Každá výhoda sa rozpadá — výskum jasne ukazuje, že polčas rozpadu kvantitatívnej stratégie sa skrátil z rokov na mesiace, takže *adaptácia poráža objavovanie*. Monitor Strategy Health vám hovorí, priamo z vlastnej histórie návratov stratégie, či je výhoda stále prítomná.

Otvorte **cBots → Strategy Health** (`/quant/health`).

## What it does

Vzhľadom na sériu návratov (alebo krivku kapitálu, najstarší ako prvý):

- rozdelí históriu na **staršiu** a **nedávnu** polovicu a porovná ich Sharpe koeficienty;
- spustí **CUSUM scan bodu zmeny** na lokalizáciu pozorovania, kde sa priemer najjasnejšie posunul (zmena režimu), nahlásený iba keď je odchýlka štatisticky výrazná;
- vracia verdict:

| Verdict | Meaning |
|---|---|
| **Healthy** | Nedávny výkon je v súlade s (alebo lepší ako) staršia história. |
| **Degrading** | Nedávny Sharpe je podstatne slabší ako staršia história — sledujte pozorne. |
| **Decayed** | Výhoda sa v nedávnom okne efektívne stratila — zvážte pozastavenie. |
| **Unknown** | Nie je dostatok histórie na posúdenie. |

- **Priamo z backtestovania — bez kopírovania a prilepenia.** Každý ukončený backtest odhaľuje srdce **Check strategy health** ikonu na riadku zoznamu **Backtest** a na jeho pohľade podrobností inštancie; jeden klik spustí monitor na uložená krivke kapitálu tohto spustenia a zobrazí verdict v dialógu. Ikona je zakázaná, kým backtest nie je dokončený a nevytvoril správu, takže nikdy nie je zbytočný ovládací prvok. Pod kapotou je to `POST /api/quant/health/backtest/{instanceId}`, ktorý číta krivku kapitálu uloženej správy.

```http
POST /api/quant/health
{ "returns": [...] }   // or { "equity": [...] }
```

## Why it is reliable

Je to čisty, deterministický doménový kód (`Core.Health`) bez infraštrukturnej závislosti a bez externých volaní — jednotkovo testovaný pre prípady rozpadu, zhoršovania, zdravia a príliš krátkej histórie a pre lokalizáciu bodu zmeny. Je to ručný dopĺňajúci prvok ku vždy zapnutým kontrolám zdravia, ktoré zálohu autonómnych agentov: rovnaké štatistiky pohybujú automatickým vypínačom, ktorý znižuje riziko živej stratégie, ktorej výhoda sa stráca.
