---
slug: /intro
title: Vítejte v cMind
description: Přátelský úvod do cMind — otevřené, samostatně hostovatelné platformy pro obchodní operace na cTrader.
sidebar_position: 1
---

# Vítejte v cMind 👋

:::warning Alfa software — není připraven na produkci
cMind je v aktivním vývoji. Očekávejte hrubé hrany, nefungující změny mezi verzemi a funkce stále ve vývoji. **Potřebujeme komunitní testery, reportéry chyb a časné přispěvatele**, kteří pomohou ho formovat. Pokud narazíte na problém, [nahlaste ho](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) — vaše zpětná vazba z reálného světa je nejcennější věc, kterou můžete právě teď přispět.
:::

Takže chcete stavět obchodní boty, backtestovat je, aniž byste roztavili notebook, spouštět je na
několika strojích, zrcadlit obchody na tucet účtů a nechat AI hlídat riziko, zatímco spíte. **Jste
přesně na správném místě.**

cMind je **otevřená, samostatně hostovatelná platforma pro obchodní operace na cTrader**. Představte si
ji jako celý svůj obchodní stůl — tvorbu, provádění, výpočetní flotilu, kopírování obchodů a jádro AI —
zabalené do klidné, tmavé, mobilně přívětivé aplikace, kterou vlastníte od začátku do konce.

:::tip V jedné větě
Sestavte → backtestujte → spusťte → kopírujte své strategie cTrader ve velkém, se zabudovanou AI, na
vlastních serverech a pod vlastní značkou.
:::

## Co vlastně umí?

| Chcete… | cMind to udělá | Více informací |
|---|---|---|
| Napsat cBot v prohlížeči | IDE Monaco + šablony C#/Python, sestavení v sandboxu | [Sestavení a backtest](./features/build-and-backtest.md) |
| Backtestovat napříč stroji | Samoopravná flotila uzlů vybere nejméně vytížený stroj | [Škálování](./deployment/scaling.md) |
| Kopírovat jeden účet na mnoho | Robustní zrcadlení s resynchronizací, bez zdvojených obchodů | [Kopírování obchodů](./features/copy-trading.md) |
| Nechat AI dělat dřinu | Generování strategií, samooprava, hlídač rizika, rozbory | [Jádro AI](./features/ai.md) |
| Držet se pravidel prop firmy | Sledování equity v reálném čase + simulace pravidel challenge | [Prop firma](./features/prop-firm.md) |
| Ověřit výhodu backtestů | Korekce PSR / DSR / t-stat pro přeučení | [Laboratoř integrity backtestů](./features/backtest-integrity.md) |
| Porozumět vlastním zvyklostem | Detekce behaviorálních úniků + AI kouč | [Obchodní deník](./features/trading-journal.md) |
| Sledovat makro události pro strategii | Point-in-time kalendář, news blackout, cBot API | [Ekonomický kalendář](./features/economic-calendar.md) |
| Ohodnotit makro sílu měn | AI výhled na všechny páry | [Síla měn](./features/currency-strength.md) |
| Zabezpečit účty pomocí 2FA | Aplikace autentizátoru TOTP + záložní kódy | [Dvoufaktorové ověřování](./features/two-factor-auth.md) |
| Nechat vlastníky ladit za běhu | Každá white-label možnost živě v Nastavení → Nasazení | [Nastavení vlastníka](./features/white-label-owner-settings.md) |
| Provozovat v libovolném jazyce | 23 jazyků včetně RTL — build selže při chybějícím klíči | [Lokalizace](./features/localization.md) |
| Vydat to jako *váš* produkt | Kompletní white-label: název, barvy, logo, favicon | [White-label](./features/white-label.md) |
| Provozovat to na telefonu | Instalovatelná, mobilně orientovaná PWA | [PWA](./features/pwa.md) |
| Řídit to z AI klienta | Zabudovaný MCP server (HTTP + SSE) | [MCP](./features/mcp.md) |

## Cesta za 5 minut ⏱️

Pokud máte Docker a pět minut, můžete si právě teď sáhnout na skutečnou instanci cMind:

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
cp .env.example .env        # set OWNER_EMAIL + OWNER_PASSWORD
docker compose up --build
```

Poté otevřete **<http://localhost:8080>**, přihlaste se a jste v obraze. Kompletní průvodce (včetně
řešení problémů, až bude mít Docker nevyhnutelně své názory) najdete v
**[Spuštění lokálně](./deployment/local.md)**.

## Nováček? Následujte cestu ze žlutých cihel 🟡

1. **[Pro koho to je?](./audience.md)** — ujistěte se, že jste náš typ problému.
2. **[Spuštění lokálně](./deployment/local.md)** — rozjeďte skutečnou instanci.
3. **[Funkce](./features/README.md)** — kompletní prohlídka toho, co je uvnitř.
4. **[Nasazení naostro](./deployment/cloud.md)** — Docker, Kubernetes, Azure, AWS.
5. **[Udělejte si to svým](./white-label-for-business.md)** — nasaďte white-label pro svou firmu.
6. **[Přispějte](./contributing.md)** — PR (lidské *i* s pomocí AI) jsou velmi vítány.

## Krátké slovo o penězích 💸

cMind hýbe **skutečným kapitálem**. Bereme to vážně — každá změna se dodává s jednotkovými, integračními
a end-to-end testy, včetně chybových cest (přerušená spojení, odmítnuté příkazy, mrtvé uzly. Měli byste
to brát vážně také: **nejprve testujte na demo účtu** a přečtěte si
[poznámky ke compliance](./features/compliance.md), než to namíříte na cokoli reálného. Obchodování je
rizikové; tento software je nástroj, nikoli finanční poradenství.

Tak — dost úvodů. Pojďme něco postavit. →
