---
title: 0003 — cTrader CLI uzly sú HTTP + JWT, bez SSH/shell
description: Prečo vzdialení agenti uzla vystavujú iba HTTP API s krátkodobými JWT a nikdy shell.
---

# 0003 — cTrader CLI uzly sú HTTP + JWT, bez SSH/shell

## Kontext

Kontajnery backtestingu/spustenia sa spúšťajú na vzdialených hostiteľoch. Očividný prístup — SSH a spustenie docker — dáva
hlavnej aplikácii ľubovoľné vzdialené vykonávanie kódu a dlhodobé poverenia na každom uzle. To je
veľký rozsah zásahu pre systém, ktorý spúšťa nedôveryhodné cBoty používateľa.

## Rozhodnutie

Každý vzdialený hostiteľ spúšťa samostatný `CtraderCliNode` **HTTP agent** s **bez SSH a bez shell**. 
Hlavná aplikácia volá agenta cez HTTP; každá požiadavka obsahuje krátkodobý **HS256 JWT** (5-minútový,
`iss=app-main` / `aud=app-node`) podpísaný tajomstvom toho uzla. Agent:

- iba spúšťa obrázky zodpovedajúce `AllowedImagePrefix` (s hranicou cesty, takže `ghcr.io/spotware` nemôže
  zodpovedať `ghcr.io/spotware-evil/...`);
- spúšťa docker cez `ArgumentList` — nikdy reťazec shell;
- je **bezstavový**, nájde kontajnery podľa štítku `app.instance`;
- samoreg stratuje a pulzuje na `POST /api/nodes/register`; hlavná aplikácia vložiť `CtraderCliNode`
  **podľa mena**, takže uzol prežije zmeny IP.

## Dôsledky

- Uniklý token požiadavky vyprší za minúty; nie je tu žiadne stojace shell poverenie, ktoré by sa mohlo ukradnúť.
- Schopnosť agenta je obmedzená na "spustiť povolený obraz" — nemôže sa zmeniť na všeobecný
  vzdialený shell.
- Identita uzla je založená na mene, takže opätovné zriadenie uzla s novou IP neosiroťuje jeho históriu.
