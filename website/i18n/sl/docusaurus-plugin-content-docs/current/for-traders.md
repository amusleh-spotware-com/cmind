---
slug: /for-traders
title: cMind za trgovce na cTraderju
description: Zakaj bi trgovec na cTraderju hostiral cMind — lastna infrastruktura in podatki, avtorstvo, testiranje, izvajanje in spremljanje cBotov v eni AI-omogočeni konzoli, na svojem prenosniku, VPS ali telefonu.
keywords:
  - cTrader
  - algoritmično trgovanje
  - samohostiran trgovalni sistem
  - testiranje cBotov
  - AI trgovalni boti
  - odprtokodna trgovalna programska oprema
sidebar_position: 5
---

# cMind za trgovce na cTraderju 📈

Že trgujete na cTraderju. Že se juglirate s urejevalnikom kode, testerjem, VPS in tremi
zavihki v brskalniku. **cMind vse to sesuje v eno temno, tipkovničko konzolo, ki jo tečete
sami** — in je odprte kode, zato informacije o vaši prednosti, strategijah in kredencialih
nikoli ne zapustijo vašega sistema.

:::tip[Povzetek]
Hostite cMind na prenosnem računalniku, poceni VPS ali domačem strežniku. Avtorski ustvarite, testirajte, tečite in spremljajte cBote na enem mestu z jedrom AI, ki dela opravila. → [Tečite ga v 5 minutah](./deployment/local.md)
:::

## Zakaj samohostirati namesto storitve v oblaku?

- **Lastna infrastruktura in podatki.** Vaši cBoti, kredenciali, žetoni in zgodovina lastnina živijo na
  **vaši** infrastrukturi — brez tretjega partnerja, brez zaklučanosti, brez "ukinili smo ta izdelek" e-pošte.
- **Res je vaše za spremembe.** C# 14 / .NET 10, stroga DDD, EF Core + PostgreSQL, strežnik MCP — vse
  odprte kode in spremenjivo. Forkirajte, razširite, pošljite PR.
- **Ni plačevanja za značilnosti.** Prinesite svoj ključ AI za katerikoli ponudnika; vsaka značilnost AI je vključena.

Raje ne želite tečati lastnih strežnikov? Gostitujoča podjetja lahko tečejo upravljan cMind za vas —
glejte [Za oblačne in VPS ponudnike](./for-cloud-providers.md).

## Ena konzola, brez juglarstva z zavihki

- **Avtorski ustvarite** v pravi IDE Monaco (urejevalniku VS Code), s predlogami C# **in** Python in
  песочnicama `dotnet build` v zahodnih kontejnerjih. → [Gradnja in testiranje](./features/build-and-backtest.md)
- **Testirajte** čez floto vozlišč in opazujte krivulje lastnine, ki se vrnevajo v živo.
- **Tečite** strategije v živo in jih **spremljajte** iz enega nadzorne plošče. → [Nadzorna plošča](./features/dashboard.md)
- **Kopirajte** glavni račun na mnoge račune čez broketerje in ID cTraderja, z usklajevano, ki
  preživi prekinjena povezave in rotirajočo žetone. → [Kopiranje trgovanja](./features/copy-trading.md)

## AI, ki dela opravila, ne pogovarjanja

Prinesite svoj ključ API (katero koli podprto ponudnike — oblačne ali lokalni model) in dobite angleščino → pravi
sestavljivi cBot z zanko samopopravljanja, nastavo parametrov, analizo testiranja in varnostno varditvijo,
ki lahko samodejno zaustavi neprimerno delujočega bota. → [Spoznajte jedro AI](./features/ai.md)

## Institucionalno gradnjo orodja, za enega

Ista natančnost, ki jo plača zadobo, na svojem polju:

- [Integrnost testiranja](./features/backtest-integrity.md) · [Dimenzioniranje položaja](./features/position-sizing.md)
- [Zdravje strategije](./features/strategy-health.md) · [Režimski laboratorij](./features/regime-lab.md)
- [Izvajanje TCA](./features/execution-tca.md) · [Dnevnik trgovanja](./features/trading-journal.md)
- [Atelje agenta](./features/agent-studio.md) · [Kontrarian pozicioniranje](./features/contrarian-positioning.md)

## Tečete tam, kjer ste vi

Začnite na prenosnem računalniku z `docker compose up`, stopnjujte na poceni VPS ali domač strežnik, ko
ste pripravljeni, in preverite svoje bote s telefona — cMind je namestljiva, mobilno prvo
[PWA](./features/pwa.md). → [Tečite ga lokalno](./deployment/local.md)

Ali želite, da vaš odjemalec AI to vodi? Obstaja vgrajeni [strežnik MCP](./features/mcp.md).

## Pomagajte, da bo boljše

cMind je odprta koda in MIT licencirana — vozovnik je oblikovan s skupnostjo:

- Prijavite težave in zahteve za značilnosti ter glasujte za tisto, ki je pomembna.
- Dodajte predloge cBotov, adapterje ponudnikov AI ali prevode uporabniškega vmesnika.
- Pošljite PRje — tri stopnje testiranja (enota + integracija + E2E) in stroga DDD ohranjata visoko prečko in
  [Vodnik za prispevanje](./contributing.md) vas vodi skozi.

Pripravljeni? → [Preberite uvod](./intro.md), nato [tečite ga lokalno](./deployment/local.md).
