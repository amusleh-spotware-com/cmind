---
description: "Обавеза за сваки нови или измењени део корисничког интерфејса у овој апликацији (Blazor странице, дијалози, компоненте). Ово је референтни извор на који се…"
---

# UI Смернице за дизајн — ОБАВЕЗА

Обавеза за **сваки** нови или измењени део корисничког интерфејса у овој апликацији (Blazor странице, дијалози, компоненте).
Ово је референтни извор на који се позива `CLAUDE.md`. Ако нека правила вас спречавају, zaustavite se и питајте — не достављајте
UI који је крши. Засновано на `plans/ui-overhaul.md`.

## 1. Мобилни приступ на првом месту

- **Развијајте за телефон од 360–430px прво**, а затим побољшавајте са `min-width` media упитима / MudBlazor
  svojstvima за прелом. Никада прво за desktop са `max-width` преписивањем.
- **Без хоризонталног клизања на било којојширини 320–1920px.** Ако је садржај шири од приказаног прозора, то је грешка.
- Циљне површине за додир ≥ **44px** (`var(--app-touch-target)`). Текстуални уноси ≥ 16px фонт (zaustavља iOS зумирање на фокус).
- Поштујте заседе: користите `env(safe-area-inset-*)`; приказни прозор већ postavlja `viewport-fit=cover`.
- Уважите `prefers-reduced-motion` — нема битних информација преносених само анимацијом.

## 2. Токени дизајна — без hard-codirane вредности

