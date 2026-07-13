---
title: 0003 — Uzly cTrader CLI jsou HTTP + JWT, bez SSH/shell
description: Proč vzdálení agenti uzlu vystavují pouze HTTP API s krátkodobými JWT a nikdy ne shell.
---

# 0003 — Uzly cTrader CLI jsou HTTP + JWT, bez SSH/shell

## Kontext

Kontejnery backtestování/spuštění se provádějí na vzdálených hostitelích. Zřejmý přístup — SSH dovnitř a spuštění dockeru — dává
hlavní aplikaci libovolné vzdálené spuštění kódu a dlouhodobé přihlašovací údaje na každém uzlu. To je
velký rozsah problému pro systém, který spouští nedůvěryhodné cBoty uživatelů.

## Rozhodnutí

Každý vzdálený hostitel spouští samostatný agent `CtraderCliNode` **HTTP** bez **SSH a bez shell**. Hlavní
aplikace volá agenta přes HTTP; každý požadavek nese krátkodobý **HS256 JWT** (5 minut,
`iss=app-main` / `aud=app-node`) podepsaný tajemstvím tohoto uzlu. Agent:

- spouští pouze image odpovídající `AllowedImagePrefix` (s hranicí cesty, aby `ghcr.io/spotware` nemohlo
  odpovídat `ghcr.io/spotware-evil/...`);
- provádí docker přes `ArgumentList` — nikdy řetězec shell;
- je **bezstavový**, hledá kontejnery podle štítku `app.instance`;
- vlastně se registruje a hlásí se na `POST /api/nodes/register`; hlavní aplikace upsertuje `CtraderCliNode`
  **podle jména**, takže uzel přežije změny IP.

## Důsledky

- Uniknutý token požadavku vyprší za minuty; není žádné stálé přihlašovací údaje shell, které by se mohly ukrást.
- Schopnost agenta je omezena na "spuštění povoleného image" — nemůže být přeměněna na obecný
  vzdálený shell.
- Identita uzlu je založena na jménu, takže opětovné zřízení uzlu s novou IP neztratí jeho historii.
