---
slug: /contributing
title: Přispívání
description: Jak přispívat do cMind — PRs s pomocí člověka nebo AI jsou vítány. První příspěvek za 10 minut.
sidebar_position: 5
---

# Přispívání do cMind

Děkuji že jste zde. cMind se stane lepším pokaždé, když někdo otevře issue, nahlásí přesné chování cTrader, opraví překlep v těchto samých dokumentech, nebo zašle PR. **Nemusíte být .NET průvodce** — testeři, tradeři a opravovatelé dokumentů jsou stejně ceníni jako lidé psaní agregáty.

:::tip[Kanonická příručka žije v repozitáři]
Tato stránka je přátelský on-ramp. Plný, vždy-aktuální proces — základní pravidla, programovací konvence, review tok — je v **[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md)**.
:::

## Váš první příspěvek za ~10 minut

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
dotnet restore
dotnet build          # 0 upozornění, nebo CI vás laskavě odmítne
dotnet test           # unit + integration + E2E
```

Našli jste něco k opravě? Branch to, změňte to, přidejte test, a otevřete PR. To je celá loop.

## Způsoby jak pomoci (ne všechny z nich jsou kód)

| Příspěvek | Úsilí | Kde |
|---|---|---|
| Nahlásit reprodukovatelný bug | 10 min | [Bug report](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) |
| Navrhnout vlastnost | 10 min | [Feature request](https://github.com/amusleh-spotware-com/cmind/issues/new?template=feature_request.yml) |
| Zlepšit tyto dokumenty | 15 min | Upravit pod `website/docs/` a PR |
| Přidat chybějící test | 30 min | `tests/UnitTests` · `IntegrationTests` · `E2ETests` |
| Nahlásit přesné chování cTrader | 10 min | [Otevřít Discussion](https://github.com/amusleh-spotware-com/cmind/discussions) |

## Domácí pravidla (krátká verze)

cMind přesouvá **skutečné peníze**, takže pár věcí je non-negotiable — a upřímně, to dělá kódovou základnu radostí k práci:

- **Přísné Domain-Driven Design.** Business logika žije na agregátech a value objektech, nikdy v endpointech nebo UI. (V repozitáři je přátelský playbook pro to.)
- **Tři test úrovně, každou změnu.** Unit + integration + E2E, *včetně* failure paths (vyklád připojení, odmítnuté objednávky, mrtvé uzly). Zelené testy jsou cena vstupu.
- **Nula upozornění.** `TreatWarningsAsErrors=true`. Moderní C# 14 idiomy.
- **Žádné tajemství, žádné magic stringy, nikdy `DateTime.UtcNow`** (vložit `TimeProvider` místo toho).
- **Dokumenty ve stejném commitu.** Chování změny → aktualizace její dokumenty. Ano, to zahrnuje tuto stránku.

Plný detail, s *proč* za každým pravidlem, v [CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md) a [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md).

## Přispívání s AI

Skutečně vítáme **AI-assisted PRs** — tento projekt je postaven k práci na agentech stejně jako lidech. Pokud řídíte Claude, Copilot, nebo podobné: ukažte mu [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md), nechť si přečte vnořené `CLAUDE.md` soubory, a drž ho na stejné lišti (testy, nula upozornění, DDD). Dobrý AI PR je nerozeznatelný od dobrého lidského PR — stejný review, stejné vítaní.

## Buď excelentní vůči sobě navzájem

Máme [Kodex chování](https://github.com/amusleh-spotware-com/cmind/blob/main/CODE_OF_CONDUCT.md). Podstata: buď laskavý, předpokládej dobrou víru, a pamatuj si že na druhé straně je člověk (nebo agent člověka). Ptej se otázek brzy — to je síla, ne problém.

Vítejte na palubě. Nemůžeme se dočkat co postavíš.
