---
description: "Obbligo per ogni nuovo elemento dell'interfaccia utente o modificato in questa app (pagine Blazor, dialoghi, componenti). Questa è la fonte di verità a cui fa riferimento CLAUDE.md. Se una…"
---

# Linee guida per il design dell'interfaccia utente — OBBLIGATORIO

Obbligo per **ogni** nuovo elemento dell'interfaccia utente o modificato in questa app (pagine Blazor, dialoghi, componenti).
Questa è la fonte di verità a cui fa riferimento `CLAUDE.md`. Se una regola ti blocca, fermati e chiedi — non
inviare un'interfaccia che la viola. Radicate in `plans/ui-overhaul.md`.

## 1. Mobile-first, sempre

- **Progetta per un telefono di 360–430px prima**, poi migliora verso l'alto con media query `min-width` / proprietà
  di breakpoint MudBlazor. Non iniziare da desktop con override `max-width`.
- **Nessuno scroll orizzontale a nessuna larghezza tra 320–1920px.** Se il contenuto è più largo del viewport, è un bug.
- Target touch ≥ **44px** (`var(--app-touch-target)`). Input di testo ≥ 16px font (impedisce lo zoom su focus di iOS).
- Rispetta i notch: usa `env(safe-area-inset-*)`; il viewport ha già impostato `viewport-fit=cover`.
- Onora `prefers-reduced-motion` — nessuna informazione essenziale trasmessa solo dall'animazione.

## 2. Token di design — nessun valore hard-coded

