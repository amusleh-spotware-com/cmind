---
title: Nadzorna plošča
description: Nadzorna plošča cMind — živi, mobilno prvi poveljni center za vaše teke cBota, testiranja, vire in grozd vozlišč.
---

# Nadzorna plošča 📊

Prva stvar, ki jo vidite, ko se prijavite, in iskreno, stran, ki jo boste pustili odprto ves dan.
Pristaniščna stran (`/`, `Components/Pages/Index.razor`) je **živa, mobilno prvo poveljni center**
za aktivnost vpisanega uporabnika čez teke cBota, testiranja, vire in (za administratorje) grozd
vozlišč. Samočisti se osvežuje, se sliši odlično na telefonu in vas nikoli ne prisili, da pritisnete
F5.

## Kaj prikazuje

Od vrha do dna, po prioriteti naročeni za telefon (vsak blok je predmet s polno širino na mobilnem,
odzivna mreža na tablici/namizju):

1. **Glava** — naslov, živa indikator (res pulzirajočo piko; statična pod `prefers-reduced-motion`),
   čas zadnje posodobitve in **stikalo za obdobje** (`1H · 24H · 7D · 30D`), ki poganja KPI in
   grafikon.
2. **Junak KPI** — štiri kartice s hitrim pogledom, vsaka velika številka + vstavljena SVG črtana
   čara in (kjer je smiselna) **delta glede na prejšnje obdobje**:
   - **Aktivna sedaj** — teki + testiranja, ki se trenutno začenjajo/tečejo.
   - **Stopnja uspeha** — zaključena ÷ (zaključena + neuspešna) v obdobju; delta v odstotnih točkah.
   - **Zaključena** — zaključeni teki/testiranja to obdobje; delta glede na prejšnje obdobje.
   - **Neuspešna** — napake to obdobje; delta (manj je bolje, zato pad kaže zeleno).
3. **Grafikon dejavnosti** — časovnica ApexCharts sprožena / zaključena / neuspešna na časovni
   skladišče.
4. **Prstan statusa instanci** — sladoled tečečih / testiranj / čakajočih / zaključenih / neuspešnih,
   skupno v sredini.
5. **Testiranja** — posnetka tri ploščic (tečeče / zaključena / neuspešna), klikni skozi na
   `/backtest`.
6. **Kopiranje trgovanja** — vaši profili za kopiranje trgovanja z živo piko statusa, štetjem
   ciljev in **živo** značko na tečečih profilih; klikni skozi na `/copy-trading`.
7. **Agenti AI** — vaši osebnostni trgovalni agenti s stanjem teka (arhitip · status) in časom
   zadnjega dejanja; klikni skozi na `/agent-studio`.
8. **Živi vir dejavnosti** — 20 najnovejših dogodkov (novejše prvo) s piko obarvano v status in
   relativnim časovnim žigom.
9. **Zdravje grozda** (samo administratorji) — aktivna versus skupna vozlišča in merilnik zmogljivosti
   v uporabi.
10. **Ploščice virov** — cBoti, trgovalni računi, ID cTraderja, ključi MCP (klikni skozi na njihove
    strani).

## Prilagodite svojo nadzorno ploščo

Vsak blok zgoraj je **gradnik, ki ga nadzirujete**. Pritisnite **Prilagodite** (zgoraj desno v
glavi), da odprete dialog, kjer **prikažete/skrijete** kateri koli gradnik in ga **preuredite** z
puščicami gor/dol. **Ponastavitev na privzeto** obnovi reda kataloga. Vaša izbira je **obstojana na
strežniku za uporabnika**, zato vas spremlja čez brskalnike in naprave — ne samo ta zavihek.

- Gradniki z vratarjem in samo za administratorje (Kopiranje trgovanja, Agenti AI, Zdravje grozda)
  se v dialogu pojavijo le, ko vaša implementacija/vloga jih lahko uporabi.
- Katalog gradnika je en sam vir resnice v `Core/Dashboard/DashboardWidgets.cs`; predstavitev
  (oznaka + ikona + razpoložljivost) živuje v `Components/Dashboard/DashboardWidgetMeta.cs`.

## Kako ostaja živa

Stran puli `GET /api/dashboard/overview?period=<1h|24h|7d|30d>` vsakih 10 sekund in ponovno
prikaže gradnike na mestu — brez ročnega ponovnega nalaganja. Prenesen odkaz prehodnega neuspeha
se prepusti in ponovno poskusi na naslednjem tiku; zanka se čisteje zaustavi pri zavrženju. Prvi
naklep prikazuje skelet; trajni odkaz neuspeha prikazuje kartico napake s **Poskusite ponovno**;
uporabnik brez podatkov vidi ničelne KPI in prazno stanje kopije.

## Ozadje

