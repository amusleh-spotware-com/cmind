---
description: "Trading Journal & Coach — analyzuje vaše vlastní běhy a backtesty na behaviorální úniky (přeinvestování, opakovaná selhání, ztrátový bias) a radí vám ohledně strategie, kterou již máte. Deterministické, s volitelnou AI narací."
---

# Trading Journal & Coach

Nejnovější skutečně užitečná kategorie AI pro obchodování není predikovat trh — je to analyzovat *vaše vlastní* chování. Trading Journal přeměňuje vaši historii běhů a backtestů v upřímnou zpětnou vazbu, abyste mohli zlepšit strategii, kterou již máte.

Otevřete **AI → Trading Journal** (`/journal`).

## Co to odhaluje

Z vašich instancí (běhů a backtestů) počítá deterministicky:

- **Počty výher / ztrát / selhání a win rate** napříč vašimi backtesty;
- **Behaviorální poznatky** — úniky, které tiše stojí retail tradery:
  - **Přeinvestování** — většina vaší aktivity je v jednom symbolu;
  - **Opakovaná selhání** — vysoký podíl běhů selhal při sestavení nebo konfiguraci;
  - **Ztrátový bias** — více ztrátových než výherních backtestů (s nabádáním spustit Integrity Lab a zkontrolovat, zda je výhoda skutečná);
  - zdravý výsledek, když nic z výše uvedeného neplatí.

```http
GET /api/journal
```

## Proč je to spolehlivé

Behaviorální analýza je čistý, deterministický doménový kód (`Core.Journal`) bez závislosti na infrastruktuře — unit testován pro přeinvestování, opakovaná selhání, ztrátový bias, vyvážený případ a prázdný účet. Fakta přicházejí první; AI kouč (Portfolio Digest) je volitelná narativní vrstva nahoře, závislá na Anthropic API klíči, takže journal funguje plně i bez nakonfigurované AI.
