---
description: "Strategija zdravje & alfa padec — deterministična padec zaznamenovanje, ki primerjava a strategija-ov nedavne Sharpe na njegovanja prej zaznamenovanje in locira na največji srednja-shift (CUSUM sprememba-točka), vrače a Zdravo / Slabja / Decayed sodbo."
---

# Strategija zdravje & alfa padec

Vsak rob pada — na raziskava je topa, ki je na pol-živeti od a quant strategija je sesulo iz leta
do mesece, zato *prilagoditev udari odkritje*. Na Strategija zdravje monitor pove ti, iz a strategija-ov lastne
povračilo zgodovina, ali na rob je še tam.

Odprite **cBots → Strategija zdravje** (`/quant/health`).

## Kaj naredi

Glede na a povračilo niz (ali kapitala krivulja, najstarejši prvi), ga:

- deli zgodovina v a **prej** in a **nedavne** pol in primerjava njihove Sharpe razmerji;
- teče a **CUSUM sprememba-točka** pregleda za locira na opažanja kje na srednja najbolj jasno shifts (a
  režim zlom), poročan samo ko na odklon je statistično bistveno;
- vrne a sodba:

| Sodba | Pomen |
|---|---|
| **Zdravo** | Nedavni zmogljivost je v zanko z (ali bolje kot) na prej zaznamenovanje. |
| **Slabja** | Nedavne Sharpe je bistveno slabše kot na prej zaznamenovanje — pazi blizu. |
| **Decayed** | Na rob je učinkovito izginila v na nedavne okna — razmisli ustavljanja. |
| **Neznano** | Ne dovolj zgodovina za sojenje. |

```http
POST /api/quant/health
```
