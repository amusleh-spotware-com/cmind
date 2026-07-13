---
title: Kereskedesi Naplo es Coach
description: "A legujabb valoban hasznos AI-a-kereskedesben kategoria nem a piac joslasa - hanem a sajat futasok es backtesztek elemzese viselkedesi lyukakert (tul-koncentracio, ismetlodo hibak, vesztesegi bias) es coach-ol a mar meglevo stratégiad. Determinisztikus, opcionalis AI narrativval."
---

# Kereskedesi Naplo es Coach

A legujabb valoban hasznos kategoria az AI-a-kereskedesben nem a piac joslasa - hanem a **sajat viselkedésed elemzese**. A Kereskedesi Naplo a futasok és backtesztek történetedet forditja onszuru visszajelzesse, igy javithasd a mar meglevo stratégiát.

Nyisd meg az **AI → Kereskedesi Naplo** (`/journal`)-t.

## Mit felszinre hoz

A peldanokból (futasok és backtesztek) determinisztikusan kiszamítja:

- **Nyeres / veszteség / hiba szamlalók és nyerési arány** a backteszteknél;
- **Viselkedési betekintések** - azok a lyukak, amik csendben kereskedőknek kerülnek:
  - **Tul-koncentracio** - a tevékenységed nagy része egy szimbulumban van;
  - **Ismetlodo hibak** - magas részesedése a futásoknak nem sikerült build-et vagy konfigurálni;
  - **Veszteségi bias** - több vesztő mint nyerő backteszt (egy nudgel, hogy futtasd az Integritas Lab-ot és ellenőrizd, az élő valódi-e);
  - egy tiszta egészségügyi bizonyítvány, amikor a fenti egyike sem alkalmazandó.

```http
GET /api/journal
```

## Miért megbizhato

A viselkedési elemzés tiszta, determinisztikus domain kod (`Core.Journal`) nincs infrastruktura fuggosege - unit-tesztelt a tul-koncentracio, ismetlodo hibak, veszteségi bias, a kiegyensulyozott eset es az ures fiok eseten. A tenyek eloszor; az AI coach (Portfolio Digest) egy opcionalis narrativ réteg felette, gate-elve az Anthropic API kulcsra, igy a napló teljesen működik AI konfigurálás nélkül.
