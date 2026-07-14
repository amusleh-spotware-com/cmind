---
slug: /intro
title: Vitajte v cMind
description: Priateľský úvod do cMind — otvorenej, samostatne hostovateľnej platformy pre obchodné operácie na cTrader.
sidebar_position: 1
---

# Vitajte v cMind 👋

:::warning[Alfa softvér — nie je pripravený na produkčné nasadenie]
cMind je vo aktívnom vývoji. Očakávajte hrubé hrany, prelomové zmeny medzi verziami a funkcie
stále vo vývoji. **Potrebujeme komunitných testerov, hlásičov chýb a prvých prispievateľov**, ktorí
nám pomôžu ho formovať. Ak narazíte na problém,
[nahláste ho](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) —
vaša reálna spätná väzba je to najcennejšie, čo teraz môžete prispieť.
:::

Takže chcete stavať obchodné boty, backtestovať ich bez toho, aby ste roztavili notebook, spúšťať ich na
niekoľkých strojoch, zrkadliť obchody na tucet účtov a nechať AI strážiť riziko, kým spíte. **Ste presne
na správnom mieste.**

cMind je **otvorená, samostatne hostovateľná platforma pre obchodné operácie na cTrader**. Predstavte si
ju ako celý svoj obchodný stôl — tvorbu, vykonávanie, výpočtovú flotilu, kopírovanie obchodov a jadro AI
— zabalené do pokojnej, tmavej, mobilne prívetivej aplikácie, ktorú vlastníte od začiatku do konca.

:::tip[V jednej vete]
Zostavte → backtestujte → spustite → kopírujte svoje stratégie cTrader vo veľkom, so zabudovanou AI, na
vlastných serveroch a pod vlastnou značkou.
:::

## Čo vlastne dokáže?

| Chcete… | cMind to urobí | Viac informácií |
|---|---|---|
| Napísať cBot v prehliadači | IDE Monaco + šablóny C#/Python, zostavenia v sandboxe | [Zostavenie a backtest](./features/build-and-backtest.md) |
| Backtestovať naprieč strojmi | Samoopravná flotila uzlov vyberie najmenej vyťažený stroj | [Škálovanie](./deployment/scaling.md) |
| Kopírovať jeden účet na mnoho | Robustné zrkadlenie s resynchronizáciou, bez zdvojených obchodov | [Kopírovanie obchodov](./features/copy-trading.md) |
| Nechať AI robiť drinu | Generovanie stratégií, samooprava, strážca rizika, rozbory | [Jadro AI](./features/ai.md) |
| Držať sa pravidiel prop firmy | Sledovanie equity v reálnom čase + simulácia pravidiel challenge | [Prop firma](./features/prop-firm.md) |
| Overiť výhodu backtestu | PSR / DSR / t-štatistická korekcia pretrénovania | [Laboratórium integrity backtestu](./features/backtest-integrity.md) |
| Pochopiť vlastné návyky | Detekcia behaviourálnych únikov + AI kouč | [Obchodný denník](./features/trading-journal.md) |
| Sledovať makro udalosti pre stratégiu | Bod-v-čase kalendár, blokácia správ, API pre cBot | [Ekonomický kalendár](./features/economic-calendar.md) |
| Hodnotiť makro silu mien | Výhľad AI pre všetky páry | [Sila meny](./features/currency-strength.md) |
| Zabezpečiť účty pomocou 2FA | Overovacia aplikácia TOTP + záložné kódy | [Dvojfaktorové overenie](./features/two-factor-auth.md) |
| Nechať majiteľov ladiť za chodu | Každá možnosť white-label živá v Nastavenia → Nasadenie | [Nastavenia majiteľa](./features/white-label-owner-settings.md) |
| Spustiť v akomkoľvek jazyku | 23 jazykov vrátane RTL — zostavenie zlyhá pri chýbajúcom kľúči | [Lokalizácia](./features/localization.md) |
| Vydať to ako *váš* produkt | Kompletný white-label: názov, farby, logo, favicon | [White-label](./features/white-label.md) |
| Prevádzkovať to na telefóne | Inštalovateľná, mobilne orientovaná PWA | [PWA](./features/pwa.md) |
| Riadiť to z AI klienta | Zabudovaný MCP server (HTTP + SSE) | [MCP](./features/mcp.md) |

## Cesta za 5 minút ⏱️

Ak máte Docker a päť minút, môžete si práve teraz siahnuť na skutočnú inštanciu cMind:

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
cp .env.example .env        # set OWNER_EMAIL + OWNER_PASSWORD
docker compose up --build
```

Potom otvorte **<http://localhost:8080>**, prihláste sa a ste v obraze. Kompletný sprievodca (vrátane
riešenia problémov, keď bude mať Docker nevyhnutne svoje názory) nájdete v
**[Spustenie lokálne](./deployment/local.md)**.

## Nováčik? Nasledujte cestu zo žltých tehál 🟡

1. **[Pre koho to je?](./audience.md)** — uistite sa, že ste náš typ problému.
2. **[Spustenie lokálne](./deployment/local.md)** — rozbehnite skutočnú inštanciu.
3. **[Funkcie](./features/README.md)** — kompletná prehliadka toho, čo je vnútri.
4. **[Nasadenie naostro](./deployment/cloud.md)** — Docker, Kubernetes, Azure, AWS.
5. **[Spravte si to svojím](./white-label-for-business.md)** — nasaďte white-label pre svoju firmu.
6. **[Prispejte](./contributing.md)** — PR (ľudské *aj* s pomocou AI) sú veľmi vítané.

## Krátke slovo o peniazoch 💸

cMind hýbe **skutočným kapitálom**. Berieme to vážne — každá zmena sa dodáva s jednotkovými, integračnými
a end-to-end testami, vrátane chybových ciest (prerušené spojenia, odmietnuté príkazy, mŕtve uzly). Mali
by ste to brať vážne aj vy: **najprv testujte na demo účte** a prečítajte si
[poznámky ku compliance](./features/compliance.md), skôr než to namierite na čokoľvek reálne.
Obchodovanie je rizikové; tento softvér je nástroj, nie finančné poradenstvo.

Tak — dosť úvodov. Poďme niečo postaviť. →