- Све боје / радијус / размаци долазе од **токена дизајна**: MudBlazor тема (`Web/Components/Theme.cs`) +
  CSS svojstava која emituje `Web/Branding/BrandingCss.cs` (`var(--app-primary)`,
  `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Никада не hard-kodirajte heksadecimalnu бoју, радијус или марку у компоненту или CSS правило.** Pročitajte токен.
  Токени теку из white-label `BrandingOptions`, па палета preprodavca мора доћи до вашег интерфејса бесплатно.
- Нова вредност која утиче на марку → додајте токен + поље за branding; немојте је исписати.

## 3. Одзивни распоред и подаци

- **Табеле се сажимају у картице на телефонима.** Свака `MudTable` postavlja `Breakpoint="Breakpoint.Sm"` и свака
  `MudTd` има `DataLabel`. Нема широке табеле на мобилном. (Шаблон: `Components/Pages/Nodes.razor`.)
- Мреже: `MudItem xs="12" sm="6" md="4"` — пуне ширине на телефону, више колона наниже.
- Обрасци са једном колоном на мобилном; велике циљне површине за додир; `inputmode`/`autocomplete` на unositima; numerički / decimalni
  inputmode за новац / проценат.
- **Одговарајуће контроле за структурирани унос — никада сирова текстуална кутија за бројеве или листе.** Прикупљајте бројеве,
  новац, проценте, датуме, enum вредности и све податке са више вредности одговарајућом контролом (`MudNumericField`,
  `MudDatePicker`, `MudSelect`, editable листу за додавање / уклањање реда типизираних поља, или табелу), свако поље
  појединачно валидирано. Једна слободна `MudTextField` коју корисник мора унети са запетом / размаком / новим редом
  раздељеним blob-ом — шта потом анализирате — је **забрањена**: то је склонo greškama, неваљдиривано и непријатно
  на телефону. **Никоме се не свиђа куцање у blob.** Унос са више вредности је editable листа типизираних редова (додај /
  уклони), или се учитава из постојећих domain-ских podataka (нпр. pokrenite проверу директно из завршеног backtest-а
  уместо да поново уносите његове бројеве). Обичан `MudTextField` је само за прави слободан tekst — имена, napomene,
  pretraga, opisi.
- Пружите **учитавање, prazne и greške** стања на свакој листи / detaljima — величина за мобилни.
- Мобилна **доња навигација** (`Components/Layout/BottomNav.razor`) је примарна телефонска навигација; grupisana
  развлачила је пуни мени. Додајте често-коришћена одредишта; чувајте ≤5 stavki.

## 4. Дијалози (прави/измена)

- Све akcije додавања / kreiranja / измене / nove користе **MudBlazor дијалог** (`IDialogService.ShowAsync<TDialog>`), никада
  исписан образац странице. Дијалози живе у `Web/Components/Dialogs/`, izlažu `[Parameter]`s, враћају угнеждену
  `public sealed record …Result(...)`. Akcije редова листе (počni / zaustavi / izbriši) остају исписане као икона дугмади.
- На телефонима, дијалози би требали бити **целоекрански / пуне ширине** и свесни tastature.

## 5. Уграђена помоћ — свака контрола

- Свака nejasna опција, izbor, switch, или акција добија **`<HelpTip Text="…" />`**
  (`Components/HelpTip.razor`) — lebdjenje на desktop-у, **додир на мобилном**. Извор tekst из `docs/` па
  водиче остају усklађени са ponašanjem; ажурирајте ба у istoj obavezi.

## 6. White-label

- Назив производа, logo, opis, podrška / kompanija, боје, favicon су све iz `BrandingOptions`.
  Referencujte ih (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), никада doslovno "cMind" или боје марке.
  PWA manifest, ikone, theme-color i prijava heroja су све markirani.

## 7. PWA

- Апликација је инсталабилна. Чувајте endpoint manifestа (`/manifest.webmanifest`) markiranu, ikone prisutne
  (192/512/maskable + apple-touch), service worker samo app-shell (nikada ne dodiče Blazor
  circuit/`_framework`/hubs), i offline stranicu koja radi. Nova statička ruta → čuvajte manifest `scope`.
- Blazor Server trebaju živ SignalR circuit → **instalabilan + app-shell**, ne potpuni offline. Nemojte obećavati offline interaktivnost.

## 8. Приступачност

- Oznake на unosima, `aria-*` na prilagođenim kontrolama, vidljiv fokus, logički redosled fokusa. Pošto je tema
  white-labelable, proverite **kontrast** prema aktivnoj temi, ne fiksnoj paleti.

## 9. E2E — нема UI која се достављају netestirana (блокира)

Свака promenjena korisniku иrelevantan koristi Playwright E2E у `tests/E2ETests`, voženja kao pravi korisnik, **na emulaciji mobilnog
uređaja** плус desktop:

- Nova ruta → dodajte je `PageSmokeTests` **i** `MobileLayoutTests` (čini, donja nav, nema greške UI-ja).
- Pretvori tabelu / stranicu → dodajte njenu rutu mоbilnom **no-overflow** skupu.
- Novi tok → realistično mobilno putovanje (kreiraj / izmeni / sačuvan krug) **i** nesrečna putanja
  (nevaljdan unos, prazna lista, dozvola-odbijena po ulozi).
- Nova savetnička pomoć → tvrdi da se otvara na dodir (`HelpTipTests` šablon).
- Koristite `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (emulacija uređaja).
- `dotnet test` zeleno pre nego što "završite". Emilovani WebKit ≠ mobilni Safari — gating pravog uređaja je poseban
  korak izdanja.

## 10. Дефиниција завршетка (UI)

- [ ] Мобилни прво; без хоризонталног прелаза 320–1920px; циљне површине за додир ≥44px.
- [ ] Само токени дизајна — нула hard-codirane боје / радијуса / марки stringova.
- [ ] Табеле → картице на телефону (`DataLabel` + `Breakpoint.Sm`); учитавање / prazne / greške стања присутна.
- [ ] Структурирани унос користи одговарајуће валидиране контроле (numerička / datum / izbor / editable red lista) — nema sirove
      текстуалне кутије коју корисник куца у раздељени број / вредност blob.
- [ ] Kreiraj / izmeni putem диjалога; целоекрански на мобилном.
- [ ] Свака контрола има `HelpTip` nabavljen iz dokaza.
- [ ] White-label + PWA poštovani.
- [ ] Мобилна + desktop E2E dodata (dima, no-overflow, путовање, nesrečna putanja); `dotnet test` zeleno.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` čiste na dodirnutim datotekama.
