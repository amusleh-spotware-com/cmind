# UI Overhaul — Mobile-First, Responsive, Installable, Fully E2E-Tested

Status: **PLAN — not implemented.** No code in this doc is applied. This is the step-by-step
blueprint for overhauling the whole app UI.

## 0. Goal & non-negotiables

Turn cMind's functional-but-basic UI into a polished, **mobile-first**, fully responsive,
**installable** (PWA) app whose theme is fully white-labelable, where every control has inline
help, and where **every pixel of the UI is exercised by realistic E2E tests on real mobile
viewports** — nothing untested.

Hard requirements (from the request, treated as acceptance gates):

1. **Mobile-first.** Every screen designed for a 360–430px phone first, enhanced upward. Mobile UX is
   the priority, not an afterthought.
2. **Fully responsive.** Flawless from 320px to ultra-wide. No horizontal scroll, no clipped controls,
   no overlapping elements at any width.
3. **Modern stack, keep Blazor.** Stay on Blazor Server + MudBlazor (already in place); modernize the
   *design layer* — design tokens, responsive patterns, motion, PWA shell — not the framework.
4. **100% E2E UI coverage.** Extend `tests/E2ETests` (Playwright) so every page, dialog, action, nav
   entry, and endpoint-backed control is driven like a real user, **on mobile devices specifically**,
   plus tablet/desktop. Every issue — even tiny ones — must be caught by a test and fixed.
5. **Inline help everywhere.** Every option, select, checkbox, and button gets a tooltip / help
   affordance sourced from `docs/`.
6. **Installable app.** Usable on a phone like a native app (add-to-home-screen, standalone display,
   splash, app icon, offline-tolerant shell).
7. **White-label-driven theming.** Theme, accent, background, logo, radius, typography all flow from
   the existing branding solution — no hard-coded colors in components.
8. **Login redesign.** Cool, professional, on-brand.
9. **Docs + mandates.** Update all `docs/`, add mobile-first / E2E / help-text mandates to both
   `CLAUDE.md` files, ship a permanent **UI Design Guidelines** doc Claude must follow, and make the
   repo README beautiful and attention-grabbing.

### Honest constraint (research-grounded)

Blazor **Server** renders through a live SignalR circuit. A service worker can cache the **app shell,
static assets, icons, and an offline fallback page**, making the app *installable* and resilient to
flaky connectivity — but genuine *offline interactivity* is impossible without moving to Blazor
WebAssembly. This plan delivers **installable + app-shell-cached + graceful offline page**, and flags
"full offline" as an explicit future track (WASM/hybrid), not a promise we silently break.

## 1. Current-state audit (baseline — already in repo)

Keep and build on these; do **not** rebuild from scratch:

- **MudBlazor** component library, custom dark cTrader-style theme (`wwwroot/css/site.css`).
- **White-label** already exists: `Web/Branding/BrandingThemeProvider.cs` (`IBrandingThemeProvider`)
  drives `MudThemeProvider Theme` in `MainLayout.razor`; `App.razor` injects `branding.CustomCss`,
  favicon, theme-color, description from `AppOptions.Branding`.
- **Feature gating**: `NavMenu.razor` groups links and hides them via `IFeatureGate`.
- **PWA manifest present but broken**: `wwwroot/manifest.webmanifest` has `"icons": []` → **not
  installable**. No service worker. `App.razor` links the manifest + `viewport-fit=cover` already.
- **Layout**: `MainLayout` = `MudAppBar` (dense) + `MudDrawer Variant=Responsive Breakpoint=Md` +
  `MudContainer`. Drawer defaults **open** (bad on mobile).
- **Login**: hand-rolled `Login.razor` with inline SVG + `app-login-*` CSS classes; functional POST to
  `/api/auth/login`, minimal styling.
- **Pages** (`Web/Components/Pages`): Index (dashboard), CBots, Run, Backtest, BuilderEditor (Monaco),
  Nodes, Users, Accounts/TradingAccountList, CopyTrading, Agent, Alerts, PropGuard, PropFirm,
  Assistant, Mcp, InstanceTable/Detail, Account, OpenApiApplications, Compliance, AiSettings,
  FeatureSettings. Dialogs in `Components/Dialogs/*`.
