---
description: "cMind ist vollständig lokalisierbar und wird in denselben 23 Sprachen wie cTrader selbst ausgeliefert – damit ein Trader die Plattform und diese Docs in seiner eigenen Sprache nutzen kann."
---

# Lokalisierung (i18n)

cMind ist vollständig lokalisierbar und wird in den **gleichen 23 Sprachen wie cTrader selbst** ausgeliefert,
sodass ein Trader die Plattform – und diese Docs – in seiner eigenen Sprache nutzt. Englisch ist der
Fallback; jede fehlende Übersetzung degradiert gracefully zu Englisch, anstatt eine leere Seite oder
einen rohen Schlüssel anzuzeigen.

## Unterstützte Sprachen

Arabisch (RTL), Chinesisch (Simplifiziert), Tschechisch, Englisch, Französisch, Deutsch, Griechisch,
Ungarisch, Indonesisch, Italienisch, Japanisch, Koreanisch, Malaiisch, Polnisch, Portugiesisch
(Brasilien), Russisch, Serbisch, Slowakisch, Slowenisch, Spanisch, Thailändisch, Türkisch,
Vietnamesisch.

Die eine Quelle der Wahrheit ist `Core.Constants.SupportedCultures` — das Request-Culture-Middleware,
der Sprachumschalter, der Resource-Parity-Test und das No-Hardcoded-String-Gate lesen alle daraus.
Eine Sprache hinzuzufügen ist eine Ein-Zeilen-Änderung dort plus ihre Resource-Dateien.

## Wie es funktioniert (Blazor Server)

- **Resources.** UI-Strings leben in `src/Web/Resources/Ui.resx` (Englische Basis) plus einem
  `Ui.<culture>.resx` pro Sprache. Komponenten lesen sie über `IStringLocalizer<Ui>` — `@L["key"]`,
  niemals ein Literal. Die `.resx`-Dateien werden aus `tools/i18n/ui-translations.json` generiert
  (`pwsh tools/i18n/gen-resx.ps1`), der übersetzerfreundlichen Quelle der Wahrheit.
- **Culture-Auflösung.** `RequestLocalizationMiddleware` wählt die Culture aus dem
  `.AspNetCore.Culture`-Cookie zuerst, dann aus dem `Accept-Language` des Browsers, dann Englisch.
- **Umschalten.** Der App-Bar-Sprachumschalter (und der **Settings → Language**-Abschnitt)
  navigiert zum `GET /set-culture`-Endpunkt — ein vollständiger Reload außerhalb des Blazor-Circuit,
  weil ein Circuit die Culture nicht live ändern kann. Er schreibt das Cookie und, für einen
  eingeloggten Benutzer, die Wahl in sein Profil (`UserProfile.Locale`); der Reload bootet einen
  frischen Circuit in der gewählten Sprache.
- **Persistenz & Login.** Die gespeicherte Profil-Locale wird bei der Anmeldung zurück ins
  Culture-Cookie geschrieben, sodass ein Benutzer auf jedem Gerät in seiner Sprache landet.
- **Rechts-nach-links.** Arabisch (und jede zukünftige RTL-Sprache) setzt `<html dir="rtl">` und
  umschließt das Layout in MudBlazors `MudRTLProvider`, der gesamten Shell entsprechend.
- **ICU.** Der Web-Host läuft mit aktivierter ICU (`InvariantGlobalization=false`); Wire-/Parse-Code
  bleibt auf `CultureInfo.InvariantCulture`, sodass nur das per-Culture UI-Formatting betroffen ist —
  niemals ein Backtest oder CSV.

## Das Gate — Kein hartcodierter UI-Text

Neue benutzerorientierte Strings **können nicht** ohne Lokalisierung im Geltungsbereich gemergt werden:

- Ein Build-fehlender Arch-Guard-Test (`NoHardcodedUiTextTests`) scannt migrierte `.razor`-Dateien und
  scheitert an jedem literalen, texttragenden Attribut (`Label`, `Text`, `Title`, `Placeholder`,
  `HelperText`, `aria-label`, `alt`), das kein `@L["…"]`-Lookup ist.
- Ein Resource-Parity-Test (`ResourceParityTests`) lässt den Build scheitern, wenn eine Sprache einen
  Schlüssel vermisst oder einen leeren Wert liefert — jede Sprache hat immer jeden Schlüssel.

## Einen String hinzufügen oder ändern

1. Fügen Sie den Schlüssel in `tools/i18n/ui-translations.json` für **jede** Kultur hinzu/bearbeiten Sie ihn.
2. Regenerieren Sie die `.resx`: `pwsh tools/i18n/gen-resx.ps1`.
3. Referenzieren Sie ihn in der Komponente mit `@L["your.key"]`.
4. `dotnet test` — die Parity- und Hardcoded-Text-Gates halten Sie ehrlich.

## Docs-Lokalisierung

Diese Docs sind ebenfalls lokalisiert. Docusaurus i18n ist für alle 23 Locales konfiguriert
(`website/i18n/`), mit einem Locale-Dropdown in der Navbar und RTL für Arabisch. Scaffolden Sie die
Übersetzungsdateien eines Locales mit `npm run write-translations -- --locale <code>` und übersetzen
Sie unter `website/i18n/<code>/`. Gemäß dem Lokalisierungsmandat bedeutet **das Hinzufügen oder
Ändern eines Docs das Aktualisieren jedes Locales in derselben Änderung.**
