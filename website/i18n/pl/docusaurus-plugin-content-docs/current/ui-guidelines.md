---
description: "Wymagania dla każdego nowego lub zmienionego elementu interfejsu użytkownika w tej aplikacji (strony Blazor, dialogi, komponenty). To jest źródło prawdy, do którego odwołuje się CLAUDE.md. Jeśli reguła Cię blokuje, zatrzymaj się i zapytaj — nie wysyłaj interfejsu użytkownika, który jej narusza. Zakorzenione w `plans/ui-overhaul.md`."
---

# UI Design Guidelines — MANDATORY

Wymagania dla **każdego** nowego lub zmienionego elementu interfejsu użytkownika w tej aplikacji (strony Blazor, dialogi, komponenty).
To jest źródło prawdy, do którego odwołuje się `CLAUDE.md`. Jeśli reguła Cię blokuje, zatrzymaj się i zapytaj — nie wysyłaj interfejsu użytkownika, który jej narusza. Zakorzenione w `plans/ui-overhaul.md`.

## 1. Mobile-first, always

- **Projektuj dla telefonu o rozdzielczości 360–430px najpierw**, a następnie rozszerz w górę za pomocą mediów `min-width` / właściwości punktu przerwania MudBlazor. Nigdy nie zaczynaj z pulpitu za pomocą przesłonięć `max-width`.
- **Brak przewijania poziomego przy żadnej szerokości 320–1920px.** Jeśli zawartość jest szersza niż okienko przeglądarki, to jest błąd.
- Obszary dotyku ≥ **44px** (`var(--app-touch-target)`). Pola tekstowe wejściowe ≥ 16px czcionka (zapobiega powiększeniu na skupieniu iOS).
- Szanuj nacięcia: użyj `env(safe-area-inset-*)`; okienko przeglądarki już ustawia `viewport-fit=cover`.
- Honoruj `prefers-reduced-motion` — nie przekazuj niezbędnych informacji tylko poprzez animację.

## 2. Design tokens — no hard-coded values

