---
slug: /contributing
title: Prispevovanje
description: Kako prispevati k cMind — dobrodošle so človeške ali AI-asistentove PR. Prvo prispevanje v 10 minutah.
sidebar_position: 5
---

# Prispevovanje k cMind 🛠️

Hvala, ker si tu. cMind postane bolje vsakič, ko nekdo odpre问題, prijavi natančno cTrader vedenje, popravi pisemo v teh dokumentih ali pošlje PR. **Ne rabiš biti .NET čarovnik** — testeri, trgovci in popravci dokumentov so enako vredni kot ljudje, ki pišejo agregatoje.

:::tip Kanonični vodnik živi v repozitoriju
Ta stran je prijazna pot vstopa. Polni, vedno aktualni proces — osnovna pravila, dogovori o kodiranju, tok pregleda — je v **[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md)**.
:::

## Tvoje prvo prispevanje v ~10 minutah

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
dotnet restore
dotnet build          # 0 opozoril, ali bo CI ljubo odklonila
dotnet test           # enote + integracija + E2E
```

Našel si kaj za popravilo? Grani ga, spremeni, dodaj test in odpri PR. To je celoten cikel.

## Načini pomoči (ne vsi so koda)

| Prispevek | Napor | Kjer |
|---|---|---|
| 🐛 Prijavi reproducibilen hrošč | 10 min | [Poročilo o hrošču](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) |
| 💡 Predlagaj funkcionalnost | 10 min | [Zahteva za funkcionalnost](https://github.com/amusleh-spotware-com/cmind/issues/new?template=feature_request.yml) |
| 📖 Izboljšaj te dokumente | 15 min | Uredi pod `website/docs/` in PR |
| 🧪 Dodaj manjkajoči test | 30 min | `tests/UnitTests` · `IntegrationTests` · `E2ETests` |
| 🧠 Prijavi točno cTrader vedenje | 10 min | [Odpri razpravo](https://github.com/amusleh-spotware-com/cmind/discussions) |

## Hiške pravila (kratka verzija)

cMind premika **pravi denar**, zato je nekaj stvari nenegotljivo — in pošteno, to spravi kodo v radost za delo:

- **Stroga Domain-Driven Design.** Poslovna logika živi na agregatojih in vrednostnih objektih, nikoli v končnih točkah ali UI. (V repozitoriju je prijazna igračka zanj.)
- **Tri stopnje testiranja, vsaka sprememba.** Enota + integracija + E2E, *vključno* poti napak (padli spojčni, zavrnjeni orders, mrtvi vozlisčni). Zeleni testi so cena vstopa.
- **Nič opozoril.** `TreatWarningsAsErrors=true`. Moderni C# 14 idiomi.
- **Brez skrivnosti, brez magičnih nizov, nikoli `DateTime.UtcNow`** (injiciraj `TimeProvider` namesto).
- **Docs v istega commit.** Spremeni vedenje → posodobi dokumentacijo. Da, to vključuje to spletno mesto.

Polni podrobnosti, s *zakaj* za vsakim pravilom, v [CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md) in [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md).

## Prispevovanje z AI 🤖

Resnično pozdravljamo **AI-asistentove PR** — ta projekt je zgrajen za delo agentov kot tudi ljudi. Če vozi Claude, Copilot ali podobno: usmeritiv [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md), da prebere ugneždene `CLAUDE.md` datoteke in ga držiSameľudskihmeri (testi, nič opozoril, DDD). Dobra AI PR je nerazločljiva od dobre človeške PR — enaka pregled, enaka pozdravljanja.

## Budi izvrstna drug do drugega

Imamo [Kodeks vedenja](https://github.com/amusleh-spotware-com/cmind/blob/main/CODE_OF_CONDUCT.md).
Bistvo: budi ljubezniv, predpostavi dobro vest in ne pozabi da je oseba (ali agentinja osebe) na drugem koncu. Sprašuj kmalu — to je moč, ne neprijetnost.

Dobrodošel. Ne moremo se dočakati kaj boš zgradil. 🎉
