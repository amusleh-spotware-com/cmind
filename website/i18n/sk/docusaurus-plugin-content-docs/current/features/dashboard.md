---
title: Dashboard
description: Dashboard cMind — live, mobile-first veliteľské centrum pre vaše cBot beží, backtesty, zdroje a cluster uzlov.
---

# Dashboard 📊

Prvá vec, ktorú vidíte, keď sa prihlásite, a čestne stránka, ktorú necháte otvorenú celý deň. Dohodka
stránka (`/`, `Components/Pages/Index.razor`) je **live, mobile-first veliteľské centrum** pre
podpísaní používateľa činnosť naprieč cBot beží, backtesty, zdroje a (pre admin) uzol
klaster. Obnovuje sa, vyzerá skvele na telefóne a nikdy vás neurobí stlačiť F5.

## Čo to ukazuje

Od vrchu po dno, priorita-objednaný na telefón (každý blok je úplne šírka stack položka na mobil, a
responsive grid na tablet/desktop):

1. **Záhlavie** — názov, live indikátor (pravý pulzujúci bod; statický pod `prefers-reduced-motion`),
   posledný čas aktualizácie a **period toggle** (`1H · 24H · 7D · 30D`), ktorý pohania KPI a graf.
2. **Hero KPI** — štyri jednoducho viditeľné karty, každá veľké číslo + inline SVG sparkline a (kde
   zmysluplný) a **delta vs predchádzajúce obdobie**:
   - **Aktívne teraz** — beží + backtesty aktuálne spúšťajú/bežia.
   - **Úspešnosť** — dokončené ÷ (dokončené + zlyhalo) počas obdobia; delta v percentuálnych bodoch.
   - **Dokončené** — ukončené beží/backtesty toto obdobie; delta vs predchádzajúce obdobie.
   - **Zlyhalo** — zlyhaní toto obdobie; delta (menej je lepšie, takže pokles ukazuje zelený).
3. **Activity chart** — ApexCharts oblasť timeline začal / dokončené / zlyhalo za čas kôš.
4. **Instance status ring** — donut z bežiaceho / backtestov / čakajú / dokončené / zlyhalo, celkovo v
   centre.
5. **Backtesty** — tri-tile snapshot (bežiace / dokončené / zlyhalo), kliknutie-cez na `/backtest`.
6. **Copy trading** — vaše profily copy-trading s live status bodov, počet destináci a **Live**
   odznak na bežiacich profiloch; kliknutie-cez na `/copy-trading`.
7. **AI agenti** — vaši persona-driven obchodní agenti so stavom spustenia (archetyp · status) a poslední-action
   čas; kliknutie-cez na `/agent-studio`.
8. **Live activity feed** — 20 najnovšie udalostí (najnovší najprv) so statusom-farbený bod a
   relatívny timestamp.
9. **Cluster health** (iba admini) — aktívne-vs-celk uzly a kapacita-in-use gauge.
10. **Resource dlaždice** — cBots, obchodné účty, cTrader ID, MCP klávesy (kliknutie-cez na ich stránky).

## Prispôsobte si svoj dashboard

Každý blok vyššie je **widget, ktorý ovládajúte**. Stlačte **Prispôsobiť** (top-right v záhlaví) aby sa
otvoril dialóg, kde **show/hide** akýkoľvek widget a **reorder** ich s up/down šípkami. **Reset do štandardne**
obnovuje katalóg objednávky. Vašou voľbou je **trvalá server-side per user**, takže nasleduje vás naprieč
prehliadače a zariadenia — nie len túto kartu.

- Feature-gated a admin-only widgety (Copy trading, AI agenti, Cluster health) sa objavujú iba v
  dialóg, keď vašu deployment/role ich môžu používať.
- Katalóg widget je jediný zdroj pravdy v `Core/Dashboard/DashboardWidgets.cs`; prezentácia
  (štítok + ikona + dostupnosť) žije v `Components/Dashboard/DashboardWidgetMeta.cs`.

## Ako zostáva živý

Stránka sa pýta `GET /api/dashboard/overview?period=<1h|24h|7d|30d>` každých 10 sekúnd a znova-vykresľuje
widgety na mieste — žiadne ručné opätovné zavedenie. Prechodné fetch zlyhanie je pohltené a opakované na ďalšom tiku;
slučka sa zastaví čisto pri dispose. Prvé zaťaženie ukazuje kostru; trvalé zlyhanie zobrazuje chybu
kartu s **Retry**; užívateľ bez dát vidí nula KPI a empty-state kopírovať.

## Backend

- `Endpoints/DashboardEndpoints.cs` mapy `/overview` (a udržiava staršiu skalárnu `/stats`). To je
  per-user a admin-gated cez `ICurrentUser`; hodiny pochádzajú z `TimeProvider`. Mapuje tiež
  `GET/PUT /api/dashboard/layout` — layout widget používateľa, načítaný na štart stránky a uložené z
  Prispôsobiť dialóg.
- **Layout persistence** je `UserDashboard` agregát (`Core/Dashboard/UserDashboard.cs`): jeden doska
  za užívateľa (jedinečný na `UserId`), vlastnenie objednaný zoznam nastavení widgetu (viditeľný + poradie) uložené ako
  `jsonb` stĺpec. Objednaný zoznam je iba kedy mutované cez `Apply` / `Reset`, ktorí validujú každý
  kľúč voči katálogu `DashboardWidgets` a udržiavať zbierku úplnú a de-deduplicated. Neznáme
  kľúče sú odoprevá s `DomainException` → `400`.
- `Endpoints/DashboardQuery.cs` stavy kompozitný `DashboardOverview` čítať model: celá-čas status
  snapshot (skupinované počty), windowed set inštancií materiálizované raz a zdroje/uzol počty.
  Inštancia status a terminálne timestampy žijú na TPH subtypes (nie stĺpce), takže riadky sa čítajú v pamäti
  cez zdieľané `InstanceEndpoints.GetStartedAt/GetStoppedAt` pomôcky. Event čas =
  `stopped ?? started ?? created`.
- `Endpoints/DashboardModels.cs` drží DTOs, periódu→(window, bucket-count) plán a
  `DashboardMath` — čisté, deterministické bucketing + KPI/delta matematika (bez I/O, `now` je proslúšené).

KPI delty porovnávajú aktuálny okno voči bezprostredne predchádzajúcom (dopyt prináša dvojitý
okno za toto). Je **žiadny live účet P&L feed** — platforma má iba equity na backtesty a
prop-firm sledovanie — takže dashboard je zámyselne *operačný* (aktivita, priepustnosť, úspešnosť),
nie makléř balance ticker.
