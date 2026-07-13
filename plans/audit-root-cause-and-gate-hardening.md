# Root-Cause: Why 71 Bugs Shipped Despite the Mandates — and the Gate Hardening That Stops It

## The uncomfortable finding

The mandates were **not missing**. `src/Web/CLAUDE.md` already required, in writing, almost every
single thing the audit flagged:

- "Tables → cards on phones: every `MudTable` sets `Breakpoint="Breakpoint.Sm"`; every `MudTd` has a
  `DataLabel`." → yet D-09, D-10, E-08, G-04 shipped raw tables that overflow on mobile.
- "Every control gets `<HelpTip>`." → yet D-08, G-06 shipped with none.
- "No literal user-facing text … a hard-coded string fails the build." → yet A-04, B-07, E-05, F-01,
  H-01, I-03 shipped ~30 fully un-localized pages.
- "Dialogs, never inline forms … destructive confirms may use a simple dialog." → yet A-06, B-03,
  D-05, D-06 delete/erase with **no confirmation at all**, and D-01 is a full inline page form.
- "Guard the load … a gated API returns 404." → yet H-02 leaves `/api/nodes` open when the UI is
  hidden; C-01/C-02/C-03 return raw 500s.

So the rules were law on paper and ignored in practice. **A mandate that is not machine-enforced is a
suggestion.** Under delivery pressure — human or agent — suggestions lose.

## The mechanism: opt-in census gates silently exempt everything new

The repo *does* have machine gates (`NoHardcodedUiTextTests`, `MobileLayoutTests`, `PageSmokeTests`,
`ResourceParityTests`, `WhiteLabelCatalogParityTests`). They are green. Bugs shipped anyway. Why?

Because the strongest gates are **opt-in**: they check a *hand-maintained list of enrolled files/routes*,
not the *whole surface*. The moment you add a new page and forget to enroll it, the gate says nothing.

Concrete proof — `NoHardcodedUiTextTests.MigratedRazorFiles()`:

```csharp
public static IEnumerable<object[]> MigratedRazorFiles() => new[]
{
    "Layout/MainLayout.razor",
    "Layout/NavMenu.razor",
    "Layout/BottomNav.razor",
    "Dialogs/SettingsDialog.razor",
    "LanguageSwitcher.razor",
}.Select(f => new object[] { f });   // ← 5 files. Every other page is invisible to the gate.
```

The comment even *admits* it: *"The scanned set is the fully-migrated app shell; it grows as pages
migrate (add the file to MigratedRazorFiles)."* Growth is manual, so it never happened. 30+ pages
were authored after this list froze, none enrolled, all un-localized, build green the whole time.

Same failure shape in `MobileLayoutTests` (a hand-listed `MobileRoutes()` / `NoOverflowRoutes()` — the
quant/journal/agent pages were never added, F-03/E-08) and in `RouteCoverageTests.ExcludedRoutes`
(`/instance/{id}` excluded "covered by their own feature E2E" — but that feature E2E was never
written, B-08).

The **one gate that works correctly is `RouteCoverageTests`** — and it works *because it is census-based*:
it `Directory.EnumerateFiles(webRoot, "*.razor")`, discovers **every** `@page`, and fails on any route
not smoke-tested. Opt-*out* (exclude with a reason), not opt-*in* (enroll to be checked). That is the
template every gate must follow.

## Root-cause taxonomy (each class → the gate that was missing)

