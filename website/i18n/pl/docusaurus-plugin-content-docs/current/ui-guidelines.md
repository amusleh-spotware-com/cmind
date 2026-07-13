---
description: "Wiązanie dla każdego nowego lub zmienionego kawałka interfejsu użytkownika w tej aplikacji (strony Blazor, dialogi, komponenty). To źródło prawdy przywoływane przez CLAUDE.md. Jeśli…"
---

# Wytyczne projektowania interfejsu użytkownika — OBOWIĄZKOWE

Wiązanie dla **każdego** nowego lub zmienionego kawałka interfejsu użytkownika w tej aplikacji (strony Blazor, dialogi, komponenty). To źródło prawdy przywoływane przez `CLAUDE.md`. Jeśli reguła cię blokuje, zatrzymaj się i zapytaj — nie wysyłaj interfejsu użytkownika, który ją narusza. Zakorzenione w `plans/ui-overhaul.md`.

## 1. Mobilny-pierwszy, zawsze

- **Autor dla telefonu 360–430px najpierw**, potem ulepsz w górę za pomocą zapytań multimediów `min-width` / MudBlazor props breakpoint. Nigdy desktop-pierwszy z przesłanięciami `max-width`.
- **Brak poziomego przewijania na dowolnej szerokości 320–1920px.** Jeśli zawartość jest szersza niż okienko, to jest błąd.
- Docelowe dotykowe ≥ **44px** (`var(--app-touch-target)`). Wejścia tekstowe ≥ 16px font (zatrzymuje zoom iOS-na-focus).
- Respektuj nacięcia: używaj `env(safe-area-inset-*)`; viewport już ustawia `viewport-fit=cover`.
- Honour `prefers-reduced-motion` — żadne istotne informacje przekazane tylko przez animację.

## 2. Tokeny projektowe — brak zakodowanych wartości

