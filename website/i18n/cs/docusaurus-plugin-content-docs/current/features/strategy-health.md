---
description: "Strategy Health & Alpha Decay — deterministická detekce rozpadu, která porovnává nedávný Sharpe strategie s jejím dřívějším záznamem a lokalizuje největší posun střední hodnoty (CUSUM change-point), vrací verdikt Healthy / Degrading / Decayed."
---

# Strategy Health & Alpha Decay

Každá výhoda se rozpadá — výzkum je nekompromisní, že poločas kvantitativní strategie se zhroutil z let na měsíce, takže *adaptace poráží objev*. Monitor Strategy Health vám říká, z vlastní historie výnosů strategie, zda výhoda stále je.

Otevřete **cBots → Strategy Health** (`/quant/health`).

## Co to dělá

Vzhledem k řadě výnosů (nebo křivce vlastního kapitálu, od nejstaršího), počítá:

- rozdělí historii na **dřívější** a **nedávnou** polovinu a porovná jejich Sharpe ratio;
- provede **CUSUM change-point** sken pro lokalizaci pozorování, kde se střední hodnota nejjasněji posunula (režimový přelom), hlášeno pouze když je odchylka statisticky pozoruhodná;
- vrací verdikt:

| Verdikt | Význam |
|---|---|
| **Healthy** | Nedávná výkonnost je v souladu s (nebo lepší než) dřívějším záznamem. |
| **Degrading** | Nedávný Sharpe je podstatně slabší než dřívější záznam — sledujte bedlivě. |
| **Decayed** | Výhoda v podstatě zmizela v nedávném okně — zvažte pozastavení. |
| **Unknown** | Nedostatek historie k posouzení. |

```http
POST /api/quant/health
{ "returns": [...] }   // nebo { "equity": [...] }
```

## Proč je to spolehlivé

Je to čistý, deterministický doménový kód (`Core.Health`) bez závislosti na infrastruktuře a bez externích volání — unit testován pro případy decayed, degrading, healthy a příliš krátké a pro lokalizaci change-point. Je to manuální doplněk k vždy zapnutým health checkům, které podporují autonomní agenty: stejné statistiky pohánějí jistič, který snižuje riziko živé strategie, jejíž výhoda mizí.