| # | Bug class | Examples | Why it escaped | Enforcing gate added |
|---|-----------|----------|----------------|----------------------|
| R1 | Un-localized pages | A-04, B-07, E-05, F-01, H-01, I-03 | Localization gate is opt-in (5-file list) | **Census** `NoHardcodedUiTextTests` — scan all `Pages/**` + `Dialogs/**` + `Components/**`, opt-out `PendingLocalization` set that only shrinks |
| R2 | Mobile table overflow | D-09, D-10, E-08, G-04 | Mobile gate is opt-in route list | **Census** `MobileLayoutTests` — every `PageSmokeTests` route auto-checked for overflow; + a source gate: every `MudTable` has `Breakpoint` + every `MudTd` a `DataLabel` |
| R3 | Destructive action, no confirm | A-06, B-03, D-05, D-06 | No gate existed; "may use a dialog" was optional wording | Shared `ConfirmDialog`; **source gate** `DestructiveActionConfirmTests` — any delete/erase/kill handler must route through it; E2E asserts the dialog appears before the mutation |
| R4 | Raw `DomainException`/`DbUpdateException` → HTTP 500 | C-01, C-02, C-03 | No global exception→ProblemDetails mapping; each endpoint hand-caught | Global `IExceptionHandler` mapping `DomainException`→400, unique-violation→409; **integration gate** asserts no endpoint 500s on a domain/constraint error |
| R5 | Feature gated in UI but not API | H-02 | Gating helper applied to nav+page, not endpoint | **Integration gate** `GatingParityTests` — a hidden feature's API returns 404/403 |
| R6 | Detail page crashes / blank on missing/terminal entity | B-02 | E2E only drove the happy entity | `PageSmokeTests` extended with a "missing id" + "terminal entity" case per detail route; census forces every `{id}` route to have one |
| R7 | Stale test → false coverage | E-06 (`/assistant` deleted) | Nothing checks tests reference live routes | **Gate** `RouteExistenceTests` — every route literal in an E2E must resolve to a real `@page` |
| R8 | Test asserts "renders", not "works" | E-07, G-05, F-06 | "E2E" satisfied by page-load; outcome never asserted | Mandate + review rule: an AI/feature E2E must assert the *observable outcome* (canned reply text, row count, status transition), enforced by review + a lint that flags feature tests with no post-action assertion |
| R9 | Provider-coupled copy (hardcoded "Anthropic") | E-01, G-03 | String not localized *and* not provider-derived | Fixed at source (provider-derived, localized); caught going forward by R1 census + a unit test on `AiFeatureNotice` |
| R10 | Raw GUID / internal id shown | B-01, D-11 | No gate on rendered identifiers | E2E census assertion: no route body contains a GUID-shaped string; API DTOs reviewed for id leakage |
| R11 | `DateTime.UtcNow` in a component | A-02 | Analyzer sweep covers `.cs`, not `.razor` markup expressions | **Source gate** `NoWallClockInRazorTests` — scan `.razor` for `DateTime.UtcNow`/`.Now`/`DateTimeOffset.UtcNow` |
| R12 | `MustChangePassword` not enforced | A-08 | Flag modeled + returned, never gated in UI/middleware | Fix in auth middleware; E2E drives the forced-change redirect; failure-path tier now mandatory for auth flags |

## The meta-rule this adds to CLAUDE.md

> **Every mandate that names "every X" MUST be backed by a census gate that discovers all X from source
> and fails on any unenrolled instance. Opt-in enrollment lists are forbidden for coverage gates — use
> opt-out exclusion sets with a written reason per entry, and the exclusion set may only shrink.**

An opt-in list is a promise to remember. A census gate is a guarantee. We ship guarantees.

## Gate hardening delivered in this change

1. `NoHardcodedUiTextTests` → census over all component surfaces; `PendingLocalization` opt-out
   (shrinks to empty as pages are localized in this same effort).
2. `MobileLayoutTests` → census over `PageSmokeTests.Routes()`; raw-table source gate.
3. `DestructiveActionConfirmTests` → new source gate; shared `Components/Dialogs/ConfirmDialog.razor`.
4. Global `DomainExceptionHandler` (IExceptionHandler) + `DomainExceptionMappingTests` integration gate.
5. `GatingParityTests` → hidden feature ⇒ API not reachable.
6. `RouteExistenceTests` → no E2E references a dead route.
7. `NoWallClockInRazorTests` → no `DateTime.UtcNow`/`.Now` in `.razor`.
8. CLAUDE.md updated: the census meta-rule (above) + mandate 11 extended with the confirm-dialog and
   gating-parity sub-rules made **build-enforced**, not prose.

Each of the 71 findings is fixed at source and covered by unit + integration + E2E per
`plans/full-feature-live-audit-findings.md`; the gates above ensure the *class* can never silently
reappear.
