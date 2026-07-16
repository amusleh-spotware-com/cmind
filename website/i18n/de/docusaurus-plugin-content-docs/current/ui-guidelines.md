---
description: "Bindend für alle neuen oder geänderten UI-Teile in dieser App (Blazor-Seiten, Dialoge, Komponenten). Dies ist die Quelle der Wahrheit, auf die CLAUDE.md verweist. Falls eine Regel dich blockt, stoppe und frage – verschiebe keine UI, die dagegen verstößt. Wurzeln in `plans/ui-overhaul.md`."
---

# UI-Designrichtlinien — BINDEND

Bindend für **alle** neuen oder geänderten UI-Teile in dieser App (Blazor-Seiten, Dialoge, Komponenten).
Dies ist die Quelle der Wahrheit, auf die `CLAUDE.md` verweist. Falls eine Regel dich blockt, stoppe und frage – verschiebe keine UI, die dagegen verstößt. Wurzeln in `plans/ui-overhaul.md`.

## 1. Mobile-first, immer

- **Für ein 360–430px Telefon zuerst entwerfen**, dann aufwärts mit `min-width` Media Queries / MudBlazor
  Breakpoint-Props erweitern. Nie Desktop-first mit `max-width` Overrides.
- **Kein horizontales Scrollen bei irgendeiner Breite 320–1920px.** Wenn Inhalte breiter sind als der Viewport, ist das ein Bug.
- Touch-Ziele ≥ **44px** (`var(--app-touch-target)`). Text-Eingaben ≥ 16px Schrift (verhindert iOS Zoom-on-Focus).
- Notches respektieren: `env(safe-area-inset-*)` verwenden; der Viewport setzt bereits `viewport-fit=cover`.
- `prefers-reduced-motion` beachten – keine essentiellen Informationen nur durch Animation vermittelt.

## 2. Design-Token – keine hartcodierten Werte

