# src/Web — Blazor Server + Minimal API + MudBlazor UI

Application/presentation layer: endpoints, hosted services, and Razor components **orchestrate** —
they never decide. Any `if` encoding a business rule belongs on an aggregate or domain service, not
here. Full UI contract: [website/docs/ui-guidelines.md](website/docs/ui-guidelines.md).

## UI — MANDATORY (blocking for every user-facing change)

- **Dialogs, never inline forms.** Every add/create/edit action opens a MudBlazor dialog via
  `IDialogService.ShowAsync<TDialog>` — a toolbar button (`New X`, top-right via `MudSpacer`) opens it;
  the dialog owns the form + validation and returns a nested `public sealed record …Result(...)`; the
  page does the HTTP call, then reloads. Dialogs live in `Components/Dialogs/`. **Never** reintroduce
  the old "card with fields + Create button at top of page" pattern (removed on purpose). Row actions
  (start/stop/delete/**edit**) stay inline as icon buttons; destructive confirms may use a simple dialog.
  - **Exception — a large multi-section form is a full page, not a dialog** (and still not the banned
    top-of-page card). The copy-profile **create** (`/copy-trading/new`) and **edit** (`/copy-trading/{id}`,
    `CopyTradingDetail.razor`) are deliberate full pages reached by their own route (the Edit **row icon**
    navigates there, disabled while the profile is Running). Do **not** "restore" them to a dialog to
    satisfy the dialog rule. A full page is the *rare* exception justified by form size; the default for
    add/create/edit stays a dialog.
- **Mobile-first.** Author every page/dialog for a 360–430px phone, enhance upward. **No horizontal
  scroll at any width 320–1920px.** Touch targets ≥44px; inputs ≥16px. Bottom nav
  (`Components/Layout/BottomNav.razor`) is the primary phone nav.
- **Design tokens only** — no hard-coded colours/radii/brand strings. Use the MudBlazor theme + CSS
  `var(--app-*)` tokens (from `Branding/BrandingCss.cs`); they flow from white-label `BrandingOptions`.
- **Tables → cards on phones:** every `MudTable` sets `Breakpoint="Breakpoint.Sm"`; every `MudTd` has a
  `DataLabel` (template: `Components/Pages/Nodes.razor`).
- **Proper controls for structured input — never a raw text box for numbers/lists.** Numbers, money,
  percent, dates, enums and multi-value data use the right validated control (`MudNumericField`,
  `MudDatePicker`, `MudSelect`, an editable add/remove row list of typed fields, a table) — one field per
  value. A free-text `MudTextField` the user types a comma/space/newline blob into (then you `.Split`/parse)
  is **forbidden**: unvalidated, error-prone, hostile on mobile. Multi-value = an editable typed-row list, or
  load it from existing domain data (e.g. score a completed backtest directly instead of re-typing its
  numbers). `MudTextField` is only for genuine free text (names, notes, search). Full rule:
  [ui-guidelines §3](../../website/docs/ui-guidelines.md).
- **Every control gets `<HelpTip Text="…" />`** (`Components/HelpTip.razor`); text sourced from `docs/`,
  updated in the same commit.
- **Guard the load / recover the boundary:** a gated API returns 404; guard page loads and reset the
  `ErrorBoundary` on nav or a sticky error crashes the page (see feature-gate/ErrorBoundary gotcha).

## Localization — MANDATORY (blocking, build-enforced)

- **No literal user-facing text.** Inject `@inject IStringLocalizer<Ui> L` and write `@L["key"]` for
  every visible string — element text, `Label`/`Text`/`Title`/`Placeholder`/`HelperText`/`aria-label`/
  `alt`, snackbar messages, page titles. A hard-coded string fails the build (`NoHardcodedUiTextTests`).
- **Add the key everywhere at once.** New/changed key → edit `tools/i18n/ui-translations.json` for
  **all** languages in `Core.Constants.SupportedCultures`, then `pwsh tools/i18n/gen-resx.ps1`.
  `ResourceParityTests` fails on a missing or blank translation.
- **Formatting:** UI display uses `CurrentCulture`; wire/parse/CSV/cbotset stays `CultureInfo.InvariantCulture`.
- **RTL:** never assume LTR — the shell already flips via `<html dir>` + `MudRTLProvider`; use logical CSS
  (`margin-inline-start`, not `margin-left`) for anything direction-sensitive.
- **Culture switch** goes through the `GET /set-culture` endpoint (full reload); a Blazor circuit can't
  change culture live. Signed-in choice persists to `UserProfile.Locale` and rides the login cookie.
- **Careful: MudBlazor param typos don't fail `get_file_problems`** (unknown component parameters throw
  only at render). Verify a new Mud parameter exists (e.g. RTL is `MudRTLProvider.RightToLeft`, **not**
  `MudThemeProvider`), and cover the page with an E2E render test.
- Full guide: `website/docs/features/localization.md`.

## `.razor` correctness

- Fix **every** Rider `get_file_problems` finding in `.razor` too, not just `.cs` — nullable derefs in
  lambdas included (`v => $"{v?.Count ?? 0}"`, don't leave the deref). A green `dotnet build` is not
  enough; analyzer/inspection findings are real errors.

## MudBlazor gotchas (each cost a debug loop)

- **`MudSelect` shows the raw VALUE (a Guid) for a value set in code**, not the item text — a prefilled
  edit dialog then displays a bare id. Fix: set **`ToStringFunc`** mapping value→label (see
  `EditInstanceDialog`). It only affects the closed display; the options still render their child content.
- **`MudSelect`'s `data-testid` lands on its hidden `<input>`**, whose `value` attribute is the ToStringFunc
  display text (not the bound value, not element text). So in E2E assert it with `InputValueAsync()` /
  `ToHaveValueAsync`, **not** `ToContainText`/`InnerText`; and to open the popover click the visible
  `.mud-input-control`, not the hidden input.
- A dialog shown via `Dialogs.ShowAsync<T>("Title", …)` renders the **ShowAsync title**, not the
  component's `TitleContent` — don't select the dialog in E2E by its `TitleContent` text; use a
  `data-testid` inside it.
- `Slow` (a `LocatorAssertionsToBeVisibleOptions`) is **not** accepted by `ToBeEnabledAsync` /
  `ToBeHiddenAsync` — each assertion has its own options type; pass `new() { Timeout = … }`.

## E2E — blocking (`tests/E2ETests`, Playwright)

- Every new page/dialog/action/nav entry/endpoint-a-page-calls ships a Playwright test driving the
  real UI through `AppFixture` on **mobile emulation** (`NewAuthedMobilePageAsync`) **and** desktop:
  create/edit/save round-trip + happy path + renders without the Blazor error UI + an unhappy path.
- New static route → add to `PageSmokeTests` **and** `MobileLayoutTests`. New help tip → assert
  tap-opens. Converted page → add to the no-overflow set.

Modern C# 14 + strict DDD per root `CLAUDE.md`.