- **E2E**: `AppFixture` boots real Web + Postgres (Testcontainers), Edge/Chromium, **default desktop
  viewport**; `PageSmokeTests` GETs each route asserting no crash. Feature-specific tests exist. No
  mobile emulation, no responsive assertions, no visual/a11y checks, no tooltip checks.

### Known UI defects to sweep (representative — the E2E overhaul must find the rest)

- Drawer open-by-default overlays content on phones.
- Data tables overflow horizontally on mobile (no `DataLabel`, no card breakpoint).
- Dialogs not full-screen on mobile → cramped forms.
- No `apple-touch-icon`, empty manifest icons → no installability, ugly home-screen icon.
- No tooltips/help on any control.
- Hard-coded hex colors in `site.css` bypass white-label tokens.
- Long tables/dashboards lack empty/loading/error states on small screens.

## 2. Design foundation (Phase 1) — tokens, theme, breakpoints

**Deliverable: a single source of design truth wired to white-labeling.**

### 2.1 Design tokens

- Define the token set: color roles (`primary/accent/surface/background/success/error/warning/info`,
  on-* foregrounds), radius scale, elevation, spacing scale, typography scale, motion durations.
- Extend `AppOptions.Branding` + `BrandingThemeProvider` to emit **all** of these into the MudBlazor
  `MudTheme` (`PaletteDark`, `Typography`, `LayoutProperties.DefaultBorderRadius`) **and** as CSS
  custom properties (`--app-*`) injected once in `App.razor`, so both MudBlazor components and custom
  CSS read the same white-label values. **No component may hard-code a color/radius/spacing** — all
  reference tokens.
- Migrate `site.css` hard-coded hexes → `var(--app-*)` fallbacks. Keep the cTrader dark default as the
  *default* token values, overridable per-tenant.

### 2.2 Breakpoint & layout policy (mobile-first)

- Adopt MudBlazor breakpoints `Xs<600 / Sm<960 / Md<1280 / Lg<1920 / Xl`. **Author styles mobile-first**
  (base = phone), add `Sm`/`Md`+ enhancements upward — never desktop-first with `max-width` overrides.
- **Mobile navigation**: drawer **closed by default** on `Xs/Sm`; add a **bottom navigation bar**
  (`MudBottomNavigation` or custom) for the top 4–5 destinations on phones, with the grouped drawer as
  the full menu behind the hamburger. Desktop keeps the persistent drawer.
- `MainLayout` container padding responsive (`pa-2` phone → `pa-md-6` desktop, already partly done);
  ensure `MaxWidth` caps content on ultra-wide.
- Safe-area insets: use `env(safe-area-inset-*)` for notch/home-indicator (viewport already has
  `viewport-fit=cover`).

### 2.3 Motion & polish

- Subtle, tasteful transitions (page/section fade, dialog slide-up on mobile), respecting
  `prefers-reduced-motion`. No gratuitous animation.

## 3. Responsive component & pattern library (Phase 2)

Build reusable patterns so every page composes from mobile-correct primitives:

1. **Responsive data display** — replace raw `MudTable`/grids: add `DataLabel` to every cell, set
   `Breakpoint="Breakpoint.Sm"` so tables collapse to **cards** on phones; use `HideSmall` for
   low-priority columns. For dense screens (Nodes, Instances, CopyTrading) provide a dedicated **card
   list** on `Xs`. No horizontal table scroll on mobile.
2. **Dialog → mobile sheet**: all `Components/Dialogs/*` open **full-screen** on `Xs` (`FullScreen`
   /`FullWidth` responsive), keyboard-aware, sticky action bar at bottom. Keeps the "all add/edit via
   dialog" CLAUDE mandate but makes it phone-usable.
3. **Toolbar/actions**: page-level `New X` action collapses into a FAB or overflow menu on mobile;
   row actions become an overflow `MudMenu` when >2 actions on `Xs`.
4. **Forms**: single-column on mobile, `xs=12 sm=6` grid on larger; large touch targets (≥44px);
   numeric/decimal input modes for money/percent fields.
5. **Feedback states**: standardized loading (skeleton), empty, and error components used on every
   list/detail page — sized for mobile.