- Wszystkie kolory/promień/rozmieścić pochodzą z **tokenów projektowych**: temat MudBlazor (`Web/Components/Theme.cs`) + niestandardowe właściwości CSS emitowane przez `Web/Branding/BrandingCss.cs` (`var(--app-primary)`, `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Nigdy nie koduj koloru hex, promienia ani ciągu marki w komponencie lub regule CSS.** Przeczytaj token. Tokeny przepływają z white-label `BrandingOptions`, więc paleta odsprzedawcy musi osiągnąć twój interfejs użytkownika za darmo.
- Nowa wartość wpływająca na markę → dodaj token + pole branding; nie umieszczaj go.

## 3. Responsywny układ i dane

- **Tabele zapaść do kart na telefonach.** Każda `MudTable` ustawia `Breakpoint="Breakpoint.Sm"` i każda `MudTd` ma `DataLabel`. Brak surowej szerokiej tabeli na urządzeniach mobilnych. (Szablon: `Components/Pages/Nodes.razor`.)
- Siatki: `MudItem xs="12" sm="6" md="4"` — pełna szerokość na telefonie, wielokolumnowy w górę.
- Formularze jednocolumnowe na urządzeniach mobilnych; duże docelowe dotykowe; `inputmode`/`autocomplete` na wejściach; numeric/decimal inputmode dla pieniędzy/procent.
- Podaj **ładowanie, puste i błędne** stany na każdej liście/szczegóły — rozmiar dla mobilny.
- Mobilne **dno nawigacja** (`Components/Layout/BottomNav.razor`) jest głównym nawigacją telefonu; szuflada zgrupowana to pełne menu. Dodaj tam wysokoruchowe miejsca przeznaczenia; utrzymuj to ≤5 elementów.

## 4. Dialogi (utwórz/edytuj)

- Wszystkie akcje dodawania/tworzenia/edytowania/nowego używają **dialogu MudBlazor** (`IDialogService.ShowAsync<TDialog>`), nigdy wbudowanego formularza strony. Dialogi żyją w `Web/Components/Dialogs/`, wystawiają `[Parameter]s, zwróć zagnieżdżony `public sealed record …Result(...)`. Akcje wiersza listy (uruchomienie/zatrzymanie/usunięcie) pozostają wbudowane jako przyciski ikon.
- Na telefonach dialogi powinny być **pełnym ekranem / pełną szerokością** i świadome klawiatury.

## 5. Wbudowana pomoc — każdy formant

- Każda niejasna opcja, wybór, przełącznik lub akcja uzyskuje **`<HelpTip Text="…" />`** (`Components/HelpTip.razor`) — najedź na pulpit, **dotknij na urządzeniach mobilnych**. Źródło tekstu z `docs/`, aby wskazówka pozostała zsynchronizowana z zachowaniem; zaktualizuj oba w tym samym commit.

## 6. White-label

- Nazwa produktu, logo, opis, wsparcie/firma, kolory, favicon wszystko pochodzą z `BrandingOptions`. Odwołaj się do nich (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), nigdy dosłownie "cMind" ani kolor marki. Manifest PWA, ikony, temat-kolor i bohater logowania są wszyscy oznaczeni.

## 7. PWA

- Aplikacja jest instalowalna. Utrzymuj manifest endpoint (`/manifest.webmanifest`) oznaczony, ikony obecne (192/512/maskable + apple-touch), usługę pracownika powłoki aplikacji tylko (nigdy dotykającego obwodu Blazor/`_framework`/hubs) i pracujące stronie offline. Nowa statyczna trasa → utrzymaj manifest `scope`.
- Blazor Server potrzebuje obwodu SignalR live → **instalowalne + app-shell**, nie pełny offline. Nie obiecuj offline interaktywności.

## 8. Dostępność

- Etykiety na wejściach, `aria-*` na niestandardowych formantach, fokus widoczny, porządek logiczny fokusu. Ponieważ temat jest white-labelable, zweryfikuj **kontrast** wobec aktywnego motywu, nie ustalonej palety.

## 9. E2E — żaden interfejs użytkownika nie wysyła nieprzetestowany (blokowanie)

Każda zmiana widoczna dla użytkownika wysyła Playwright E2E w `tests/E2ETests`, napędzana jak rzeczywisty użytkownik, **na emulacji urządzenia mobilnego** plus pulpit:

- Nowa trasa → dodaj ją do `PageSmokeTests` **i** `MobileLayoutTests` (renderuje, dolne nav, brak błędu interfejsu użytkownika).
- Konwersja tabeli/strony → dodaj jej trasę do mobilnego zestawu **brak przepływu**.
- Nowy przepływ → realistyczna mobilna podróż (utwórz/edytuj/zapisz podróż w obie strony) **i** nieszczęśliwa ścieżka (nieprawidłowy wejść, pusta lista, permisja-odmowa na rolę).
- Nowa wskazówka pomocy → potwierdź, że otwiera się na dotknięcie (wzór `HelpTipTests`).
- Użyj `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (emulacja urządzenia).
- `dotnet test` zielony przed "zrobione". Emulowany WebKit ≠ mobilny Safari — brama rzeczywistego urządzenia jest oddzielnym krokiem wydania.

## 10. Definicja gotowości (interfejs użytkownika)

- [ ] Mobilny-pierwszy; brak poziomego przepływu 320–1920px; docelowe dotykowe ≥44px.
- [ ] Tylko tokeny projektowe — zero zakodowanych kolorów/promieni/ciągów marki.
- [ ] Tabele → karty na telefonie (`DataLabel` + `Breakpoint.Sm`); ładowanie/puste/błędne stany obecne.
- [ ] Utwórz/edytuj poprzez dialog; pełny ekran na urządzeniu mobilnym.
- [ ] Każdy formant ma `HelpTip` pochodzący z dokumentów.
- [ ] White-label + PWA respektowane.
- [ ] Mobilny + pulpit E2E dodany (dym, brak przepływu, podróż, nieszczęśliwa ścieżka); `dotnet test` zielony.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` czysty na zmieniane pliki.
