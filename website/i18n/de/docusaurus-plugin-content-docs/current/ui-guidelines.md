---
description: "Bindung für jeden neu oder geändert Stück UI in dieser App (Blazor Seiten, Dialoge, Komponenten). Dies ist die Quelle der Wahrheit verwiesen von CLAUDE.md. Wenn eine…"
---

# UI Design Richtlinien — VERBINDLICH

Bindung für **jeden** neu oder geändert Stück UI in dieser App (Blazor Seiten, Dialoge, Komponenten). Dies ist die Quelle der Wahrheit verwiesen von `CLAUDE.md`. Wenn ein Regel blockiert Sie, stoppen und fragen — nicht ausliefern UI die verletzt es. Verwurzelt in `plans/ui-overhaul.md`.

## 1. Mobile-zuerst, immer

- **Autor für ein 360–430px Telefon zuerst**, dann erhöhen aufwärts mit `min-width` Media Abfrage / MudBlazor Bruchpunkt Props. Nein Desktop-zuerst mit `max-width` Außerkraftsetzung.
- **Keine horizontale Scroll bei jeden Breite 320–1920px.** Wenn Inhalte breiter als Viewport ist, es ein Fehler.
- Berühren Ziele ≥ **44px** (`var(--app-touch-target)`). Text-Eingaben ≥ 16px Font (Stopps iOS Zoom-auf-Fokus).
- Respekt Kerben: verwenden Sie `env(safe-area-inset-*)`; Viewport bereits Sätze `viewport-fit=cover`.
- Ehre `prefers-reduced-motion` — nein essentiell Info vermittelt nur von Animation.

## 2. Design Token — nein Hart-Coded Werte

