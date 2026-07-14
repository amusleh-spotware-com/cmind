---
slug: /contributing
title: Prispievanie
description: Ako prispieť k cMind — human alebo AI-assisted PRs vitajú. Prvý príspevok za 10 minút.
sidebar_position: 5
---

# Prispievanie k cMind 🛠️

Ďakujeme vám za vašu prítomnosť. cMind sa zlepšuje zakaždým, keď niekto otvorí úkol, hlási presné
cTrader správanie, opravi preklepo v týchto dokumentoch, alebo dodá PR. **Nemusíte byť .NET čarodejník** — testeri, obchodníci a doc-fixeri sú rovnako cenní ako ľudia píšuci agregáty.

:::tip[Kanonický sprievodca žije v repo]
Táto stránka je priateľský on-ramp. Plný, vždy-aktuálny proces — základné pravidlá, coding
konvencie, review flow — je v **[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md)**.
:::

## Váš prvý príspevok za ~10 minút

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
dotnet restore
dotnet build          # 0 warnings, alebo CI vás milostivo odmietne
dotnet test           # unit + integration + E2E
```

Našli ste niečo na opravu? Vetvenka, zmena, test a otvorte PR. To je celá slučka.

## Spôsoby pomoci (nie všetko sú kód)

| Príspevok | Úsilie | Kde |
|---|---|---|
| 🐛 Nahlásiť reproducible bug | 10 min | [Bug report](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) |
| 💡 Navrhnúť funkciu | 10 min | [Feature request](https://github.com/amusleh-spotware-com/cmind/issues/new?template=feature_request.yml) |
| 📖 Vylepšiť tieto dokumenty | 15 min | Edit pod `website/docs/` a PR |
| 🧪 Pridať chýbajúci test | 30 min | `tests/UnitTests` · `IntegrationTests` · `E2ETests` |
| 🧠 Nahlásiť exact cTrader správanie | 10 min | [Otvoriť Discussion](https://github.com/amusleh-spotware-com/cmind/discussions) |

## Domáce pravidlá (krátka verzia)

cMind hýbe **skutočným peniazmi**, takže niekoľko vecí je nesporných — a čestne, robí codebase
radosťou na prácu:

- **Striktný Domain-Driven Design.** Obchodná logika žije na agregátoch a value objektoch, nikdy v
  endpointoch alebo UI. (Je tam priateľský playbook pre to v repo.)
- **Tri test tiers, každá zmena.** Unit + integration + E2E, *vrátane* failure paths (dropped
  connections, rejected orders, dead nodes). Green testy sú cena príchodu.
- **Nula varovaní.** `TreatWarningsAsErrors=true`. Moderný C# 14 idiomy.
- **Bez tajomstiev, bez magic stringov, nikdy `DateTime.UtcNow`** (injektujte `TimeProvider` namiesto).
- **Dokumenty v rovnakom commite.** Zmena správania → aktualizácia jej dokumentu. Áno, to obsahuje túto stránku.

Plný detail, s *prečo* za každým pravidlom, v
[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md) a
[AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md).

## Prispievanie s AI 🤖

Skutočne vitáme **AI-assisted PRs** — tento projekt je postavený na to, aby sa pracovalo na ňom agentmi ako aj
ľuďmi. Ak riadia Claude, Copilot alebo podobne: nasmerujte ho na
[AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md), nechajte ho čítať vnorené
`CLAUDE.md` súbory a držte ho na rovnakej úrovni (testy, nula varovaní, DDD). Dobrý AI PR je
nešetriacny od dobrého human PR — rovnaký review, rovnaký vítaní.

## Buďte vzorne k sebe

Máme [Code of Conduct](https://github.com/amusleh-spotware-com/cmind/blob/main/CODE_OF_CONDUCT.md).
Podstata: buď milý, predpokladaj dobrú vieru a pamätaj si, že na druhom konci je osoba (alebo osoba
agenta). Položte si otázky skoro — to je sila, nie problém.

Vitajte na palube. Nemôžeme sa dočkať, čo budete stavať. 🎉
