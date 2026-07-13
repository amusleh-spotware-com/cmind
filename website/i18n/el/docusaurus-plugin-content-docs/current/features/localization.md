# Localization (i18n)

Το cMind είναι πλήρως localizable και έρχεται στις **ίδιες 23 γλώσσες που υποστηρίζει το cTrader**, ώστε ένας trader
χρησιμοποιεί την πλατφόρμα — και διαβάζει αυτά τα docs — στη δική του γλώσσα. English είναι το fallback; οποιαδήποτε missing
translation υποβαθμίζεται gracefully σε English αντί να εμφανίζει blank ή raw key.

## Υποστηριζόμενες γλώσσες

Arabic (RTL), Chinese (Simplified), Czech, English, French, German, Greek, Hungarian, Indonesian,
Italian, Japanese, Korean, Malay, Polish, Portuguese (Brazil), Russian, Serbian, Slovak, Slovenian,
Spanish, Thai, Turkish, Vietnamese.

Η μία source of truth είναι `Core.Constants.SupportedCultures` — το request-culture middleware, το
language switcher, το resource-parity test, και το no-hardcoded-string gate όλα διαβάζουν από αυτό. Προσθήκη
γλώσσας είναι μία one-line change εκεί plus τα resource files του.

## Πώς λειτουργεί (Blazor Server)

- **Resources.** UI strings ζουν σε `src/Web/Resources/Ui.resx` (English base) plus ένα
  `Ui.<culture>.resx` ανά γλώσσα. Τα Components διαβάζουν τα μέσω `IStringLocalizer<Ui>` — `@L["key"]`,
  ποτέ literal. Τα `.resx` files παράγονται από `tools/i18n/ui-translations.json`
  (`pwsh tools/i18n/gen-resx.ps1`), το translator-friendly source of truth.
- **Culture resolution.** `RequestLocalizationMiddleware` επιλέγει το culture από το `.AspNetCore.Culture`
  cookie πρώτα, τότε το browser's `Accept-Language`, τότε English.
- **Switching.** Το app-bar language switcher (και το **Settings → Language** section) κάνει navigate στο
  `GET /set-culture` endpoint — ένα full-reload έξω από το Blazor circuit, επειδή ένα circuit δεν μπορεί
  να αλλάξει culture live. Γράφει το cookie και, για ένα signed-in user, persists την επιλογή στο profile τους (`UserProfile.Locale`); το reload ξεκινά ένα fresh circuit στην επιλεγμένη γλώσσα.
- **Persistence & login.** Το saved profile locale γράφεται πίσω στο culture cookie κατά sign-in,
  ώστε ένας user να προσγειώνεται στη γλώσσα τους σε κάθε device.
- **Right-to-left.** Arabic (και οποιαδήποτε future RTL language) θέτει `<html dir="rtl">` και τυλίγει το layout σε
  MudBlazor's `MudRTLProvider`, κατοπτρίζοντας ολόκληρο το shell.
- **ICU.** Το Web host τρέχει με ICU enabled (`InvariantGlobalization=false`); wire/parse code παραμένει σε
  `CultureInfo.InvariantCulture`, ώστε μόνο per-culture UI formatting επηρεάζεται — ποτέ backtest ή CSV.

## Το gate — χωρίς hard-coded UI text

Νέα user-facing strings **δεν μπορούν** να merged un-localized σε covered scope:

- Ένα build-failing arch-guard test (`NoHardcodedUiTextTests`) σαρώνει migrated `.razor` files και fails σε
  οποιοδήποτε literal, text-bearing attribute (`Label`, `Text`, `Title`, `Placeholder`, `HelperText`,
  `aria-label`, `alt`) που δεν είναι ένα `@L["…"]` lookup.
- Ένο resource-parity test (`ResourceParityTests`) fails το build αν κάποια γλώσσα λείπει key ή έχει
  blank value — κάθε γλώσσα πάντα έχει κάθε key.

## Προσθήκη ή αλλαγή string

1. Προσθέστε/edit το key στο `tools/i18n/ui-translations.json` για **κάθε** culture.
2. Δημιουργήστε ξανά τα `.resx`: `pwsh tools/i18n/gen-resx.ps1`.
3. Αναφερθείτε σε αυτό στο component με `@L["your.key"]`.
4. `dotnet test` — το parity και hardcoded-text gates σας κρατούν ειλικρινή.

## Docs localization

Αυτά τα docs είναι επίσης localized. Το Docusaurus i18n ρυθμίζεται για όλες τις 23 locales (`website/i18n/`), με
ένα locale dropdown στο navbar και RTL για Arabic. Scaffold ένα locale's translation files με
`npm run write-translations -- --locale <code>` και translate κάτω από `website/i18n/<code>/`. Per τη
localization mandate, **προσθήκη ή αλλαγή οποιουδήποτε doc σημαίνει ενημέρωση κάθε locale στην ίδια αλλαγή.**
