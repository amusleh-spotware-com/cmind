---
description: "Legame per ogni pezzo di UI nuovo o modificato in questa app (pagine Blazor, dialoghi, componenti). Questa è la fonte di verità a cui fa riferimento CLAUDE.md. Se una..."
---

# Linee guida sul design dell'interfaccia utente — OBBLIGATORIO

Legame per **ogni** elemento dell'interfaccia utente nuovo o modificato in questa app (pagine Blazor, dialoghi, componenti). Questa è la fonte di verità a cui fa riferimento `CLAUDE.md`. Se una regola ti blocca, fermati e chiedi — non spedire UI che la viola. Radicato in `plans/ui-overhaul.md`.

## 1. Mobile-first, sempre

- **Scrivi per un telefono 360–430px per primo**, quindi migliora verso l'alto con media query `min-width` / prop di breakpoint MudBlazor. Non iniziare mai da desktop-first con override `max-width`.
- **Nessuno scorrimento orizzontale a nessuna larghezza 320–1920px.** Se il contenuto è più largo del viewport, è un bug.
- Obiettivi tattili ≥ **44px** (`var(--app-touch-target)`). Input di testo ≥ 16px font (impedisce lo zoom su focus di iOS).
- Rispetta i notch: usa `env(safe-area-inset-*)`; il viewport già imposta `viewport-fit=cover`.
- Onora `prefers-reduced-motion` — nessuna informazione essenziale trasmessa solo da animazione.

## 2. Token di design — nessun valore hardcode

- Tutti i colori/raggio/spacing provengono da **token di design**: tema MudBlazor (`Web/Components/Theme.cs`) + le proprietà CSS personalizzate emesse da `Web/Branding/BrandingCss.cs` (`var(--app-primary)`, `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Non hardcode mai un colore hex, raggio o stringa di marchio in un componente o regola CSS.** Leggi un token. I token scorrono da `BrandingOptions` white-label, quindi la tavolozza di un rivenditore deve raggiungere la tua interfaccia utente gratuitamente.
- Nuovo valore che colpisce il marchio → aggiungi un token + campo di branding; non incorporarlo.

## 3. Layout reattivo e dati

- **Le tabelle crollano in schede su telefoni.** Ogni `MudTable` imposta `Breakpoint="Breakpoint.Sm"` e ogni `MudTd` ha una `DataLabel`. Nessuna tabella larga grezza su dispositivo mobile. (Template: `Components/Pages/Nodes.razor`.)
- Griglie: `MudItem xs="12" sm="6" md="4"` — full-width su telefono, multi-colonna verso l'alto.
- Moduli a singola colonna su dispositivo mobile; grandi obiettivi tattili; `inputmode`/`autocomplete` su input; inputmode numerico/decimale per denaro/percentuale.
- Fornisci stati di **loading, empty e error** su ogni elenco/dettaglio — dimensionati per dispositivo mobile.
- La **navigazione inferiore** mobile (`Components/Layout/BottomNav.razor`) è la navigazione telefonica primaria; il cassetto raggruppato è il menu completo. Aggiungi destinazioni ad alto traffico lì; mantienilo ≤5 elementi.

## 4. Dialoghi (crea/modifica)

- Tutte le azioni add/create/edit/new usano un **dialogo MudBlazor** (`IDialogService.ShowAsync<TDialog>`), non mai un modulo di pagina inline. I dialoghi risiedono in `Web/Components/Dialogs/`, espongono `[Parameter]`s, restituiscono un `public sealed record …Result(...)` annidato. Le azioni sulla riga dell'elenco (start/stop/delete) rimangono inline come pulsanti icona.
- Su telefoni, i dialoghi dovrebbero essere **a schermo intero / full-width** e consapevoli della tastiera.

## 5. Aiuto inline — ogni controllo

- Ogni opzione, select, switch o azione non ovvia riceve un **`<HelpTip Text="…" />`** (`Components/HelpTip.razor`) — al passaggio del mouse su desktop, **tocca su dispositivo mobile**. Provieni il testo da `docs/` in modo che la guida rimanga sincronizzata con il comportamento; aggiorna entrambi nello stesso commit.

## 6. White-label

- Il nome del prodotto, il logo, la descrizione, il supporto/azienda, i colori, la favicon tutti provengono da `BrandingOptions`. Fai riferimento ad essi (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), non letterale "cMind" o un colore di marchio. Il manifest PWA, le icone, il theme-color e l'hero di login sono tutti marchiati.

## 7. PWA

- L'app è installabile. Mantieni l'endpoint manifest (`/manifest.webmanifest`) marchiato, icone presenti (192/512/maskable + apple-touch), il service worker solo app-shell (non toccando mai il circuito Blazor/`_framework`/hubs), e la pagina offline funzionante. Nuovo percorso statico → mantieni il manifest `scope`.
- Blazor Server ha bisogno di un circuito SignalR attivo → **installabile + app-shell**, non offline completo. Non promettere interattività offline.

## 8. Accessibilità

- Etichette su input, `aria-*` su controlli personalizzati, focus visibile, ordine di focus logico. Poiché il tema è white-labelable, verifica il **contrasto** rispetto al tema attivo, non a una tavolozza fissa.

## 9. E2E — nessuna interfaccia utente spedita non testata (bloccante)

Ogni modifica rivolta all'utente spedisce Playwright E2E in `tests/E2ETests`, guidata come un vero utente, **su emulazione di dispositivo mobile** più desktop:

- Nuovo percorso → aggiungi a `PageSmokeTests` **e** `MobileLayoutTests` (renderizza, bottom nav, nessuna UI di errore).
- Converti una tabella/pagina → aggiungi il suo percorso al set **no-overflow** mobile.
- Nuovo flusso → un percorso mobile realistico (round-trip create/edit/save) **e** un percorso infelice (input non valido, elenco vuoto, permesso negato per ruolo).
- Nuovo suggerimento di aiuto → affermalo si apre al tocco (pattern `HelpTipTests`).
- Usa `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (emulazione del dispositivo).
- `dotnet test` verde prima di "done". WebKit emulato ≠ Mobile Safari — il gating di dispositivi reali è un passaggio di rilascio separato.

## 10. Definizione di done (UI)

- [ ] Mobile-first; nessun overflow orizzontale 320–1920px; obiettivi tattili ≥44px.
- [ ] Solo token di design — zero colori/radii/stringhe di marchio hardcoded.
- [ ] Tabelle → schede su telefono (`DataLabel` + `Breakpoint.Sm`); stati di loading/empty/error presenti.
- [ ] Crea/modifica tramite dialogo; schermo intero su dispositivo mobile.
- [ ] Ogni controllo ha un `HelpTip` proveniente dai documenti.
- [ ] White-label + PWA rispettati.
- [ ] E2E mobile + desktop aggiunto (smoke, no-overflow, journey, unhappy path); `dotnet test` verde.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` pulito su file toccati.
