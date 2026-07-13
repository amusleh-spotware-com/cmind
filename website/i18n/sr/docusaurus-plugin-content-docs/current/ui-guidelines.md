---
description: "Обавезујуће за **сваки** нови или измењен комад UI у ова апликација (Blazor странице, дијалози, компоненте). Ово је извор истине референциран од CLAUDE.md. Ако а…"
---

# UI Дизајн Смерниће — ОБАВЕЗУЈУЋЕ

Обавезујуће за **сваки** нови или измењен комад UI у ова апликација (Blazor странице, дијалози, компоненте). Ово је извор истине референциран од `CLAUDE.md`. Ако правило те блокира, стани и питај — не доставља UI који то крши. Укорењена у `plans/ui-overhaul.md`.

## 1. Mobile-first, увек

- **Аутор за 360–430px телефон прво**, затим побољшати горе са `min-width` медиј упити / MudBlazor breakpoint пропс. Никад desktop-first са `max-width` преписује.
- **Без хоризонталног скрола на било коју ширину 320–1920px.** Ако је садржај шири од viewport, то је грешка.
- Допирни циљеви ≥ **44px** (`var(--app-touch-target)`). Текст уноси ≥ 16px фонт (зауставља iOS zoom-on-focus).
- Уважити заседе: користи `env(safe-area-inset-*)`; viewport већ постави `viewport-fit=cover`.
- Честитајте `prefers-reduced-motion` — нема суштинских инфо преносена само од анимације.

## 2. Дизајн токени — без hard-coded вредности

- Све боја/radius/spacing долазе од **дизајн токени**: MudBlazor тема (`Web/Components/Theme.cs`) + CSS custom својства емитована од `Web/Branding/BrandingCss.cs` (`var(--app-primary)`, `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Никад hard-code hex боја, радијус, или brand ниска у компоненти или CSS правило.** Читај токен. Токени ток од white-label `BrandingOptions`, тако reseller палета мора достићи твоја UI за слободно.
- Нови brand-affecting вредност → додајте токен + branding поље; немој га inline.

## 3. Одзивна расподела & подаци

- **Столови пропадају картама на телефонима.** Сваки `MudTable` постави `Breakpoint="Breakpoint.Sm"` и сваки `MudTd` има `DataLabel`. Без сирова широка табела на мобилну. (Шаблон: `Components/Pages/Nodes.razor`.)
- Гридови: `MudItem xs="12" sm="6" md="4"` — пуна-ширина на телефону, multi-column горе.
- Форме једна-колона на мобилну; велики допирни циљеви; `inputmode`/`autocomplete` на уносима; numeric/decimal inputmode за новца/проценти.
- Пружити **учитавање, празно, и грешка** стања на сваки листа/детаљ — величина за мобилну.
- Мобилна **дно навигација** (`Components/Layout/BottomNav.razor`) је примарна телефон nav; груписана фиока је пуна мени. Додајте high-traffic одредишта тамо; задржи то ≤5 предметима.

## 4. Дијалози (create/edit)

- Сви додај/направи/уреди/ново акције користе **MudBlazor дијалог** (`IDialogService.ShowAsync<TDialog>`), никад inline страна форма. Дијалози живе у `Web/Components/Dialogs/`, излажу `[Parameter]`s, враћа угнеждена `public sealed record …Result(...)`. Листа ред акције (почни/стани/избриши) остају inline као icon дугмади.
- На телефонима, дијалози би требало бити **пуна-екран / пуна-ширина** и keyboard-aware.

## 5. Inline помоћ — сваки контрола

- Свака non-obvious опција, избори, прекидач, или акција добија **`<HelpTip Text="…" />`** (`Components/HelpTip.razor`) — hover на desktop, **тап на мобилну**. Извор текст од `docs/` тако водство остаје у sync са понашањем; ажурирај обоје у исто комита.

## 6. White-label

- Производ име, logo, опис, подршка/компанија, боја, favicon сви долазе од `BrandingOptions`. Референцирај их (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), никад литерал "cMind" или brand боја. PWA манифест, иконе, theme-color, и логин хероја су сви branded.

## 7. PWA

- Апликација је инсталабилна. Задржи манифест крајњу тачку (`/manifest.webmanifest`) branded, иконе присутна (192/512/maskable + apple-touch), service радник app-shell-only (никад дотицања Blazor цирктуа/`_framework`/hubs), и offline страна радна. Нови статички маршрут → задржи манифест `scope`.
- Blazor Server требају live SignalR цирктуа → **инсталабилна + app-shell**, не пуна offline. Не обећавај offline интерактивност.

## 8. Приступачност

- Етикете на уносима, `aria-*` на прилагођена контрола, видљива фокус, логички фокус ред. Јер је тема white-labelable, верифику **контраст** против активна тема, не фиксна палета.

## 9. E2E — не UI доставља untested (блокирање)

Сваки user-facing промена доставља Playwright E2E у `tests/E2ETests`, вождена као прави корисник, **на мобилни device емулација** плус desktop:

- Нови маршрут → додајте то `PageSmokeTests` **и** `MobileLayoutTests` (чини, дно nav, не грешка UI).
- Конвертуј табела/страна → додајте њене маршрут мобилни **без-overflow** скуп.
- Нови ток → реалистичан мобилни путовање (create/edit/save round-trip) **и** unhappy путања (невалиди унос, празна листа, permission-denied per улога).
- Нови помоћ савет → потврди то отварање на тап (`HelpTipTests` шаблон).
- Користи `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (device емулација).
- `dotnet test` зелена пре "done". Емулирана WebKit ≠ мобилна Safari — real-device врата је одвојена издање корак.

## 10. Дефиниција од done (UI)

- [ ] Mobile-first; без хоризонталног overflow 320–1920px; допирни циљеви ≥44px.
- [ ] Само дизајн токени — нула hard-coded боја/радиј/brand нискама.
- [ ] Столови → картама на телефону (`DataLabel` + `Breakpoint.Sm`); учитавање/празно/грешка стања присутна.
- [ ] Create/edit via дијалог; пуна-екран на мобилну.
- [ ] Сваки контрола има `HelpTip` извор од док.
- [ ] White-label + PWA уважена.
- [ ] Мобилна + desktop E2E додана (smoke, без-overflow, путовање, unhappy путања); `dotnet test` зелена.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` чисто на додирни датотека.