- Tutti i colori/raggi/spaziature provengono da **token di design**: tema MudBlazor (`Web/Components/Theme.cs`) +
  le proprietà CSS personalizzate emesse da `Web/Branding/BrandingCss.cs` (`var(--app-primary)`,
  `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Non hard-codificare mai un colore hex, raggio o stringa di marca in un componente o regola CSS.** Leggi un token.
  I token fluiscono da `BrandingOptions` di white-label, quindi la tavolozza di un rivenditore deve raggiungere la tua interfaccia gratuitamente.
- Nuovo valore che influenza il brand → aggiungi un token + campo di branding; non includerlo inline.

## 3. Layout responsive e dati

- **Le tabelle si trasformano in schede su telefoni.** Ogni `MudTable` imposta `Breakpoint="Breakpoint.Sm"` e ogni
  `MudTd` ha un `DataLabel`. Nessuna tabella larga grezza su mobile. (Modello: `Components/Pages/Nodes.razor`.)
- Griglie: `MudItem xs="12" sm="6" md="4"` — larghezza intera su telefono, multi-colonna verso l'alto.
- Moduli a colonna singola su mobile; grandi target di tocco; `inputmode`/`autocomplete` su input; inputmode numerico/decimale
  per denaro/percentuale.
- **Controlli appropriati per input strutturato — mai una casella di testo grezza per numeri o elenchi.** Raccogli numeri,
  denaro, percentuali, date, enum e qualsiasi dato multi-valore con il controllo corretto (`MudNumericField`,
  `MudDatePicker`, `MudSelect`, un elenco di righe di campi tipizzati aggiungibili/rimovibili, o una tabella), ogni campo
  convalidato individualmente. Una singola `MudTextField` di testo libero in cui l'utente deve digitare un blob separato da virgola/spazio/newline
  — che poi analizzi — è **vietata**: è soggetta a errori, non convalidata e ostile
  su un telefono. **Nessuno vuole digitare un blob.** L'input multi-valore è un elenco modificabile di righe tipizzate (aggiungi /
  rimuovi), oppure è caricato dai dati del dominio esistenti (ad esempio, esegui il controllo direttamente da un backtest completato
  piuttosto che reinserire i suoi numeri). `MudTextField` semplice è solo per testo genuinamente libero — nomi, note,
  ricerca, descrizioni.
- Fornisci stati **caricamento, vuoto ed errore** su ogni elenco/dettaglio — dimensionato per mobile.
- La navigazione **inferiore su mobile** (`Components/Layout/BottomNav.razor`) è la navigazione primaria del telefono; il
  cassetto raggruppato è il menu completo. Aggiungi destinazioni ad alto traffico lì; mantenilo ≤5 elementi.

## 4. Dialoghi (creazione/modifica)

- Tutte le azioni di aggiunta/creazione/modifica/nuova usano un **dialogo MudBlazor** (`IDialogService.ShowAsync<TDialog>`), mai
  un modulo inline della pagina. I dialoghi vivono in `Web/Components/Dialogs/`, espongono `[Parameter]`, restituiscono un
  `public sealed record …Result(...)` annidato. Le azioni di riga dell'elenco (avvia/arresta/elimina) rimangono inline come pulsanti con icona.
- Su telefoni, i dialoghi dovrebbero essere **a schermo intero / larghezza intera** e consapevoli della tastiera.

## 5. Aiuto inline — ogni controllo

- Ogni opzione non ovvia, select, switch o azione riceve una **`<HelpTip Text="…" />`**
  (`Components/HelpTip.razor`) — hover su desktop, **tap su mobile**. Sourcia il testo da `docs/` in modo che
  la guida rimanga in sincro con il comportamento; aggiorna entrambi nello stesso commit.

## 6. White-label

- Nome del prodotto, logo, descrizione, supporto/azienda, colori, favicon provengono tutti da `BrandingOptions`.
  Fai riferimento a essi (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), mai "cMind" letterale o un
  colore di marca. Il manifesto PWA, le icone, il tema-colore e l'hero di login sono tutti branded.

## 7. PWA

- L'app è installabile. Mantieni l'endpoint del manifesto (`/manifest.webmanifest`) branded, le icone presenti
  (192/512/maskable + apple-touch), il service worker solo app-shell (mai toccando il circuito Blazor/`_framework`/hubs), e la
  pagina offline funzionante. Nuova rotta statica → mantenere `scope` del manifesto.
- Blazor Server ha bisogno di un circuito SignalR in tempo reale → **installabile + app-shell**, non completamente offline. Non
  promettere interattività offline.

## 8. Accessibilità

- Etichette su input, `aria-*` su controlli personalizzati, focus visibile, ordine di focus logico. Poiché il tema è
  personalizzabile in white-label, verifica il **contrasto** contro il tema attivo, non una tavolozza fissa.

## 9. E2E — nessuna interfaccia inviata senza test (bloccante)

Ogni modifica rivolta all'utente invia E2E Playwright in `tests/E2ETests`, guidata come un vero utente, **su emulazione di dispositivo mobile**
più desktop:

- Nuova rotta → aggiungila a `PageSmokeTests` **e** `MobileLayoutTests` (rendering, bottom nav, nessuna UI di errore).
- Converti una tabella/pagina → aggiungi la sua rotta al set **no-overflow** mobile.
- Nuovo flusso → un percorso mobile realistico (round-trip creazione/modifica/salvataggio) **e** un percorso infelice
  (input non valido, elenco vuoto, permesso negato per ruolo).
- Nuovo suggerimento di aiuto → assicura che si apra al tap (`HelpTipTests` pattern).
- Usa `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (emulazione dispositivo).
- `dotnet test` verde prima di "done". WebKit emulato ≠ mobile Safari — il gating del dispositivo reale è una fase di rilascio separata.

## 10. Definizione di completamento (UI)

- [ ] Mobile-first; nessuno overflow orizzontale 320–1920px; target touch ≥44px.
- [ ] Solo token di design — zero colori/raggi/stringhe di marca hard-coded.
- [ ] Tabelle → schede su telefono (`DataLabel` + `Breakpoint.Sm`); stati caricamento/vuoto/errore presenti.
- [ ] L'input strutturato usa controlli convalidati appropriati (numerico/data/select/elenco di righe modificabile) — nessuna casella di testo grezza
      in cui l'utente digita un blob di numero/valore delimitato.
- [ ] Creazione/modifica tramite dialogo; schermo intero su mobile.
- [ ] Ogni controllo ha un `HelpTip` sourciato da docs.
- [ ] White-label + PWA rispettati.
- [ ] E2E mobile + desktop aggiunto (smoke, no-overflow, journey, unhappy path); `dotnet test` verde.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` pulito su file toccati.
