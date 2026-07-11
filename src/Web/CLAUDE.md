# src/Web — Blazor Server + Minimal API + MudBlazor UI

Application/presentation layer: endpoints, hosted services, and Razor components **orchestrate** —
they never decide. Any `if` encoding a business rule belongs on an aggregate or domain service, not
here. Full UI contract: [docs/ui-guidelines.md](../../docs/ui-guidelines.md).

## UI — MANDATORY (blocking for every user-facing change)

- **Dialogs, never inline forms.** Every add/create/edit action opens a MudBlazor dialog via
  `IDialogService.ShowAsync<TDialog>` — a toolbar button (`New X`, top-right via `MudSpacer`) opens it;
  the dialog owns the form + validation and returns a nested `public sealed record …Result(...)`; the
  page does the HTTP call, then reloads. Dialogs live in `Components/Dialogs/`. **Never** reintroduce
  the old "card with fields + Create button at top of page" pattern (removed on purpose). Row actions
  (start/stop/delete) stay inline as icon buttons; destructive confirms may use a simple dialog.
- **Mobile-first.** Author every page/dialog for a 360–430px phone, enhance upward. **No horizontal
  scroll at any width 320–1920px.** Touch targets ≥44px; inputs ≥16px. Bottom nav
  (`Components/Layout/BottomNav.razor`) is the primary phone nav.
- **Design tokens only** — no hard-coded colours/radii/brand strings. Use the MudBlazor theme + CSS
  `var(--app-*)` tokens (from `Branding/BrandingCss.cs`); they flow from white-label `BrandingOptions`.
- **Tables → cards on phones:** every `MudTable` sets `Breakpoint="Breakpoint.Sm"`; every `MudTd` has a
  `DataLabel` (template: `Components/Pages/Nodes.razor`).
- **Every control gets `<HelpTip Text="…" />`** (`Components/HelpTip.razor`); text sourced from `docs/`,
  updated in the same commit.
- **Guard the load / recover the boundary:** a gated API returns 404; guard page loads and reset the
  `ErrorBoundary` on nav or a sticky error crashes the page (see feature-gate/ErrorBoundary gotcha).

## `.razor` correctness

- Fix **every** Rider `get_file_problems` finding in `.razor` too, not just `.cs` — nullable derefs in
  lambdas included (`v => $"{v?.Count ?? 0}"`, don't leave the deref). A green `dotnet build` is not
  enough; analyzer/inspection findings are real errors.

## E2E — blocking (`tests/E2ETests`, Playwright)

- Every new page/dialog/action/nav entry/endpoint-a-page-calls ships a Playwright test driving the
  real UI through `AppFixture` on **mobile emulation** (`NewAuthedMobilePageAsync`) **and** desktop:
  create/edit/save round-trip + happy path + renders without the Blazor error UI + an unhappy path.
- New static route → add to `PageSmokeTests` **and** `MobileLayoutTests`. New help tip → assert
  tap-opens. Converted page → add to the no-overflow set.

Modern C# 14 + strict DDD per root `CLAUDE.md`.
