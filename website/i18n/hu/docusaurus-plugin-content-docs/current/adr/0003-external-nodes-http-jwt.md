---
title: 0003 — A cTrader CLI node-ok HTTP + JWT, nincs SSH/shell
description: Miért a távolsági node-ügynökök csak egy HTTP API-t tárnak fel rövid élettartamú JWT-kel, és soha nem a shell-t.
---

# 0003 — A cTrader CLI node-ok HTTP + JWT, nincs SSH/shell

## Kontextus

A backtest/futtatási konténerek távolsági gépen futnak. Az egyértelmű megközelítés — SSH és docker futtatása — tetszőleges távolsági kódvégrehajtást és hosszú élettartamú hitelesítő adatokat ad a főalkalmazásnak minden node-on. Ez nagy kockázati kör egy olyan rendszernél, amely nem megbízható felhasználói cBot-okat futtat.

## Döntés

Minden távolsági gép egy önálló `CtraderCliNode` **HTTP-ügynököt** futtat, **SSH nélkül és shell nélkül**. A főalkalmazás HTTP-n keresztül hívja meg az ügynököt; minden kérés egy rövid élettartamú **HS256 JWT**-ot hordoz (5 perc, `iss=app-main` / `aud=app-node`), amely az adott node titkával van aláírva. Az ügynök:

- csak az `AllowedImagePrefix`-nek megfelelő képeket futtat (út-határral, így a `ghcr.io/spotware` nem felel meg `ghcr.io/spotware-evil/...`-nak);
- a docker-t `ArgumentList` segítségével futtatja — soha nem shell-string;
- **állapot nélküli**, a konténereket az `app.instance` label alapján keresi meg;
- önmaga regisztrál és szívveréssel jelentkezik a `POST /api/nodes/register`-hez; a főalkalmazás felülírja a `CtraderCliNode`-ot **név alapján**, így egy node túléli az IP-változtatásokat.

## Következmények

- Egy kiszivárgott kérés-token percek alatt lejár; nincs álló shell hitelesítő adat, amelyet el lehetne lopni.
- Az ügynök képessége az "engedélyezett rendszerkép futtatása" határa — nem lehet általános távolsági shell-lé alakítani.
- A node-identitás név alapú, így egy node újra-építése új IP-vel nem hagyja magában az előzményeket.