6. **Help affordance** (see §5): a reusable `HelpTip` component (icon + `MudTooltip`) placed beside
   labels/controls, content sourced from a help-text registry.
7. **Charts** (InstanceDetail equity curve, dashboard): responsive width, touch-friendly, legible on
   small screens.

## 4. PWA / installable app (Phase 3)

Make it a real add-to-home-screen app (installable app-shell; offline caveat per §0):

1. **Fix manifest** (`manifest.webmanifest`): real `name`/`short_name`, `description`, `id`,
   `start_url`, `scope`, `display: "standalone"`, `orientation`, `theme_color`/`background_color`
   sourced from branding, `categories`, and a full **icons** array — 192, 512, and a **maskable**
   512. **White-label**: serve the manifest from a branded endpoint so tenants override name/icons/
   colors (generate `manifest.webmanifest` dynamically from `AppOptions.Branding`).
2. **Icons**: generate `icon-192.png`, `icon-512.png`, `icon-512-maskable.png`, `apple-touch-icon`
   (180), favicon set; add `<link rel="apple-touch-icon">` + iOS splash `apple-touch-startup-image`
   tags in `App.razor` (iOS ignores manifest icons/splash). All brandable.
3. **Service worker**: app-shell/static-asset caching + versioned caches +
   `updateViaCache:'none'` registration; **offline fallback page** ("You're offline — reconnecting")
   since Server needs the circuit. Cache-bust on release. No aggressive caching in Development.
4. **Install affordance**: capture `beforeinstallprompt` (Chromium/Android) for a custom "Install app"
   button; iOS shows the "Add to Home Screen" hint (no `beforeinstallprompt`).
5. **Mobile chrome**: theme-color, status-bar style, standalone-mode CSS (hide any browser-only UI),
   pull-to-refresh behavior handled.

## 5. Inline help / tooltips / guides (Phase 4)

Every option, select box, and button gets contextual help sourced from docs:

1. **Help-text registry**: a strongly-typed catalog (constants + resx or a `docs/`-backed map) keyed by
   control, populated from `docs/features/*.md`. No hard-coded help strings scattered in components
   (respects the "no magic strings" rule → keys/constants).
2. **`HelpTip` component**: `MudTooltip` on desktop (hover) + tap-to-open popover on mobile (no hover
   on touch). Accessible (`aria-describedby`, focusable).
3. **Coverage**: sweep every page/dialog control and attach a `HelpTip` or `HelpText`. Complex flows
   (Builder, Backtest params, Prop-firm rules, Copy profiles) get a short inline guide / first-run
   coach-mark.
4. **Docs linkage**: help content stays in sync with `docs/features/*` (mandate: touching a feature
   updates its help text + doc in the same commit).

## 6. Page-by-page overhaul (Phase 5)

For **each** page: mobile layout first, responsive table→card, help tips, states, white-label tokens,
and a matching mobile E2E journey. Priority order:

1. **Login** (`Login.razor`) — flagship redesign: branded split/hero layout on desktop, centered card
   on mobile; brand logo/gradient from white-label; polished fields with icons, show/password toggle,
   loading state, clear error surface, "remember me", legal links. On-brand, professional, animated
   entrance. Fully keyboard + screen-reader accessible.
2. **Dashboard / Index** — mobile stat cards stack; responsive charts; live tiles legible on phone.
3. **CBots / Run / Backtest** — list→cards on mobile; param dialogs full-screen; Monaco
   **BuilderEditor** given a usable mobile mode (read/review + guarded editing; Monaco is desktop-
   oriented — provide a mobile-friendly fallback and clear affordances).
4. **Instances (Table/Detail)** — card list on mobile, equity chart responsive, logs viewer mobile-ok.
5. **Nodes / Users** — admin tables → cards; action overflow menus.
6. **Accounts / TradingAccountList / CopyTrading / OpenApiApplications** — dialog flows mobile-first.
7. **AI: Assistant / Agent / Alerts / PropGuard / PropFirm / Mcp** — chat/AI panels mobile-optimized;
   forms and result panels stack.
8. **Settings: AiSettings / FeatureSettings / Compliance** — grouped, mobile-friendly settings lists.
9. **Account** page — profile/password mobile layout.

