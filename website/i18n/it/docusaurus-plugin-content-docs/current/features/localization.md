---
description: "cMind è completamente localizzabile e spedisce nelle stesse 23 lingue che cTrader stesso supporta."
---

# Localizzazione (i18n)

cMind è completamente localizzabile e spedisce nelle **stesse 23 lingue che cTrader stesso supporta**, così
un trader usa la piattaforma — e legge questi docs — nella propria lingua. L'inglese è il fallback; qualsiasi
traduzione mancante degrada gracefully a inglese piuttosto che mostrare un blank o una raw key.

## Lingue supportate

Arabic (RTL), Chinese (Simplified), Czech, English, French, German, Greek, Hungarian, Indonesian,
Italian, Japanese, Korean, Malay, Polish, Portuguese (Brazil), Russian, Serbian, Slovak, Slovenian,
Spanish, Thai, Turkish, Vietnamese.

L'unica source of truth è `Core.Constants.SupportedCultures` — il request-culture middleware, il
language switcher, il resource-parity test, e il no-hardcoded-string gate leggono tutti da lì. Aggiungere una
lingua è un cambiamento one-line lì più i suoi file risorse.

## Come funziona (Blazor Server)

- **Resources.** Le stringhe UI vivono in `src/Web/Resources/Ui.resx` (base English) più uno
  `Ui.<culture>.resx` per lingua. I componenti li leggono tramite `IStringLocalizer<Ui>` — `@L["key"]`,
  mai un letterale. I file `.resx` sono generati da `tools/i18n/ui-translations.json`
  (`pwsh tools/i18n/gen-resx.ps1`), la source of truth translator-friendly.
- **Culture resolution.** `RequestLocalizationMiddleware` sceglie la culture dal `.AspNetCore.Culture`
  cookie prima, poi l'`Accept-Language` del browser, poi inglese.
- **Switching.** Il language switcher nell'app-bar (e la sezione **Settings → Language**) naviga a
  l'endpoint `GET /set-culture` — un full-reload fuori dal Blazor circuit, perché un circuit non può
  cambiare culture live. Scrive il cookie e, per un utente loggato, persiste la scelta nel loro
  profilo (`UserProfile.Locale`); il reload boot un fresh circuit nella lingua scelta.
- **Persistenza & login.** La locale del profilo salvato è riscritta nel culture cookie al sign-in,
  così un utente atterra nella sua lingua su ogni dispositivo.
- **Right-to-left.** Arabic (e qualsiasi futura lingua RTL) imposta `<html dir="rtl">` e avvolge il layout in
  `MudRTLProvider` di MudBlazor, speculando l'intero shell.
- **ICU.** L'host Web gira con ICU abilitato (`InvariantGlobalization=false`); il codice wire/parse resta su
  `CultureInfo.InvariantCulture`, così solo la formattazione UI per-culture è affected — mai un backtest o CSV.

## Il gate — no text UI hard-coded

Le nuove stringhe user-facing **non possono** essere mergiate non-localizzate nello scope coperto:

- Un arch-guard test che fa failare il build (`NoHardcodedUiTextTests`) scansiona i file `.razor` migrated
  e fallisce su qualsiasi letterale, text-bearing attribute (`Label`, `Text`, `Title`, `Placeholder`,
  `HelperText`, `aria-label`, `alt`) che non sia un lookup `@L["…"]`.
- Un resource-parity test (`ResourceParityTests`) fa failare il build se qualsiasi lingua manca una key o
  spedisce un valore blank — ogni lingua ha sempre ogni key.

## Aggiungere o cambiare una stringa

1. Aggiungi/edita la key in `tools/i18n/ui-translations.json` per **ogni** cultura.
2. Rigenera i `.resx`: `pwsh tools/i18n/gen-resx.ps1`.
3. Referenziala nel componente con `@L["your.key"]`.
4. `dotnet test` — i gate parity e hardcoded-text ti tengono honest.

## Localizzazione docs

Questi docs sono localizzati anch'essi. Docusaurus i18n è configurato per tutte le 23 locale
(`website/i18n/`), con un locale dropdown nella navbar e RTL per Arabic. Scaffold i file di
traduzione di una locale con `npm run write-translations -- --locale <code>` e traduci sotto
`website/i18n/<code>/`. Per il mandate di localizzazione, **aggiungere o cambiare qualsiasi doc significa
aggiornare ogni locale nella stessa modifica.**
