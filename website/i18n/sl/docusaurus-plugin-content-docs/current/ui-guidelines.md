---
description: "Zavezujoče pravilo za vsak novi ali spremenjen UI v tej aplikaciji (Blazor strani, dialogi, komponente). To je referenčni vir, ki ga navaja CLAUDE.md. Če vam pravilo preprečuje nadaljevanje, se…"
---

# UI Design Guidelines — MANDATORY

Zavezujoče pravilo za **vsak** novi ali spremenjen UI v tej aplikaciji (Blazor strani, dialogi, komponente).
To je referenčni vir, ki ga navaja `CLAUDE.md`. Če vam pravilo preprečuje nadaljevanje, se ustavite in vprašajte — ne
pošljite UI, ki ga krši. Utemeljeno v `plans/ui-overhaul.md`.

## 1. Mobile-first, always

- **Naredite za telefon 360–430px najprej**, nato izboljšajte navzgor z `min-width` media queries / MudBlazor
  breakpoint svojstvi. Nikoli desktop-first s `max-width` preglasi.
- **Brez vodoravnega drsenja pri nobeni širini 320–1920px.** Če je vsebina širša od pogleda, je to napaka.
- Tarče dotika ≥ **44px** (`var(--app-touch-target)`). Besedilni vnosi ≥ 16px font (preprečuje iOS povečanje na fokus).
- Upoštevajte zareze: uporabite `env(safe-area-inset-*)`; pogled je že nastavljen `viewport-fit=cover`.
- Spoštujte `prefers-reduced-motion` — brez bistvenih informacij samo skozi animacijo.

## 2. Design tokens — no hard-coded values

