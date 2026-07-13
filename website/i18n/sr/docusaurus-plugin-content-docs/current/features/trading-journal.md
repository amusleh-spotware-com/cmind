---
description: "Trading Journal & Coach — analizira vaše sopstvene run-ove i backtest-ove za behavioral leaks (over-concentration, repeated failures, gubitnički bias) i coach-uje vas na strategiju koju već imate. Deterministački, sa opcionim AI narativom."
---

# Trading Journal & Coach

Najnovija genuin korisna kategorija AI-for-trading nije predviđanje tržišta — to je analiza
*vašeg sopstvenog* ponašanja. Trading Journal pretvara vašu istoriju run-ova i backtest-ova u iskren feedback tako da
možete poboljšati strategiju koju već imate.

Otvorite **AI → Trading Journal** (`/journal`).

## Šta otkriva

Iz vaših instanci (run-ova i backtest-ova) računa, deterministički:

- **Win / loss / failure counts i win rate** preko vaših backtest-ova;
- **Behavioral insights** — leaks koji tišina koštaju retail tradere:
  - **Over-concentration** — većina vaše aktivnosti je u jednom simbolu;
  - **Repeated failures** — visok udeo run-ova nije uspeo da se izgradi ili konfiguriše;
  - **Losing bias** — više gubitnih nego dobitnih backtest-ova (sa podsticajem da pokrenete Integrity Lab i
    proverite da li je edge stvaran);
  - čist health bill kada ništa od navedenog ne važi.

```http
GET /api/journal
```

## Zašto je pouzdano

Behavioral analysis je čist, deterministički domen kod (`Core.Journal`) bez infrastrukturnih
zavisnosti — unit-testiran za over-concentration, repeated failures, losing bias, balanced case i
empty account. Činjenice dolaze prvo; AI coach (Portfolio Digest) je opcioni narativni sloj
iznad, kontrolisan API ključem, tako da journal funkcioniše potpuno bez konfigurisanog AI-a.
