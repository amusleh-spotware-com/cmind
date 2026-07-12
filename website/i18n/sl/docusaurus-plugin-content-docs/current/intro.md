---
slug: /intro
title: Dobrodošli v cMind
description: Prijazen uvod v cMind — odprtokodno platformo za trgovalne operacije za cTrader, ki jo lahko gostite sami.
sidebar_position: 1
---

# Dobrodošli v cMind 👋

Torej želite graditi trgovalne bote, jih testirati za nazaj, ne da bi pri tem stopili prenosnik, jih
poganjati na več računalnikih, zrcaliti posle na ducat računov in pustiti, da UI pazi na tveganje,
medtem ko spite. **Ste točno na pravem mestu.**

cMind je **odprtokodna platforma za trgovalne operacije za cTrader, ki jo lahko gostite sami**.
Predstavljajte si jo kot celotno svojo trgovalno mizo — pisanje, izvajanje, računsko floto, kopiranje
trgovanja in jedro UI — vse zapakirano v mirno, temno, mobilnikom prijazno aplikacijo, ki jo imate v
lasti od začetka do konca.

:::tip V enem stavku
Gradite → testirajte za nazaj → poganjajte → kopirajte svoje strategije cTrader v velikem obsegu, z
vgrajenim UI, na svojih strežnikih in pod svojo znamko.
:::

## Kaj dejansko zmore?

| Želite… | cMind to naredi | Preberite več |
|---|---|---|
| Napisati cBot v brskalniku | IDE Monaco + predloge C#/Python, gradnje v peskovniku | [Gradnja in backtest](./features/build-and-backtest.md) |
| Testirati za nazaj med računalniki | Samopopravljalna flota vozlišč izbere najmanj zaposleno napravo | [Skaliranje](./deployment/scaling.md) |
| Kopirati en račun na mnoge | Robustno zrcaljenje z resinhronizacijo, brez podvojenih poslov | [Kopiranje trgovanja](./features/copy-trading.md) |
| Prepustiti UI zamudno delo | Generiranje strategij, samopopravilo, varuh tveganja, obdukcije | [Jedro UI](./features/ai.md) |
| Ostati znotraj pravil prop podjetja | Sledenje lastniškega kapitala v živo + simulacija pravil izziva | [Prop podjetje](./features/prop-firm.md) |
| Ga izdati kot *svoj* izdelek | Popolna bela znamka: ime, barve, logotip, favicon | [Bela znamka](./features/white-label.md) |
| Ga poganjati na telefonu | Namestljiva, mobilnikom prijazna PWA | [PWA](./features/pwa.md) |
| Ga upravljati iz UI odjemalca | Vgrajeni strežnik MCP (HTTP + SSE) | [MCP](./features/mcp.md) |

## Pot v 5 minutah ⏱️

Če imate Docker in pet minut, lahko že zdaj brskate po pravem primerku cMind:

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
cp .env.example .env        # set OWNER_EMAIL + OWNER_PASSWORD
docker compose up --build
```

Nato odprite **<http://localhost:8080>**, se prijavite in začnite. Celoten vodnik (z odpravljanjem
težav, ko bo imel Docker neizogibno svoje mnenje) je v **[Lokalno poganjanje](./deployment/local.md)**.

## Novi tukaj? Sledite rumeni tlakovani poti 🟡

1. **[Za koga je to?](./audience.md)** — prepričajte se, da ste naša vrsta težav.
2. **[Lokalno poganjanje](./deployment/local.md)** — zaženite pravi primerek.
3. **[Funkcije](./features/README.md)** — celotni ogled tega, kaj je notri.
4. **[Zares uvedite](./deployment/cloud.md)** — Docker, Kubernetes, Azure, AWS.
5. **[Naredite ga za svojega](./white-label-for-business.md)** — uporabite belo znamko za svoje podjetje.
6. **[Prispevajte](./contributing.md)** — PR-ji (človeški *in* s pomočjo UI) so zelo dobrodošli.

## Nekaj hitrih besed o denarju 💸

cMind premika **pravi kapital**. To jemljemo resno — vsaka sprememba se dostavi z enotnimi,
integracijskimi in celostnimi testi, vključno s potmi napak (prekinjene povezave, zavrnjena naročila,
mrtva vozlišča). Tudi vi bi to morali jemati resno: **najprej testirajte na demo računu** in preberite
[opombe o skladnosti](./features/compliance.md), preden ga usmerite na kar koli pravega. Trgovanje je
tvegano; ta programska oprema je orodje, ne finančni nasvet.

No — dovolj uvoda. Pojdimo nekaj zgraditi. →
