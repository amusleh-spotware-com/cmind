---
description: "Vezanje za vsak nov ali spremenjen kos UI-ja v tej aplikaciji (Blazor strani, dialogi, sestavine). To je vir resnice, na katerega se sklicuje CLAUDE.md. Če..."
---

# Smernice za oblikovanje UI — OBVEZNO

Vezanje za **vsak** nov ali spremenjen kos UI-ja v tej aplikaciji (Blazor strani, dialogi, sestavine).
To je vir resnice, na katerega se sklicuje `CLAUDE.md`. Če vas pravilo blokira, ustavite in vprašajte — ne
pošljite UI-ja, ki ga krši. Korenine v `plans/ui-overhaul.md`.

## 1. Mobilno prvo, vedno

- **Avtorski ustvarjeni za telefon 360–430px prvi**, nato izboljšajte navzgor z `min-width` medijske
  poizvedbe / MudBlazor razdelilne oprave. Nikoli namizje-prvo s `max-width` preglasami.
- **Ni vodoravnega drsenja pri kateri koli širini 320–1920px.** Če je vsebina širša od razporeda, je
  to napaka.
- Dotikni cilji ≥ **44px** (`var(--app-touch-target)`). Tekstovni vhodi ≥ 16px pisave (zaustaviti
  iOS zoom-na-fokus).
- Spoštujte zareze: uporabite `env(safe-area-inset-*)`; razporedi že nastavi `viewport-fit=cover`.
- Spoštujte `prefers-reduced-motion` — nobena bistvena informacija ni posredovana samo z animacijo.

## 2. Oblikovanje žetonov — nobenih trdo kodiranih vrednosti

- Vsa barva/radius/razmik prihaja iz **oblikovanja žetonov**: tema MudBlazor (`Web/Components/Theme.cs`) +
  lastnosti CSS po meri oddane z `Web/Branding/BrandingCss.cs` (`var(--app-primary)`,
  `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Nikoli trdo kodirajte barve heksadecimalke, radiusa ali niza blagovno znamko v komponenti ali
  CSS pravilo.** Preberite žeton. Žetoni tečejo iz belo označene `BrandingOptions`, zato mora
  palette ponovno prodajalca dosegniti vaš UI brezplačno.
- Nova vrednost, ki vpliva na branding → dodajte žeton + polje branding; ga ne vstavite vrsto.

## 3. Odzivna postavitev in podatki

- **Tabele se sesedejo v kartice na telefonih.** Vsaka `MudTable` nastavi `Breakpoint="Breakpoint.Sm"` in
  vsaka `MudTd` ima `DataLabel`. Ni neobdelanega široko tabele na mobilnem. (Predloga:
  `Components/Pages/Nodes.razor`.)
- Mreže: `MudItem xs="12" sm="6" md="4"` — polna širina na telefonu, večstolpčna navzgor.
- Oblike ena-stolpčna na mobilnem; veliki dotikni cilji; `inputmode`/`autocomplete` na vhodih;
  numerični/decimalni inputmode za denar/procent.
- Zagotovite **nalaganje, prazno in napako** stanja na vsaki listi/Detail — velikost za mobilno.
- Mobilna **spodnja navigacija** (`Components/Layout/BottomNav.razor`) je primarni telefonski nav;
  grupirani predalnik je polno meni. Dodajte visoko potnike tja; drži ga ≤5 predmetom.

## 4. Dialogi (ustvari/uredi)

- Vsa dejanja dodaj/ustvari/uredi/novo uporabijo **MudBlazor dialog** (`IDialogService.ShowAsync<TDialog>`),
  nikoli ročna stran oblike. Dialogi živijo v `Web/Components/Dialogs/`, razkrijejo `[Parameter]`s,
  vrnejo ugnezden `public sealed record …Result(...)`. Vrstica dejanja seznama (začetek/zaustavitev/brisanje)
  ostane vrsto kot gumbki ikon.
- Na telefonih bi morali biti dialogi **polni zaslon / polna širina** in zavedajo tipkovnice.

## 5. Vstavljena pomoč — vsak nadzor

- Vsaka neočitna možnost, izberi, stikalo ali dejanje dobi **`<HelpTip Text="…" />`**
  (`Components/HelpTip.razor`) — lebdi na namizju, **tapnite na mobilnem**. Izvor besedila iz `docs/`,
  tako da smernice ostanejo v sinhronizaciji s vedenjem; ažurirajte oba v istem potrdku.

## 6. Belo označena

- Ime izdelka, logotip, opis, podpora/podjetje, barve, favicon prihajajo vse iz `BrandingOptions`.
  Sklicujte se na njih (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), nikoli dobesedno
  "cMind" ali barvo blagovno znamko. Manifestacija PWA, ikone, tema-barva in prijava heroj so
  vsi belo označeni.

## 7. PWA

- Aplikacija je namestljiva. Obdržite končno točko manifestacije (`/manifest.webmanifest`) belo
  označeno, ikone prisotne (192/512/maskable + apple-touch), delavec za storitve samo-lupino (nikoli
  dotikanja Blazor vezja/`_framework`/hubs) in stran brez interneta deluje. Nova statična pot →
  obdržite `scope` manifestacije.
- Blazor Server potrebuje živo SignalR vezje → **namestljiva + lupina aplikacije**, ne polno
  brez interneta. Ne obljubljajte brez-internetne interaktivnosti.

## 8. Dostopnost

- Oznake na vhodih, `aria-*` na lastnih nadzorih, vidna oseba, logičan red osebe. Ker je tema belo
  označena, preverite **kontrast** pred aktivno temo, ne a fiksno paleto.

## 9. E2E — nobena UI se ne pošlje netestitrana (blokiranje)

Vsaka sprememba obrnjena na uporabnika pošlje Playwright E2E v `tests/E2ETests`, poganjana
kot pravi uporabnik, **na emulaciji mobilne naprave** plus namizje:

- Nova pot → dodajte jo v `PageSmokeTests` **in** `MobileLayoutTests` (prikazuje, spodnja nav,
  nobena napaka UI).
- Pretvorite tabelo/stran → dodajte svojo pot mobilnem **ne-preplavljanju** set.
- Nov tok → realistična mobilna pot (ustvari/uredi/shrani povratna pot) **in** nesrečna pot
  (neveljaven vnos, prazen seznam, dovoljenjem-zavrnjen na vlogo).