- Alles Farbe/Radius/Spacing kommt von **Design Token**: MudBlazor Thema (`Web/Components/Theme.cs`) + CSS Benutzer-Eigenschaften emittiert durch `Web/Branding/BrandingCss.cs` (`var(--app-primary)`, `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Nein Hart-Code ein Hex Farbe, Radius, oder Marken-Zeichenfolge in ein Komponente oder CSS Regel.** Lesen ein Token. Token fließen von Weiß-Label `BrandingOptions`, daher ein Reseller Palette muss erreichen Ihr UI für Frei.
- Neu Marken-beeinflussen Wert → Hinzufügung ein Token + Branding Feld; nicht Inline es.

## 3. Reaktiv Layout & Daten

- **Tabellen zusammenfallen zu Karten auf Telefone.** Jede `MudTable` setzt `Breakpoint="Breakpoint.Sm"` und jede `MudTd` hat ein `DataLabel`. Nein roh breit Tabelle auf Mobil. (Vorlage: `Components/Pages/Nodes.razor`.)
- Gitter: `MudItem xs="12" sm="6" md="4"` — Vollbreite auf Telefon, Multi-Spalte aufwärts.
- Formulare einzeln-Spalte auf Mobil; großartig Tippen Ziele; `inputmode`/`autocomplete` auf Eingaben; Zifferisch/Dezimal Inputmode zum Geld/Prozent.
- Bereitstellen **wird lädt, leer und Fehler** Zustände auf jeden Liste/Detail — Größe zum Mobil.
- Die Mobil **Unten Navigation** (`Components/Layout/BottomNav.razor`) ist die primär Telefon Nav; die Gruppiert Schublade ist die Vollständig Menü. Hinzufügung Hoch-Verkehr Zielzonen dort; halte es ≤5 Punkte.

## 4. Dialoge (Erstelle/Bearbeiten)

- Alle Hinzufügen/Erstelle/Bearbeiten/Neu Aktionen verwenden ein **MudBlazor Dialog** (`IDialogService.ShowAsync<TDialog>`), nein Inline Seite Form. Dialoge leben in `Web/Components/Dialogs/`, Verfügbar machen `[Parameter]`s, Rückkehr ein verschachtelt `public sealed record …Result(...)`. Liste Reihen Aktionen (Start/Stopp/Löschen) bleiben Inline wie Symbole Schaltfläche.
- Auf Telefone, Dialoge sollten **Vollbild / Vollbreite** und Tastatur-bewusst.

## 5. Inline Hilfe — jeden Kontrolle

- Jeden nicht-offensichtlich Option, Auswahl, Schalter, oder Aktion bekommt ein **`<HelpTip Text="…" />`** (`Components/HelpTip.razor`) — Schwebe auf Desktop, **Tippen auf Mobil**. Quelle die Text von `docs/` daher Führung bleib in Sync mit Verhalten; Update beide in gleich Festschrift.

## 6. Weiß-Label

- Produkt-Name, Logo, Beschreibung, Unterstützung/Unternehmen, Farben, Favicon alle kommt von `BrandingOptions`. Referenz sie (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), nein wörtlich "cMind" oder ein Marken Farbe. Die PWA Manifest, Symbole, Thema-Farbe, und Login Held alle Marke.

## 7. PWA

- Die App ist installierbar. Halten Sie die Manifest-Endpoint (`/manifest.webmanifest`) Marke, Symbole vorhanden (192/512/maskbar + Apple-Touch), der Service Worker App-Shell-nur (nein Berühren Blazor Schaltung/`_framework`/Hubs), und die Offline-Seite funktioniert. Neu statisch Route → halten Manifest `Anwendungsbereich`.
- Blazor Server braucht ein Live SignalR Schaltung → **Installierbar + App-Shell**, nicht Vollständig Offline. Nicht versprechen Offline Interaktivität.

## 8. Zugänglichkeit

- Etiketten auf Eingaben, `aria-*` auf Benutzer Kontrolle, sichtbar Fokus, Logik Fokus Reihenfolge. Weil Thema ist Weiß-Label-Fähig, überprüfen **Kontrast** gegen aktiv Thema, nicht ein behoben Palette.

## 9. E2E — kein UI Schiffe untestet (Blocking)

Jeden Benutzer-zugang Änderung Schiffe Playwright E2E in `tests/E2ETests`, getrieben wie ein echten Benutzer, **auf Mobil Gerät Emulation** plus Desktop:

- Neu Route → Hinzufügung es zu `PageSmokeTests` **und** `MobileLayoutTests` (rendert, Unten Nav, nein Fehler UI).
- Konvertieren ein Tabelle/Seite → Hinzufügung sein Route zu Mobil **nein-Überfluss** Set.
- Neu Fluss → ein Realistisch Mobil Reise (Erstelle/Bearbeiten/Speichern Rund-fahrt) **und** ein Unglücklich Pfad (ungültig Eingabe, leer Liste, Berechtigung-Verweigert pro Rolle).
- Neu Hilfe Tipp → behaupten es öffnet auf Tippen (`HelpTipTests` Muster).
- Verwenden Sie `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (Gerät Emulation).
- `dotnet test` Grün bevor "erledigt". Emuliert WebKit ≠ Mobil Safari — echten-Gerät Gating ist ein separat Freigabe Schritt.

## 10. Definition der Fertigstellung (UI)

- [ ] Mobil-zuerst; keine horizontale Überfluss 320–1920px; Berühren Ziele ≥44px.
- [ ] Nur Design Token — Null Hart-Code Farben/Radii/Marken-Zeichenfolgen.
- [ ] Tabellen → Karten auf Telefon (`DataLabel` + `Breakpoint.Sm`); Laden/Leer/Fehler Zustände anwesend.
- [ ] Erstelle/Bearbeiten via Dialog; Vollbild auf Mobil.
- [ ] Jeden Kontrolle hat ein `HelpTip` Quelle von Dokumente.
- [ ] Weiß-Label + PWA respektiert.
- [ ] Mobil + Desktop E2E hinzugefügt (Rauch, Nein-Überfluss, Reise, Unglücklich Pfad); `dotnet test` Grün.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` sauber auf berührten Dateien.