- Vse barve/radii/razmiki izhajajo iz **design tokenov**: MudBlazor tema (`Web/Components/Theme.cs`) +
  CSS lastne lastnosti ki jih oddaja `Web/Branding/BrandingCss.cs` (`var(--app-primary)`,
  `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Nikoli ne vključujte heksadecimalne barve, radija ali znamkovnega niza v komponenti ali CSS pravilu.** Preberite token.
  Tokeni izhajajo iz white-label `BrandingOptions`, tako da mora biti paleta prodajalca dostopna vašemu UI brezplačno.
- Nova vrednost, ki vpliva na znamko → dodajte token + branding polje; ga ne vstavite znotraj.

## 3. Responsive layout & data

- **Tabele se na telefonih zrušijo v kartice.** Vsaka `MudTable` nastavi `Breakpoint="Breakpoint.Sm"` in vsak
  `MudTd` ima `DataLabel`. Brez surovega široke tabele na mobilnem. (Predloga: `Components/Pages/Nodes.razor`.)
- Mreže: `MudItem xs="12" sm="6" md="4"` — polna širina na telefonu, večstolpec navzgor.
- Oblike en stolpec na mobilnem; velike tarče dotika; `inputmode`/`autocomplete` na vnosih; numeric/decimal
  inputmode za denar/procente.
- **Pravilne kontrole za strukturirani vnos — nikoli surova besedilna polja za številke ali sezname.** Zbirajte številke,
  denar, odstotke, datume, enumeracije in vse večvrednostne podatke s pravim kontrolom (`MudNumericField`,
  `MudDatePicker`, `MudSelect`, spremenljivi seznam dodaj/odstrani vrsto ali tabela), vsako polje
  posebej preverjeno. Ena prosta besedilna `MudTextField`, ki jo mora uporabnik vtipkati z vejicami/presledki/novimi vrsticami
  ločeni blob — ki ga nato razčlenite — je **prepovedana**: je nagnjela k napakam, nepreverjena in sovražna
  na telefonu. **Nihče ne želi pisati bloba.** Večvrednostni vnos je spremenljiv seznam vrst (dodaj /
  odstrani), ali je naložen iz obstoječih domenskih podatkov (npr. takoj zaženite preverjanje na končanem backtests
  namesto da ponovno vnosite njegove številke). Navadna `MudTextField` je le za prosto besedilo — imena, opombe,
  iskanje, opisi.
- Zagotovite **nalaganje, prazno in napako** stanja na vsakem seznamu/detajlih — velikosti za mobilno.
- Mobilna **spodnja navigacija** (`Components/Layout/BottomNav.razor`) je primarni telefon nav; the
  združena fioka je celoten meni. Dodajte sem destinacije z visokim prometom; jih držite ≤5 postavk.

## 4. Dialogs (create/edit)

- Vsi add/create/edit/new ukrepi uporabljajo **MudBlazor dialog** (`IDialogService.ShowAsync<TDialog>`), nikoli
  inline stranski obrazec. Dialogi se nahajajo v `Web/Components/Dialogs/`, izpostavljajo `[Parameter]`s, vrnejo ugnezdeni
  `public sealed record …Result(...)`. Akcije reda seznama (start/stop/delete) ostanejo inline kot ikone gumbi.
- Na telefonih bi morali biti dialogi **celozaslonski / polne širine** in osveščeni tipkovnice.

## 5. Inline help — every control

- Vsaka neevidenta možnost, izbor, stikalo ali akcija dobi **`<HelpTip Text="…" />`**
  (`Components/HelpTip.razor`) — lebdi na namizju, **tap na mobilnem**. Besedilo nabavite iz `docs/` zato
  usmerjevanje ostane usklajeno z obnašanjem; posodobite oba v istem commitu.

## 6. White-label

- Ime izdelka, logotip, opis, podpora/podjetje, barve, favicon vse izhajajo iz `BrandingOptions`.
  Jih navedite (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), nikoli dobesednega "cMind" ali
  branding barve. PWA manifest, ikone, theme-color in login heroj so vsi branding.

## 7. PWA

- Aplikacija je namestljiva. Ohranjajte manifest endpoint (`/manifest.webmanifest`) branding, ikone prisotne
  (192/512/maskable + apple-touch), service worker app-shell-only (nikoli se ne dotika Blazor
  circuit/`_framework`/hubs), in offline stran deluje. Nova statična pot → ohrani manifest `scope`.
- Blazor Server potrebuje živo SignalR tokokrog → **namestljivo + app-shell**, ne polne offline. Ne obljubljajte
  offline interaktivnosti.

## 8. Accessibility

- Oznake na vnosih, `aria-*` na ustnih kontrolah, vidni fokus, logični vrstni red fokusa. Ker je tema
  bela-labelable, preverite **kontrast** glede na aktivno temo, ne fiksne palete.

## 9. E2E — no UI ships untested (blocking)

Vsaka sprememba obrnjena na uporabnika pošlje Playwright E2E v `tests/E2ETests`, vožena kot pravi uporabnik, **na mobilni
emulaciji naprav** plus namizju:

- Nova pot → dodajte jo k `PageSmokeTests` **in** `MobileLayoutTests` (izriše, spodnja nav, ni napake UI).
- Pretvori tabelo/stran → dodaj njeno pot v mobilni **no-overflow** niz.
- Novi tok → realistična mobilna pot (create/edit/save round-trip) **in** nesrečna pot
  (neveljaven vnos, prazen seznam, dovoljenje-zavrnjeno po vlogi).
- Nova nasveta → zatrdi, da se odpre na tap (`HelpTipTests` vzorec).
- Uporabite `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (emulacija naprave).
- `dotnet test` zeleno pred "zaključeno". Emulirani WebKit ≠ mobilni Safari — gating pravem naprave je ločen
  izdajni korak.

## 10. Definition of done (UI)

- [ ] Mobile-first; brez vodoravnega drsenja 320–1920px; tarče dotika ≥44px.
- [ ] Samo design tokeni — nič trdo kodiranih barv/radijev/branding nizov.
- [ ] Tabele → kartice na telefonu (`DataLabel` + `Breakpoint.Sm`); nalaganje/prazno/napako stanja prisotna.
- [ ] Strukturirani vnos uporablja pravilne preverjene kontrole (numeric/date/select/editable row list) — ne surova
      besedilna polja, ki jo vtipka uporabnik kot ločen niz številk/vrednosti.
- [ ] Create/edit prek dialoga; celozaslonski na mobilnem.
- [ ] Vsaka kontrola ima `HelpTip` nabavljen iz dokumentov.
- [ ] White-label + PWA spoštovan.
- [ ] Dodana mobilna + namizna E2E (dim, no-overflow, pot, nesrečna pot); `dotnet test` zeleno.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` čisto na dotaknjenih datotekah.