Each page ships in the same commit as: its responsive markup, help tips, and its mobile+desktop E2E.

## 7. E2E test overhaul (Phase 6) — the core gate

Extend `tests/E2ETests` to drive the **entire** UI like a real user, **mobile-first**, catching every
defect. This is mandatory and blocking.

### 7.1 Device matrix

- Parameterize `AppFixture` contexts with Playwright device descriptors: **iPhone-class**,
  **Pixel-class** (Chromium `isMobile`+touch), a **small phone (360px)**, a **tablet**, and
  **desktop**. Run the smoke + journey suites across the matrix.
- Add explicit **breakpoint tests** at 320 / 375 / 414 / 768 / 1280 / 1920.

### 7.2 What every test asserts (per page, per device)

- Renders without Blazor error UI / 5xx (existing smoke, extended to mobile viewports).
- **No horizontal overflow** (`scrollWidth <= clientWidth`), no element wider than viewport.
- Drawer closed on mobile; bottom-nav visible on mobile; hamburger opens/closes drawer.
- All **primary actions reachable** (visible, ≥44px touch target, tappable) on mobile.
- Tables collapse to cards on `Xs`; `DataLabel`s visible.
- Dialogs open full-screen on mobile, are scrollable, submit and close.
- **Tooltips/help** present and openable (tap on mobile) for key controls.
- Loading/empty/error states render.

### 7.3 Realistic user journeys (behave like an actual user)

- Full flows end-to-end **on a phone**: log in → create cBot → set params (dialog) → run/backtest →
  view instance → stop; onboard trading account; create copy profile; create prop-firm challenge;
  use the AI assistant; change a setting; log out. Include **edge/unhappy paths**: invalid form input,
  network hiccup, empty lists, permission-denied (viewer vs admin vs owner), long text overflow,
  rotend device, back-button, deep-link to each route while unauthenticated → redirect to login.
- `PageSmokeTests`: keep the "every static route" contract; **add every new route**; run it on the
  mobile matrix too.

### 7.4 Added test dimensions

- **Visual regression**: baseline screenshots per key page × device; fail on unexpected diff (catches
  "tiny" layout breaks the request calls out). Store baselines; review on intentional change.
- **Accessibility**: axe-core (via injected script) on each page — no critical a11y violations; focus
  order, labels, contrast (contrast especially since theme is white-labelable).
- **White-label test**: boot with an alternate branding config; assert tokens/logo/colors applied and
  manifest reflects them.
- **PWA test**: assert manifest served with non-empty icons, service worker registers, app-shell
  cached, offline fallback shown when circuit dropped, `apple-touch-icon` present.

### 7.5 Fixture work

- `AppFixture`: helper to create device-emulated authed contexts; per-role login helpers
  (owner/admin/viewer); a "go offline" helper (CDP) for PWA tests; screenshot/axe helpers.
- Keep CI stable: emulation is the fast inner loop; document (in the guidelines) that release-gating on
  real iOS Safari needs a device cloud — emulated WebKit ≠ mobile Safari (known fidelity gap).

## 8. Docs, mandates, guidelines, README (Phase 7)

1. **New permanent doc `docs/ui-guidelines.md`** — the mandatory UI design system Claude follows for
   **every** new/changed UI: mobile-first rule, token usage (no hard-coded colors), breakpoint policy,
   responsive table→card pattern, dialog-on-mobile rule, touch-target minimums, help-tip requirement,
   white-label rule, motion/reduced-motion, accessibility baseline, and the E2E-per-UI requirement
   (device matrix, overflow assertion, journey + visual + a11y). Include a checklist copy-pasteable
   into PRs.
2. **`CLAUDE.md` (project) + `~/.claude-me/CLAUDE.md`** — add binding mandates:
   - *Mobile-first UI*: every new/changed page authored phone-first, responsive to 320px, no h-scroll.
   - *Help text*: every new control ships a `HelpTip`/help text sourced from docs.
   - *White-label*: no hard-coded colors/radii — tokens only.
   - *PWA*: new static routes added to smoke + manifest scope respected.
   - *E2E mobile*: every user-facing change ships a Playwright test on the **mobile** matrix
     (extends the existing "Playwright E2E mandatory" rule), plus visual + a11y where applicable.
   - Point all of the above at `docs/ui-guidelines.md` as the source of truth.
