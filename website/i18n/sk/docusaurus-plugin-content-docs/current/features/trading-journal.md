---
description: "Trading Journal & Coach — analyzuje vaše vlastné behy a backtesty pre behaviourálne leaks (over-concentration, repeated failures, losing bias) a radí vám o stratégii, ktorú už máte. Deterministic, s voliteľným AI narrative."
---

# Trading Journal & Coach

Najnovšia skutočne užitočná kategória AI-for-trading nie je predpovedanie trhu — je analýza
*vášho vlastného* správania. Trading Journal premieňa vašu históriu behov a backtestov na úprimnú spätnú väzbu, takže
sa môžete zlepšiť v stratégii, ktorú už máte.

Otvorte **AI → Trading Journal** (`/journal`).

## Čo to povrchuje

Z vašich inštancií (behy a backtesty) počíta, deterministicky:

- **Win / loss / failure counts a win rate** naprieč vašimi backtestami;
- **Behaviourálne insights** — leaks, ktoré potichu stoja retailových obchodníkov:
  - **Over-concentration** — väčšina vašej aktivity je v jednom symbole;
  - **Repeated failures** — vysoký podiel behov zlyhalo na build alebo konfiguráciu;
  - **Losing bias** — viac strácajúcich ako vyhrávajúcich backtestov (s ponukou spustiť Integrity Lab a
    skontrolovať, či je výhoda skutočná);
  - čistý health report keď nič z vyššieho neplatí.

```http
GET /api/journal
```

## Prečo je spoľahlivý

Behaviourálna analýza je čistý, deterministický doménový kód (`Core.Journal`) bez infraštruktúrnej
závislosti — unit-testovaný pre over-concentration, repeated failures, losing bias, vyvážený prípad a
prázdny účet. Fakta prichádzajú najprv; AI coach (Portfolio Digest) je voliteľná narrative vrstva
navrch, gated na Anthropic API key, takže journal funguje plne bez nakonfigurovanej AI.