- Wszystkie kolory/promienie/odstępy pochodzą z **tokenów projektowych**: motyw MudBlazor (`Web/Components/Theme.cs`) +
  niestandardowych właściwości CSS emitowanych przez `Web/Branding/BrandingCss.cs` (`var(--app-primary)`,
  `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Nigdy nie koduj na stałe koloru szesnastkowego, promienia ani ciągu marki w komponencie lub regule CSS.** Przeczytaj token.
  Tokeny pochodzą z białolabelkowego `BrandingOptions`, więc paleta sprzedawcy musi dotrzeć do Twojego interfejsu bezpłatnie.
- Nowa wartość wpływająca na markę → dodaj token + pole brandingowe; nie wbudowuj go.

## 3. Responsive layout & data

- **Tabele są zwijane w karty na telefonach.** Każdy `MudTable` ustawia `Breakpoint="Breakpoint.Sm"` a każdy
  `MudTd` ma `DataLabel`. Brak czystej szerokiej tabeli na mobilnych. (Szablon: `Components/Pages/Nodes.razor`.)
- Siatki: `MudItem xs="12" sm="6" md="4"` — pełna szerokość na telefonie, kolumny wielokolumnowe w górę.
- Formularze jednokolomnowe na mobilnych; duże obszary dotykowe; `inputmode`/`autocomplete` na wejściach; inputmode numeryczne/dziesiętne
  dla pieniędzy/procentów.
- **Prawidłowe kontrolki dla strukturalnego wejścia — nigdy nie surowe pole tekstowe dla liczb czy list.** Zbieraj liczby,
  pieniądze, procenty, daty, enumy i wszelkie dane wielowartościowe za pomocą odpowiedniej kontrolki (`MudNumericField`,
  `MudDatePicker`, `MudSelect`, edytowalna lista dodawania/usuwania wierszy pól wpisanych lub tabela), każde pole
  indywidualnie zatwierdzone. Pojedyncze wolne pole `MudTextField`, w które użytkownik musi wpisać obiekt oddzielony przecinkami/spacją/nową linią — które następnie analizujesz — jest **zabronione**: jest podatne na błędy, niezatwierdzone i wroga na telefonie. **Nikt nie chce wpisywać obiektu.** Wielowartościowe wejście jest edytowalną listą wpisanych wierszy (dodaj /
  usuń) lub jest ładowane z istniejących danych domeny (np. przeprowadź sprawdzenie bezpośrednio z zakończonego backtestu
  zamiast ponownego wprowadzania jego liczb). Zwykły `MudTextField` jest tylko dla autentycznego wolnego tekstu — nazw, notatek,
  wyszukiwania, opisów.
- Udostępnij **ładowanie, puste i błędne** stany na każdej liście/szczegółach — rozmiar dla mobilnych.
- Nawigacja mobilna **dolna** (`Components/Layout/BottomNav.razor`) to główna nawigacja telefonu; szufladka pogrupowana to pełne menu. Dodaj tam miejsca o dużym ruchu; trzymaj to ≤5 przedmiotów.

## 4. Dialogs (create/edit)

- Wszystkie akcje dodawania/tworzenia/edycji/nowe używają **dialoga MudBlazor** (`IDialogService.ShowAsync<TDialog>`), nigdy
  wbudowanego formularza strony. Dialogi znajdują się w `Web/Components/Dialogs/`, udostępniają `[Parameter]`s, zwracają zagnieżdżony
  `public sealed record …Result(...)`. Akcje wierszy listy (start/stop/usuń) pozostają wbudowane jako przyciski ikon.
- Na telefonach dialogi powinny być **pełnoekranowe / pełna szerokość** i świadome klawiatury.

## 5. Inline help — every control

- Każda nieoczywista opcja, select, przełącznik lub akcja uzyskuje **`<HelpTip Text="…" />`**
  (`Components/HelpTip.razor`) — najedź na pulpit, **stuknij na mobilnym**. Źródło tekstu z `docs/` aby
  wskazówka pozostała zsynchronizowana z zachowaniem; aktualizuj oba w tym samym zatwierdzeniu.

## 6. White-label

- Nazwa produktu, logo, opis, wsparcie/firma, kolory, favicon wszystko pochodzi z `BrandingOptions`.
  Odwołaj się do nich (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), nigdy dosłownie "cMind" ani
  kolor marki. Manifest PWA, ikony, kolor motywu i bohater logowania są wszyscy markowani.

## 7. PWA

- Aplikacja jest instalowalna. Utrzymuj punkt końcowy manifestu (`/manifest.webmanifest`) marki, ikony obecne
  (192/512/maskable + apple-touch), pracownik serwisu tylko aplikacji powłoki (nigdy nie dotykając obwodu Blazor/`_framework`/huby), oraz działającą stronę offline. Nowa statyczna trasa → utrzymuj manifest `scope`.
- Blazor Server potrzebuje żywego obwodu SignalR → **instalowalne + powłoka aplikacji**, nie pełny offline. Nie
  obiecuj interaktywności offline.

## 8. Accessibility

- Etykiety na wejściach, `aria-*` na niestandardowych kontrolkach, widoczne skupienie, logiczna kolejność skupienia. Ponieważ motyw jest
  białolabelkowy, zweryfikuj **kontrast** względem aktywnego motywu, a nie stałą paletę.

## 9. E2E — no UI ships untested (blocking)

Każda zmiana interfejsu użytkownika widoczna dla użytkownika wysyła Playwright E2E w `tests/E2ETests`, prowadzona jak prawdziwy użytkownik, **na emulacji urządzenia mobilnego** plus pulpit:

- Nowa trasa → dodaj ją do `PageSmokeTests` **i** `MobileLayoutTests` (wyświetla, nawigacja dolna, brak interfejsu błędu).
- Konwertuj tabelę/stronę → dodaj jej trasę do zestawu mobilnego **bez przepełnienia**.
- Nowy przepływ → realistyczna mobilna podróż (round-trip tworzenia/edycji/zapisywania) **i** nieszczęśliwa ścieżka
  (nieprawidłowe wejście, pusta lista, brak uprawnień na rolę).
- Nowa wskazówka pomocy → potwierdź, że otwiera się na stuknięcie (`HelpTipTests` wzór).
- Użyj `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (emulacja urządzenia).
- `dotnet test` zielone przed "gotowe". Emulowany WebKit ≠ mobilny Safari — rzeczywista brama urządzenia to osobny
  krok wydania.

## 10. Definition of done (UI)

- [ ] Mobile-first; brak poziomego przepełnienia 320–1920px; obszary dotyku ≥44px.
- [ ] Tylko tokeny projektowe — zero twardych kolorów/promieni/ciągów marki.
- [ ] Tabele → karty na telefonie (`DataLabel` + `Breakpoint.Sm`); obecne stany ładowania/puste/błędu.
- [ ] Strukturalne wejście używa prawidłowych kontrolek zatwierddzonych (numeryczne/data/select/edytowalna lista wierszy) — brak surowego
      pola tekstowego, w które użytkownik wpisuje wielowartościowy/wartościowy obiekt oddzielony ogranicznikami.
- [ ] Tworzenie/edycja za pośrednictwem dialogo; pełnoekranowe na mobilnych.
- [ ] Każda kontrolka ma `HelpTip` pochodzący z dokumentów.
- [ ] Białolabel + PWA szanowany.
- [ ] Dodano E2E mobilne + pulpitowe (dym, brak przepełnienia, podróż, nieszczęśliwa ścieżka); `dotnet test` zielone.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` czysty na dotyczonych plikach.