- `Endpoints/DashboardEndpoints.cs` preslika `/overview` (in drži starejšo skalarno `/stats`). Je
  na uporabnika in samo za administratorje prek `ICurrentUser`; ura prihaja iz `TimeProvider`. Tudi
  preslika `GET/PUT /api/dashboard/layout` — postavitev gradnika uporabnika, naložena ob zagonu
  strani in shranjena iz dialoga Prilagodite.
- **Obstojnost postavitve** je agregat `UserDashboard` (`Core/Dashboard/UserDashboard.cs`): ena
  plošča na uporabnika (edinstven na `UserId`), ki je lastna urejeni seznam nastavkov gradnika
  (vidna + red) shranjen kot stolpec `jsonb`. Urejeni seznam se nikoli mutira skozi `Apply` /
  `Reset`, ki preveri vsak ključ proti katalogu `DashboardWidgets` in drži zbor celovito in
  razpravilno. Neznani ključi so zavrnjeni s `DomainException` → `400`.
- `Endpoints/DashboardQuery.cs` gradi sestavljeni model čitanja `DashboardOverview`: skojevni
  posnetek statusa (grupirani števci), niz oken instanc, ki se materialo enkrat, in števci
  vira/vozlišča. Status instanci in časovni žigi terminala živijo na subtipih TPH (ne stolpcih),
  zato se vrstice berejo v pomnilniku prek skupnih pomočnikov `InstanceEndpoints.GetStartedAt/GetStoppedAt`.
  Čas dogodka = `stopped ?? started ?? created`.
- `Endpoints/DashboardModels.cs` drži DTO, obdobje→(okno, štetje vedra) načrt in `DashboardMath` —
  čist, determinističen veder + matematika KPI/delta (brez I/O, `now` je dano).

Delte KPI primerjajo trenutno okno glede na takoj prejšnje (poizvedba prenese dvojno okno za to).
Ni **brez živega računa P&L vira** — platforma ima le lastnino za testiranja in sledenje
lastnine — torej je nadzorna plošča namerno *operativna* (dejavnost, pretok, stopnja uspeha), ne
valutni tečaj borznega stanja.

## Oblikovanje in žetoni

Vsa barva prihaja iz žetonov oblikovanja (`var(--app-success|-warning|-error|-info|-primary|-text*)`),
zato belo označena paleta teče skozi brezplačno — vključno z grafikonom, katerega barve serije se
berejo iz razrešenih žetonov ob času izvajanja prek `window.appReadTokens` (SVG ne more direktno
koristiti spremenljivk CSS). Nikjer na nadzorni plošči ni trdo kodirane heksadecimalke. Poglejte
[../ui-guidelines.md](../ui-guidelines.md).

## Povezava "Poganja cMind"

Nadzorna plošča prikazuje majhno, okusno **"Poganja cMind"** povezavo, ki kaže na to spletno
mesto dokumentacije. Je **prikazana privzeto** — ponosni smo na projekt in pomaga drugim trgovcem,
da ga najdejo — vendar je to v celoti vaša odločitev. Ponovno prodajalci, ki tečejo v popolnoma
belo označeno instanco, prepnejo `App:Branding:ShowSiteLink` na `false` in povsem izgine. Poglejte
[Belo označena branding](./white-label.md#poganja-povezava).

## Preskusi

- **Slog enote** (`tests/IntegrationTests/DashboardMathTests.cs`) — veder, stopnja uspeha, delte
  prejšnjega obdobja, razčlenjevanje obdobja, prazno/meja (dogodek ob `now`, delimo-po-nič
  varnost).
- **Enota** (`tests/UnitTests/Dashboard/UserDashboardTests.cs`) — agregat `UserDashboard`: privzeti
  seme, uporabi red/vidnost, dodaj-izpuščeno, duplikat-propad, neznani ključ zavrnjenja, ponastavitev.
- **Integracija** (`tests/IntegrationTests/DashboardQueryTests.cs`, `DashboardLayoutTests.cs`) —
  model čitanja pred pravim Postgresom (status/KPI/dejavnost/viri, zdravje administratorskega
  vozlišča, pot praznega uporabnika), nove odseke testiranj/profili kopiranj/agenti in
  **povratno** (shrani postavitev po meri → ponovno naloži → red + vidnost obstojana).
- **E2E** (`tests/E2ETests/DashboardTests.cs`, `DashboardCustomizeTests.cs`) — namizje + mobilno:
  kartice KPI, grafikon, prstan in vir se prikazujejo; stikalo obdobja preslika aktivno obdobje
  in ponovno naložiti; KPI preskakuje v `/run`; **skrivanje gradnika obstoji čez ponovno nalaganje**,
  **Ponastavitev** ga prinese nazaj in dialog Prilagodite deluje na telefonu brez vodoravnega
  preplavljanja. `/` je tudi v `PageSmokeTests`, `MobileLayoutTests` (lupina + brez preplavljanja)
  in `MobileJourneyTests`.
