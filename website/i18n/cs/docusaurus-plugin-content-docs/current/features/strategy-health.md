---
description: "Zdraví strategie a rozpad alfou — deterministická detekce rozpadu, která porovnává poslední Sharpe strategie s jejím dřívějším výkonem a lokalizuje největší posun průměru (CUSUM change-point), vrací verdikt Healthy / Degrading / Decayed / Unknown."
---

# Zdraví strategie a rozpad alfou

Každá výhoda se rozpadá — výzkum je jasný, že poločas rozpadu kvantitativní strategie se zhroutil z let na měsíce, takže *adaptace vítězí nad objevem*. Monitor Zdraví strategie vám řekne, z vlastní historie výnosů strategie, zda je výhoda ještě přítomna.

Otevřete **cBots → Zdraví strategie** (`/quant/health`).

## Co dělá

S danou řadou výnosů (nebo křivkou vlastního kapitálu, nejstaršímu napřed):

- rozděluje historii na **dřívější** a **nedávnou** polovinu a porovnává jejich Sharpe ratio;
- spouští **CUSUM change-point** sken k lokalizaci pozorování, kde se průměr nejzřetelněji posunul (zlom režimu), hlášený pouze pokud je odchylka statisticky pozoruhodná;
- vrací verdikt:

| Verdikt | Význam |
|---|---|
| **Healthy** | Nedávný výkon je v souladu s (nebo lepší než) dřívější záznam. |
| **Degrading** | Nedávné Sharpe je podstatně slabší než dřívější záznam — sledujte pozorně. |
| **Decayed** | Výhoda se prakticky vytratila v nedávném okně — zvažte pozastavení. |
| **Unknown** | Není dostatek historie k posouzení. |

- **Přímo z běhu backtestu — bez kopírování a vkládání.** Každý dokončený backtest odhaluje ikonu srdce **Zkontroluj zdraví strategie** na řádku seznamu **Backtest** a v zobrazení podrobností jeho instance; jedním kliknutím spustíte monitor na uloženou křivku vlastního kapitálu tohoto běhu a zobrazíte verdikt v dialogu. Ikona je zakázána, dokud se backtest nespustí a nevytvoří report, takže to nikdy není mrtvý ovládací prvek. Pod kapotou je to `POST /api/quant/health/backtest/{instanceId}`, který čte křivku vlastního kapitálu z uloženého reportu.

```http
POST /api/quant/health
{ "returns": [...] }   // nebo { "equity": [...] }
```

## Proč je to spolehlivé

Je to čistý, deterministický doménový kód (`Core.Health`) bez závislosti na infrastruktuře a bez externích volání — jednotkově testován na případy rozpadlé, degradující se, zdravé a příliš krátké, a na lokalizaci change-point. Je manuálním doplňkem neustále se spouštěných kontrol zdraví, které zálohují autonomní agenty: stejné statistiky řídí jistič, který derisks živou strategii, jejíž výhoda slábe.