- Alle Farben/Radius/Abstände stammen aus **Design-Token**: MudBlazor-Theme (`Web/Components/Theme.cs`) +
  die CSS benutzerdefinierten Eigenschaften emittiert von `Web/Branding/BrandingCss.cs` (`var(--app-primary)`,
  `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Hartcodiere niemals eine Hex-Farbe, Radius oder Markenzeichenkette in einer Komponente oder CSS-Regel.** Lese einen Token.
  Token fließen von White-Label `BrandingOptions`, daher muss die Palette eines Wiederverkäufers deine UI kostenlos erreichen.
- Neuer markenbeeinflussender Wert → Token + Branding-Feld hinzufügen; nicht inline.

## 3. Responsives Layout & Daten

- **Tabellen kollabieren auf Telefonen zu Karten.** Alle `MudTable` setzen `Breakpoint="Breakpoint.Sm"` und alle
  `MudTd` haben einen `DataLabel`. Keine rohe breite Tabelle auf mobil. (Vorlage: `Components/Pages/Nodes.razor`.)
- Grids: `MudItem xs="12" sm="6" md="4"` – Vollbreite auf Telefon, mehrspaltig aufwärts.
- Formulare einspaltig auf mobil; große Tap-Ziele; `inputmode`/`autocomplete` auf Eingaben; numeric/decimal
  inputmode für Geld/Prozent.
- **Richtige Steuerelemente für strukturierte Eingaben – nie eine rohe Textbox für Zahlen oder Listen.** Sammle Zahlen,
  Geld, Prozentsätze, Daten, Enums und alle Multi-Wert-Daten mit dem richtigen Steuerelement (`MudNumericField`,
  `MudDatePicker`, `MudSelect`, eine editierbare add/remove Zeilenliste von typisierten Feldern oder eine Tabelle), jedes Feld
  einzeln validiert. Ein einzelnes freies Text-`MudTextField`, das der Benutzer mit Komma/Leerzeichen/Newline
  getrennten Blobs eingeben muss – die du dann parseest – ist **verboten**: Es ist fehleranfällig, unvalidiert und feindselig
  auf einem Telefon. **Niemand will einen Blob tippen.** Multi-Wert-Eingabe ist eine editierbare Liste typierter Reihen (add /
  remove), oder wird aus vorhandenen Domänendaten geladen (z.B. den Check direkt von einem abgeschlossenen Backtest laufen, anstatt seine Zahlen erneut einzugeben). Plain `MudTextField` ist nur für echten freien Text – Namen, Notizen,
  Suche, Beschreibungen.
- Bereitstellen von **Loading-, Empty- und Error**-Zuständen auf jeder Liste/Detail – dimensioniert für mobil.
- Die mobile **Bottom-Navigation** (`Components/Layout/BottomNav.razor`) ist die primäre Telefon-Nav; das
  gruppierte Drawer ist das vollständige Menü. Füge dort hochfrequente Ziele hinzu; halte es ≤5 Elemente.

## 4. Dialoge (create/edit)

- Alle add/create/edit/new Aktionen verwenden einen **MudBlazor Dialog** (`IDialogService.ShowAsync<TDialog>`), nie
  ein Inline-Seitenformular. Dialoge leben in `Web/Components/Dialogs/`, exponieren `[Parameter]`s, geben einen verschachtelten
  `public sealed record …Result(...)` zurück. Listenzeilenaktionen (start/stop/delete) bleiben inline als Icon-Buttons.
- Auf Telefonen sollten Dialoge **Vollbild / Vollbreite** und Tastatur-bewusst sein.

## 5. Inline-Hilfe – jedes Steuerelement

- Jede nicht-offensichtliche Option, Select, Switch oder Aktion bekommt einen **`<HelpTip Text="…" />`**
  (`Components/HelpTip.razor`) – Hover auf Desktop, **Tap auf mobil**. Beziehe den Text aus `docs/`, sodass
  die Anleitung mit dem Verhalten synchron bleibt; aktualisiere beide im gleichen Commit.

## 6. White-Label

- Produktname, Logo, Beschreibung, Support/Unternehmen, Farben, Favicon kommen alle aus `BrandingOptions`.
  Referenziere sie (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), nie literal "cMind" oder eine
  Markenfarbe. Das PWA-Manifest, Icons, Theme-Farbe und Login-Hero sind alle markengebunden.

## 7. PWA

- Die App ist installierbar. Halte den Manifest-Endpunkt (`/manifest.webmanifest`) markengebunden, Icons vorhanden
  (192/512/maskable + apple-touch), den Service Worker nur mit App-Shell (berühre niemals die Blazor
  Circuit/`_framework`/hubs), und die Offline-Seite funktioniert. Neue statische Route → halte Manifest `scope`.
- Blazor Server benötigt eine live SignalR Circuit → **installierbar + App-Shell**, nicht vollständig offline. Verspreche nicht offline Interaktivität.

## 8. Barrierefreiheit

- Labels auf Eingaben, `aria-*` auf benutzerdefinierten Steuerelementen, sichtbarer Fokus, logische Fokusreihenfolge. Da das Theme
  White-Label-bar ist, verifiziere **Kontrast** gegen das aktive Theme, nicht gegen eine feste Palette.

## 9. E2E – keine UI wird ungetezt versendet (blockierend)

Jede Benutzer-sichtbare Änderung versendet Playwright E2E in `tests/E2ETests`, getestet wie ein echter Benutzer, **auf mobiler
Geräte-Emulation** plus Desktop:

- Neue Route → füge sie zu `PageSmokeTests` **und** `MobileLayoutTests` hinzu (rendert, Bottom-Nav, kein Error-UI).
- Tabelle/Seite konvertieren → füge ihre Route zur mobilen **no-overflow** Menge hinzu.
- Neuer Flow → eine realistische Mobile-Journey (create/edit/save round-trip) **und** einen unhappy Path
  (ungültige Eingabe, leere Liste, Permission-denied pro Rolle).
- Neuer Hilfe-Tipp → assertiere, dass er auf Tap öffnet (`HelpTipTests` Muster).
- Verwende `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (Geräte-Emulation).
- `dotnet test` grün vor "Done". Emuliertes WebKit ≠ Mobiler Safari – echtes Geräte-Gating ist ein separater
  Release-Schritt.

## 10. Definition of Done (UI)

- [ ] Mobile-first; kein horizontales Overflow 320–1920px; Touch-Ziele ≥44px.
- [ ] Nur Design-Token – null hartcodierte Farben/Radii/Markenzeichenketten.
- [ ] Tabellen → Karten auf Telefon (`DataLabel` + `Breakpoint.Sm`); Loading/Empty/Error Zustände vorhanden.
- [ ] Strukturierte Eingabe verwendet richtige validierte Steuerelemente (numeric/date/select/editierbare Zeilenliste) – keine rohe
      Textbox, die der Benutzer mit Trennzeichen versehene Zahlen/Wert-Blobs eingeben muss.
- [ ] Create/Edit via Dialog; Vollbild auf mobil.
- [ ] Jedes Steuerelement hat einen `HelpTip` aus Docs.
- [ ] White-Label + PWA respektiert.
- [ ] Mobile + Desktop E2E hinzugefügt (Smoke, no-overflow, Journey, unhappy Path); `dotnet test` grün.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` sauber auf berührten Dateien.