3. **Update `docs/features/*.md`** for every page whose UX changes; add a `docs/features/pwa.md` and
   `docs/features/theming-white-label.md` (or extend existing) covering install + branding tokens.
4. **README revamp** — make it beautiful and standout:
   - Hero: logo/banner, tagline, badges (build, license, .NET 10), animated/screenshot GIFs of the app
     on **mobile + desktop**, install-as-app callout.
   - Sections: what it is, feature highlights (with icons), screenshots gallery, quick start, white-
     label showcase, architecture diagram, links to docs. Modern, scannable, "exotic and unique" feel.
   - Keep it honest and in sync with the app.

## 9. Rollout sequence & definition of done

**Phases (each merges green, tests passing):**

1. Design foundation — tokens + white-label wiring + mobile-first layout shell (bottom nav, drawer
   default-closed on mobile).
2. Responsive component/pattern library (tables→cards, mobile dialogs, states, `HelpTip`).
3. PWA — manifest fix + icons + service worker + install prompt + iOS.
4. Inline help registry + tips across all controls.
5. Page-by-page overhaul (Login first), each with its mobile E2E.
6. E2E overhaul — device matrix, journeys, visual regression, a11y, white-label + PWA tests.
7. Docs + CLAUDE mandates + `ui-guidelines.md` + README.

**Definition of done (all must hold):**

- [ ] Every page usable and correct at 320–1920px; **zero horizontal overflow** on any device.
- [ ] Drawer closed + bottom nav on mobile; every primary action reachable with ≥44px touch targets.
- [ ] Every table collapses to cards on phones; every dialog full-screen on phones.
- [ ] Every control has a working tooltip/help sourced from docs (tap-openable on mobile).
- [ ] App is **installable** (valid manifest + icons + service worker), branded per tenant, with an
      offline fallback page; `apple-touch-icon` + iOS splash present.
- [ ] All colors/radii/typography come from white-label tokens — no hard-coded values in components.
- [ ] Login redesigned, on-brand, accessible.
- [ ] `tests/E2ETests` covers **every** page/dialog/action on the **mobile** matrix + tablet/desktop,
      with realistic journeys, edge cases, per-role paths, visual regression, and a11y; `PageSmokeTests`
      updated with all routes; `dotnet test` green.
- [ ] `docs/ui-guidelines.md` created; both `CLAUDE.md` files updated with mobile-first / help / white-
      label / PWA / mobile-E2E mandates; feature docs synced; README revamped and beautiful.
- [ ] Rider `get_file_problems` clean on every touched `.cs`/`.razor`; `dotnet format analyzers` clean
      on touched projects; reviewer pass done.

## 10. Risks / notes

- **Blazor Server offline** is fundamentally limited (SignalR circuit) — deliver installable app-shell
  + offline fallback; log "true offline / WASM-hybrid" as a separate future epic, don't over-promise.
- **Monaco on mobile** is awkward — provide a guarded/fallback editing experience, don't pretend the
  full IDE works on a phone.
- **Emulated WebKit ≠ mobile Safari** — E2E emulation catches layout; real-device gating (device cloud)
  noted in guidelines for release confidence.
- **Visual-regression flakiness** — pin fonts, disable animations in test mode, mask volatile regions
  (timestamps, live data) to keep baselines stable.
- **White-label contrast** — because tenants pick colors, a11y contrast checks must run against the
  active theme, and the token system should warn/guard on low-contrast combos.

### Sources (research grounding)

- [MudBlazor — Table (responsive / DataLabel / Breakpoint)](https://mudblazor.com/components/table)
- [MudBlazor — Breakpoints](https://mudblazor.com/features/breakpoints)
- [Microsoft Learn — Blazor PWA](https://learn.microsoft.com/en-us/aspnet/core/blazor/progressive-web-app/)
- [Customizing PWA manifest & icons](https://codingwithdavid.blogspot.com/2025/02/customizing-pwa-manifest-and-icons-for.html)
- [Playwright .NET — Emulation](https://playwright.dev/dotnet/docs/emulation)
- [Playwright mobile testing guide (2026)](https://testdino.com/blog/playwright-mobile-testing)
