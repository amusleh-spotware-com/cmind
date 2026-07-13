# Full-Feature Live Audit — Findings

> **Audit-only. No code was changed during this audit.** Fixes + regression tests in the follow-up fix plan.
> AI lane ran against current fake (FakeLocalLlmServer) — no `AI_E2E_*` vars set.
> Live creds: 2 cIDs (`amusleh`, `afhacker`), token cache present.

## Summary Table

> **71 bugs total. 0 blockers. 16 high. 29 medium. 26 low.**
> Cross-cutting themes: (1) Localization gap — 6 lanes have unscanned pages with zero `@L[]`; (2) Missing confirmation dialogs on destructive actions (A-06, B-03, D-05, D-06); (3) CSP/Google Fonts blocked site-wide (C-04, D-07, I-01 — same root cause); (4) `AiFeatureNotice` hardcodes "Anthropic API key" and fires on non-AI pages (E-01..E-03, G-03).

| Lane | Routes Covered | Bugs | Blocker | High | Medium | Low |
|------|---------------|------|---------|------|--------|-----|
| A — Auth/Identity | 7 + mobile + RTL | 8 | 0 | 2 | 4 | 2 |
| B — cBots Lifecycle | 9 routes + InstanceTable + dialogs | 9 | 0 | 2 | 4 | 3 |
| C — Copy Trading / Open API | `/accounts`, `/copy-trading`, `/settings/openapi`, OAuth callback, 3 API endpoints | 7 | 0 | 3 | 2 | 2 |
| D — Prop Firm / Guard / Compliance | `/prop-firm`, `/prop-guard`, `/settings/legal` + mobile + RTL | 11 | 0 | 2 | 6 | 3 |
| E — AI features / Agent | 14 routes (`/ai/*`, `/agent`, `/agent-studio`, `/alerts`, `/mcp`, `/settings/ai`) | 9 | 0 | 2 | 4 | 3 |
| F — Quant / Journal | 8 routes (`/quant/*`, `/journal`) | 8 | 0 | 2 | 3 | 3 |
| G — Calendar / Currency Strength | 4 routes + mobile + RTL | 6 | 0 | 1 | 3 | 2 |
| H — Settings / White-label / Toggles | 4 routes + NodesUi modes | 5 | 0 | 1 | 2 | 2 |
| I — Dashboard / Shell / PWA / Mobile | 40 routes full smoke + mobile + RTL + PWA + axe | 7 | 0 | 1 | 3 | 3 |
| **TOTAL** | **40+ routes, all PageSmokeTests routes covered** | **71** | **0** | **16** | **29** | **26** |

---

## Deferred Infrastructure Changes

### DEF-01 — Replace FakeLocalLlmServer with Microsoft.Extensions.AI test IChatClient
- **User directive:** swap AI E2E fake from `FakeLocalLlmServer` (HTTP server) to
  `Microsoft.Extensions.AI` (+ `Microsoft.Extensions.AI.Abstractions`) test/fake `IChatClient`
  wired behind `IAiClient`/`AiLocalFixture` seam.
- **CLAUDE.md impact:** mandate 2 + `multi-provider-ai` docs need updating to match.
- **No edit made during audit.** Implement in fix plan.

---

## Lane A — Auth, Access & Identity

Routes: `/login`, `/register`, 2FA challenge, `/users`, `/account`, password/`MustChangePassword`, `/set-culture` + localization + RTL

<!-- Lane A findings appended below -->

### A-01 — Pending/inactive user login shows no error message
- **Severity:** high
- **Route / feature:** `/login` — inactive-account login flow
- **State:** self-registered account with `ActivationState = PendingApproval` or `PendingEmailVerification`
- **Live or fake:** n/a
- **Steps to reproduce:**
  1. Create a self-registered user account (or manually set `ActivationState` != `Active`).
  2. Attempt to log in with that account's credentials on `/login` (form POST).
  3. The backend redirects to `/login?pending=1`.
- **Observed:** The login page renders normally — no error message, no indication of why login failed. The `HasError` property only checks for the `?error=` query param; `?pending=1` is silently ignored. The user sees the login form again with no feedback.
- **Expected:** A clear, user-facing message explaining the account is pending activation or awaiting approval.
- **Evidence:** `src/Web/Endpoints/AuthEndpoints.cs:60` redirects to `/login?pending=1`; `src/Web/Components/Pages/Login.razor:126-127` only reads `[SupplyParameterFromQuery(Name = "error")]` — the `pending` param is never consumed.
- **Suspected root cause (read-only lead):** `Login.razor` missing `[SupplyParameterFromQuery(Name = "pending")]` and corresponding UI branch. `AuthEndpoints.cs:60`.
- **Which tier SHOULD catch it:** E2E — a test that registers a user, does NOT approve, then attempts login and asserts a visible pending-notice element.
- **Regression-test sketch:** `Assertions.Expect(page.Locator("[data-testid=login-pending]")).ToBeVisibleAsync()` after submitting a pending account's credentials.

---

### A-02 — `Login.razor` uses `DateTime.UtcNow` in copyright footer (violates mandate 4)
- **Severity:** medium
- **Route / feature:** `/login` — copyright year in footer
- **State:** any
- **Live or fake:** n/a
- **Steps to reproduce:**
  1. Open `/login` in any browser.
  2. Inspect the copyright line at the bottom of the login card.
- **Observed:** `&copy; @DateTime.UtcNow.Year` — uses the real system clock directly.
- **Expected:** `TimeProvider` injected and `GetUtcNow().Year` used; or a constant for a static UI element.
- **Evidence:** `src/Web/Components/Pages/Login.razor:95`.
- **Suspected root cause (read-only lead):** `Login.razor` has no `TimeProvider` injection; line 95 calls `DateTime.UtcNow.Year` directly.
- **Which tier SHOULD catch it:** Unit — `NoHardcodedUiTextTests` scope already finds other violations; a complementary `NoDateTimeUtcNowTests` scanner over `.razor` files would catch this.
- **Regression-test sketch:** `ScanRazorFiles().Should().NotContain(f => f.Contains("DateTime.UtcNow") || f.Contains("DateTime.Now"))`.

---

### A-03 — `MfaFlowTests.Profile_shows_two_factor_section_and_qr_on_mobile` E2E test fails
- **Severity:** high
- **Route / feature:** `/account` → Enable two-factor dialog — mobile viewport
- **State:** mobile (iPhone 13 emulation, 390px)
- **Live or fake:** n/a
- **Steps to reproduce:**
  1. Run `dotnet test tests/E2ETests --filter "MfaFlowTests"`.
  2. `Profile_shows_two_factor_section_and_qr_on_mobile` times out after 30 s.
- **Observed:** `TimeoutException: Timeout 30000ms exceeded` waiting for `[data-testid=mfa-qr] svg` to be visible. The test calls `page.ClickAsync("[data-testid=mfa-enable-open]")` then waits for `[data-testid=mfa-qr] svg` via `page.WaitForSelectorAsync`.
- **Expected:** The `EnableTwoFactorDialog` opens successfully on mobile and the QR SVG renders inside it.
- **Evidence:** `tests/E2ETests/MfaFlowTests.cs:64`; `src/Web/Components/Dialogs/EnableTwoFactorDialog.razor` — the QR is inside `MudDialog`; on mobile MudBlazor may render the dialog off-screen or in a portal that the selector doesn't find. Test run output shows failure. Alternatively the dialog may not open because the button click didn't register on the mobile emulation.
- **Suspected root cause (read-only lead):** `MfaFlowTests.cs:64` — the test uses `WaitForSelectorAsync` (strict DOM wait) instead of `WaitForAsync` on a Locator that accounts for the dialog portal. On mobile viewports MudBlazor dialogs render in a `.mud-overlay` portal; the dialog might open but the SVG element inside is not immediately in the `[data-testid=mfa-qr]` scope until the API call resolves (`/api/auth/mfa/setup`).
- **Which tier SHOULD catch it:** E2E (this IS the E2E test that catches it — it is currently failing).
- **Regression-test sketch:** `await Assertions.Expect(page.Locator("[data-testid=mfa-qr] svg")).ToBeVisibleAsync(new() { Timeout = 15000 })` — use Playwright Assertions with timeout instead of raw `WaitForSelectorAsync`.

---

### A-04 — `Account.razor` and `Users.razor` pages have zero localization (`@L["key"]`) — not in the hardcoded-text gate scope
- **Severity:** medium
- **Route / feature:** `/account`, `/users` — all user-facing strings
- **State:** any locale (switch to Arabic/German/etc.)
- **Live or fake:** n/a
- **Steps to reproduce:**
  1. Navigate to `/set-culture?culture=ar&redirectUri=/account`.
  2. Observe the account page.
  3. Navigate to `/users`.
- **Observed:** All headings, labels, button text, and alert text on both pages remain in English regardless of the selected locale. Examples: "Change Password", "Current", "New", "Update", "Logout", "Two-factor authentication", "Enable two-factor", "Turn off", "Regenerate backup codes", "Install app", "Users", "Email", "Role", "Locked", "New User", "Reset password", etc.
- **Expected:** Every user-facing string uses `@inject IStringLocalizer<Ui> L` and `@L["key"]` lookups, rendering in the active locale per mandate 9.
- **Evidence:** `src/Web/Components/Pages/Account.razor` — zero `@L[` references; `src/Web/Components/Pages/Users.razor` — zero `@L[` references. The `NoHardcodedUiTextTests.MigratedRazorFiles()` list does not include either page, so the build gate does not catch these.
- **Suspected root cause (read-only lead):** Pages were written before the i18n mandate was added and were never migrated into `MigratedRazorFiles`. Also `EnableTwoFactorDialog.razor`, `DisableTwoFactorDialog.razor`, `RegenerateBackupCodesDialog.razor`, `NewUserDialog.razor` all have unlocalized strings.
- **Which tier SHOULD catch it:** Unit — `NoHardcodedUiTextTests` should include these files; the gate is present but the file list is incomplete.
- **Regression-test sketch:** Add `"Pages/Account.razor"`, `"Pages/Users.razor"`, and the 2FA dialogs to `MigratedRazorFiles()` — the test will then fail until all strings are localized.

---

### A-05 — Users table `isLockedOut` column only reflects permanent lockout, not time-based lockout
- **Severity:** medium
- **Route / feature:** `/users` — "Locked" column in user table
- **State:** user locked via failed login attempts (time-based: `LockoutEnd` set, `IsLockedOut=false`)
- **Live or fake:** n/a
- **Steps to reproduce:**
  1. As a non-owner user, fail login 5 times within the lockout window.
  2. As owner, navigate to `/users`.
  3. Check the "Locked" column for that user.
- **Observed:** The `GET /api/users/` endpoint returns `u.IsLockedOut` — the persisted boolean that is only set to `true` by a permanent admin lock, never by the automatic failed-login time-based lockout (`LockoutEnd`). A user locked out by too many failed attempts shows `false` in the "Locked" column.
- **Expected:** The column should reflect `IsCurrentlyLockedOut(now)` — i.e., either permanently locked (`IsLockedOut=true`) or temporarily locked (`LockoutEnd > now`).
- **Evidence:** `src/Web/Endpoints/UserEndpoints.cs:16` — `.Select(u => new { u.Id, u.Email, Role = u.RoleName, u.IsLockedOut, u.CreatedAt })`; `src/Web/Components/Pages/Users.razor:26` — `@context.isLockedOut`; `src/Core/Entities.cs:122` — `IsCurrentlyLockedOut` includes `LockoutEnd` check.
- **Suspected root cause (read-only lead):** The endpoint projection uses `u.IsLockedOut` (EF-mapped property) rather than computing `IsCurrentlyLockedOut`. In EF Core, `IsCurrentlyLockedOut(now)` can't be directly translated to SQL, but the endpoint could project `LockoutEnd` as well and let the client display "locked until …".
- **Which tier SHOULD catch it:** Integration — a test that fails login N times, then fetches `/api/users/` and asserts the user's lockout indicator reflects the real locked state.
- **Regression-test sketch:** `var users = await http.GetFromJsonAsync<...>("/api/users/"); users.Single(u => u.Email == lockedEmail).IsLockedOut.Should().BeTrue()`.

---

### A-06 — `Remove` (delete user) on `/users` fires immediately with no confirmation dialog
- **Severity:** medium
- **Route / feature:** `/users` — Delete user icon button
- **State:** any user in the table
- **Live or fake:** n/a
- **Steps to reproduce:**
  1. Navigate to `/users` as owner.
  2. Click the red delete icon (🗑) next to any user row.
- **Observed:** The user is deleted immediately — HTTP `DELETE /api/users/{id}` fires on the first click, no confirmation dialog, no undo. The row disappears from the table.
- **Expected:** Per mandate 7, a destructive action opens a MudBlazor confirmation dialog first. The admin must confirm before the user is deleted.
- **Evidence:** `src/Web/Components/Pages/Users.razor:94` — `private async Task Remove(Guid id) { await Http.DeleteAsync($"/api/users/{id}"); await Load(); }` — no `Dialogs.ShowAsync<ConfirmDialog>` call.
- **Suspected root cause (read-only lead):** The `Remove` method was kept simple and no confirmation dialog component was wired up.
- **Which tier SHOULD catch it:** E2E — a test that clicks the delete icon and asserts a confirmation dialog appears before the DELETE request fires.
- **Regression-test sketch:** `await page.ClickAsync("[aria-label='Delete user']"); await Assertions.Expect(page.Locator(".mud-dialog")).ToBeVisibleAsync();`.

---

### A-07 — Reset password temp password shown only in auto-dismissing snackbar (one-shot, no copy)
- **Severity:** low
- **Route / feature:** `/users` — Reset password icon button
- **State:** any user in the table
- **Live or fake:** n/a
- **Steps to reproduce:**
  1. Navigate to `/users` as owner.
  2. Click the "Reset password" icon (🔑) next to any user.
  3. If the admin misses the snackbar (or it auto-closes in ~3 s), the temp password is lost.
- **Observed:** `Snack.Add($"Temp password: {pw}", Severity.Info)` — a MudBlazor info snackbar with no copy button and default auto-close. If the admin doesn't immediately copy it, it vanishes and there is no recovery path other than resetting again.
- **Expected:** The generated temp password should be shown in a dialog with a copy-to-clipboard button and persist until the admin dismisses it. A snackbar for a sensitive credential that has no undo is unacceptable UX.
- **Evidence:** `src/Web/Components/Pages/Users.razor:89-90`.
- **Suspected root cause (read-only lead):** The reset flow was implemented inline rather than as a dialog.
- **Which tier SHOULD catch it:** E2E — assert that after clicking reset, a dialog (not only a snackbar) containing the new password is visible and has a copy button.
- **Regression-test sketch:** `await page.ClickAsync("[aria-label='Reset password']"); await Assertions.Expect(page.Locator(".mud-dialog [data-testid=temp-password]")).ToBeVisibleAsync();`.

---

### A-08 — `MustChangePassword` flag set on password-reset but never enforced in the UI
- **Severity:** low
- **Route / feature:** `/account` — forced password change after admin reset
- **State:** user whose password was reset by admin (`MustChangePassword=true`)
- **Live or fake:** n/a
- **Steps to reproduce:**
  1. Admin resets a user's password via `/users` → Reset icon.
  2. The user logs in with the temp password.
  3. The user is NOT redirected to `/account` or shown any "you must change your password" notice.
  4. The user can navigate the full application without changing their password.
- **Observed:** `user.ResetPassword(...)` sets `MustChangePassword = true` on the entity. The `/api/auth/login` response returns `{ MustChangePassword: true }` for a form login — but only the API JSON path returns this; the form-submit login redirects to `/` without any enforcement. No Blazor middleware, route guard, or `MainLayout` check reads `MustChangePassword`.
- **Expected:** A user with `MustChangePassword=true` should be intercepted after login and forced to change their password before accessing any other page.
- **Evidence:** `src/Core/Entities.cs:134` — `MustChangePassword = true`; `src/Web/Endpoints/AuthEndpoints.cs:83` — API path returns flag; no Blazor component or middleware reads or enforces it.
- **Suspected root cause (read-only lead):** The enforcement step (a redirect/guard) was not implemented; CLAUDE.md notes it as "password reset stays manual via `MustChangePassword`" but does not mark it deliberately not done in the enforcement sense.
- **Which tier SHOULD catch it:** E2E — reset a user's password, log in as that user, navigate to `/cbots`, assert either a redirect to `/account` or a visible "please change your password" banner.
- **Regression-test sketch:** `await page.GotoAsync("/cbots"); page.Url.Should().Contain("/account", "a must-change-password user must not reach other pages");`.

---

## Lane B — cBots Lifecycle

Routes: `/cbots`, `/run`, `/backtest`, `/optimize`, `/nodes`, `InstanceDetail`, `BuilderEditor`, `InstanceTable`, `AssistantBuildBot`

<!-- Lane B findings appended below -->

### B-01 — InstanceDetail shows raw Guid in page heading
- **Severity:** medium
- **Route / feature:** `/instance/{Id:guid}`
- **State:** any (running, terminal, or missing)
- **Live or fake:** n/a
- **Steps to reproduce:** 1. Navigate to `/run`. 2. Click the eye icon on any instance row. 3. Observe the page heading.
- **Observed:** Line 20 of InstanceDetail.razor renders the raw GUID directly as the primary heading via `Instance @Id`.
- **Expected:** Human-readable label (cBot name + symbol/timeframe). Mandate 11: No internal identifiers in the UI.
- **Evidence:** `src/Web/Components/Pages/InstanceDetail.razor:20`
- **Suspected root cause (read-only lead):** The heading binds the route Guid param directly; `_detail` fields `cBot`, `symbol`, `timeframe` are available but never used in the heading.
- **Which tier SHOULD catch it:** E2E
- **Regression-test sketch:** `(await page.Locator("h5").InnerTextAsync()).Should().NotMatchRegex("[0-9a-f]{8}-[0-9a-f]{4}-")`

### B-02 — InstanceDetail renders blank page for a non-existent/missing instance ID
- **Severity:** high
- **Route / feature:** `/instance/{Id:guid}`
- **State:** missing entity
- **Live or fake:** n/a
- **Steps to reproduce:** 1. Navigate to `/instance/00000000-0000-0000-0000-000000000000`. 2. Observe rendered output.
- **Observed:** API returns 404; `_detail = null`; `OnInitializedAsync` returns early. Page renders only the heading and an empty log panel with no "not found" notice and no back-link. Circuit stays up but page is blank.
- **Expected:** Clear "Instance not found" notice with a back link. Mandate 11: detail pages must render for missing entities.
- **Evidence:** `src/Web/Components/Pages/InstanceDetail.razor:22` — `@if (_detail is not null)` with no `@else` block.
- **Suspected root cause (read-only lead):** Missing `@else` branch in the template.
- **Which tier SHOULD catch it:** E2E
- **Regression-test sketch:** Navigate to a garbage Guid; assert `[data-testid='instance-not-found']` is visible.

### B-03 — InstanceTable Delete fires immediately with no confirmation dialog
- **Severity:** high
- **Route / feature:** `/run`, `/backtest` — InstanceTable Delete icon
- **State:** any terminal instance
- **Live or fake:** n/a
- **Steps to reproduce:** 1. Navigate to `/run`. 2. Click the red Delete icon on a terminal row. 3. Row disappears immediately.
- **Observed:** `Delete(Guid id)` calls `Http.DeleteAsync` directly with no confirmation dialog. Instance record, logs, and equity history are destroyed with one click.
- **Expected:** MudBlazor confirmation dialog before DELETE, matching `CBots.razor` lines 284-289.
- **Evidence:** `src/Web/Components/Pages/InstanceTable.razor:58` — no `IDialogService` injected.
- **Suspected root cause (read-only lead):** `IDialogService` was never injected.
- **Which tier SHOULD catch it:** E2E
- **Regression-test sketch:** Click `[data-testid='instance-delete']`; assert `.mud-dialog` is visible before any row count changes.

### B-04 — InstanceTable Stop and Delete give no user feedback
- **Severity:** medium
- **Route / feature:** `/run`, `/backtest` — InstanceTable Stop and Delete
- **State:** any
- **Live or fake:** n/a
- **Steps to reproduce:** 1. Click Stop on a running instance. 2. Observe: table reloads silently.
- **Observed:** `Stop` and `Delete` methods fire HTTP call and reload but never call `ISnackbar`. API errors are swallowed silently.
- **Expected:** Success and error snackbars matching every other action in the app.
- **Evidence:** `src/Web/Components/Pages/InstanceTable.razor:57-58` — no `@inject ISnackbar`.
- **Suspected root cause (read-only lead):** `ISnackbar` was never injected.
- **Which tier SHOULD catch it:** E2E
- **Regression-test sketch:** Click Stop; assert `.mud-snackbar` is visible.

### B-05 — BacktestDialog allows null From/To dates to be submitted
- **Severity:** medium
- **Route / feature:** `/backtest` — BacktestDialog date pickers
- **State:** user clears both dates before submitting
- **Live or fake:** n/a
- **Steps to reproduce:** 1. Open "Backtest New cBot" dialog. 2. Clear From and To pickers. 3. Fill cBot and account. 4. Click Start backtest.
- **Observed:** `Submit()` does not gate on `_from`/`_to`. Dialog closes and parent sends null dates. Backend `FormatBacktestDate` returns empty string for Null JSON, adding `--start ""` and `--end ""` to CLI args. cTrader Console rejects these, producing a Failed instance with an opaque error.
- **Expected:** Submit blocked when either date is null.
- **Evidence:** `BacktestDialog.razor:79-81`; `ContainerCommandHelpers.cs:108-116`
- **Suspected root cause (read-only lead):** Missing `|| _from is null || _to is null` guards in `Submit()`.
- **Which tier SHOULD catch it:** unit + E2E
- **Regression-test sketch:** Unit: `BuildConsoleArgsList` must never emit `--start ""`; E2E: submit with cleared dates, assert validation error.

### B-06 — InstanceTable Load() has no error handling — API error crashes the component
- **Severity:** medium
- **Route / feature:** `/run`, `/backtest` — InstanceTable
- **State:** transient API error (5xx, network blip, token expiry)
- **Live or fake:** n/a
- **Steps to reproduce:** 1. Navigate to `/run`. 2. Server returns 500 on `/api/instances/`. 3. Observe page.
- **Observed:** `Load()` calls `GetDynamicArrayAsync` without try/catch. `GetDynamicArrayAsync` throws `HttpRequestException` on non-2xx. Unhandled exception trips the ErrorBoundary on the Run and Backtest pages.
- **Expected:** `Load()` catches exception, sets `_items = []`, optionally shows snackbar — matching `Run.razor OnInitializedAsync`.
- **Evidence:** `src/Web/Components/Pages/InstanceTable.razor:38-41` — no try/catch.
- **Suspected root cause (read-only lead):** Error handling omitted when the component was authored.
- **Which tier SHOULD catch it:** integration
- **Regression-test sketch:** Mock 500 on `/api/instances/`; assert `.blazor-error-ui` is not visible.

### B-07 — cBot-lifecycle pages have hardcoded English strings not gated by the localization test
- **Severity:** low
- **Route / feature:** `/run`, `/backtest`, `/instance/{id}`, `/cbots`, InstanceTable, `/nodes`
- **State:** locale=ar
- **Live or fake:** n/a
- **Steps to reproduce:** 1. Switch locale to Arabic. 2. Navigate to any cBot-lifecycle page.
- **Observed:** Strings like "Run cBot", "Backtest cBot", "Instances", "View instance", "Stop instance", "No cBots yet.", "Build", "Edit", "Run", "Delete", "Status:", "Symbol:", "TF:", "Build Result", "Run Logs" all render in English. None of these files inject `IStringLocalizer<Ui>`. `NoHardcodedUiTextTests.MigratedRazorFiles()` covers only five shell/layout files.
- **Expected:** All user-facing strings via `@L["key"]` in all 22 locales.
- **Evidence:** `CBots.razor`, `Run.razor`, `Backtest.razor`, `InstanceTable.razor`, `InstanceDetail.razor`, `Nodes.razor` — no `IStringLocalizer<Ui>`; `NoHardcodedUiTextTests.cs MigratedRazorFiles()` omits all of them.
- **Suspected root cause (read-only lead):** Localization migration reached layout files but not feature pages.
- **Which tier SHOULD catch it:** unit
- **Regression-test sketch:** Add `"Pages/CBots.razor"` etc. to `MigratedRazorFiles()`.

### B-08 — /instance/{id} and /builder/{id} have no smoke, mobile, or overflow E2E coverage
- **Severity:** low
- **Route / feature:** InstanceDetail (`/instance/{Id:guid}`), BuilderEditor (`/builder/{Id:guid}`)
- **State:** any
- **Live or fake:** n/a
- **Steps to reproduce:** 1. Search `PageSmokeTests.Routes()` for `/instance/` or `/builder/`. 2. Search E2E files for `instance-stop`, `instance-delete`, `instance-view` testids.
- **Observed:** `RouteCoverageTests` excludes parameterized routes noting "covered by their own feature E2E" but no such feature E2E covers non-existent/terminal states, mobile layout, or lifecycle-control disabled-state assertions. The testids `instance-stop`, `instance-delete`, `instance-view` exist but no E2E file references them.
- **Expected:** `InstanceDetailTests` covering running, terminal, and missing states; mobile/overflow assertions for both pages.
- **Evidence:** `tests/E2ETests/PageSmokeTests.cs:15` — absent. `tests/E2ETests/MobileLayoutTests.cs:14-28` — absent. No E2E file uses the instance testids.
- **Suspected root cause (read-only lead):** Tests deferred; RouteCoverageTests justification never fulfilled.
- **Which tier SHOULD catch it:** E2E
- **Regression-test sketch:** Navigate to `/instance/{knownTerminalId}`; assert `.blazor-error-ui` is not visible.

### B-09 — CBots list quick-run ignores returned InstanceId — no navigation to new instance
- **Severity:** low
- **Route / feature:** `/cbots` — row Run (Play) icon
- **State:** run succeeds
- **Live or fake:** n/a
- **Steps to reproduce:** 1. Navigate to `/cbots`. 2. Click Play on a cBot row. 3. "Started" snackbar appears. 4. User stays on `/cbots` with no link to the new instance.
- **Observed:** `CBots.razor Run()` receives `QuickRunResponse` containing `InstanceId` but ignores it. User must manually navigate to `/run`.
- **Expected:** After "Started", navigate to `/run` or `/instance/{instanceId}`.
- **Evidence:** `src/Web/Components/Pages/CBots.razor:269-275` — `d.InstanceId` populated but `Nav.NavigateTo` never called.
- **Suspected root cause (read-only lead):** Navigation omitted when quick-run was wired up.
- **Which tier SHOULD catch it:** E2E
- **Regression-test sketch:** `await page.WaitForURLAsync("**/run**")` after clicking Play on a cBot row.




---

## Lane C — Copy Trading & Open API Accounts

Routes: `/copy-trading`, `/accounts`, `TradingAccountList`, `/settings/openapi`, `OpenApiApplications`, OAuth invite/callback

<!-- Lane C findings appended below -->

### C-01 — POST /api/ctids/{id}/accounts returns HTTP 500 for missing Broker field (DomainException unhandled)
- **Severity:** high
- **Route / feature:** `POST /api/ctids/{id}/accounts`
- **State:** populated (cID exists, missing required field)
- **Live or fake:** n/a
- **Steps to reproduce:**
  1. Create a cID via `POST /api/ctids`.
  2. `POST /api/ctids/{id}/accounts` with body `{"accountNumber":9999999,"isLive":false}` — omit the `Broker` field.
- **Observed:** HTTP 500 — unhandled `DomainException: domain.account.broker_name_required` propagates to the ASP.NET runtime error page. Full stack trace exposed in the response body in Development.
- **Expected:** HTTP 400 (or 422) with a user-readable validation message. `DomainException` signals a domain rule violation — the endpoint catches `DomainErrors.BrokerNotAllowed` but silently misses `BrokerNameRequired`, which bubbles as 500.
- **Evidence:** `tmp-audit/C/` (API test)
- **Suspected root cause (read-only lead):** `src/Web/Endpoints/CtidEndpoints.cs` line ~102 — the `try/catch` only catches `DomainException` when `ex.Code == DomainErrors.BrokerNotAllowed`; no handler for `BrokerNameRequired` (or a global `DomainException → 400` middleware).
- **Which tier SHOULD catch it:** integration
- **Regression-test sketch:** `POST /api/ctids/{id}/accounts` omitting Broker → assert HTTP 400, not 500.

### C-02 — POST /api/ctids with duplicate username returns HTTP 500 (should be 409 Conflict)
- **Severity:** high
- **Route / feature:** `POST /api/ctids`
- **State:** populated (username already exists for user)
- **Live or fake:** n/a
- **Steps to reproduce:**
  1. Create a cID: `POST /api/ctids {"username":"audit-cid-x","password":"AuditPass123!"}` → 200 OK.
  2. Repeat the same request with the same username.
- **Observed:** HTTP 500 — `Microsoft.EntityFrameworkCore.DbUpdateException` wrapping `Npgsql.PostgresException: 23505: duplicate key value violates unique constraint "IX_CTids_UserId_Username"` surfaces as a 500. Full stack trace exposed to the client.
- **Expected:** HTTP 409 Conflict with message "A cID account with this username already exists." The unique constraint is a predictable, expected condition and must be caught and converted to a 4xx.
- **Evidence:** `tmp-audit/C/` (API test)
- **Suspected root cause (read-only lead):** `src/Web/Endpoints/CtidEndpoints.cs` — the `POST /` handler has no `try/catch` for `DbUpdateException`; no global EF constraint → 409 handler middleware.
- **Which tier SHOULD catch it:** integration
- **Regression-test sketch:** `POST /api/ctids` twice with same username → second call asserts HTTP 409.

### C-03 — POST /api/ctids/{id}/accounts with duplicate account number returns HTTP 500 (should be 409)
- **Severity:** high
- **Route / feature:** `POST /api/ctids/{id}/accounts`
- **State:** populated (account number already added to cID)
- **Live or fake:** n/a
- **Steps to reproduce:**
  1. Add a trading account: `POST /api/ctids/{id}/accounts {"accountNumber":3635817,"broker":"SpotwareDemo","isLive":false}`.
  2. Repeat the exact same request.
- **Observed:** HTTP 500 — `DbUpdateException` (unique constraint `IX_TradingAccounts_CTidId_AccountNumber`) exposed as 500 with full stack trace.
- **Expected:** HTTP 409 Conflict "Account 3635817 is already connected to this cID." Duplicate accounts are a predictable user mistake, not a server error.
- **Evidence:** `tmp-audit/C/` (API test)
- **Suspected root cause (read-only lead):** `src/Web/Endpoints/CtidEndpoints.cs` `MapPost("/{id}/accounts")` — only catches `DomainErrors.BrokerNotAllowed`; no handling for unique constraint violations.
- **Which tier SHOULD catch it:** integration
- **Regression-test sketch:** Add same account twice to a cID → assert HTTP 409 on second request.

### C-04 — CSP blocks Google Fonts stylesheet on every page (console error on all routes)
- **Severity:** medium
- **Route / feature:** all pages (App.razor loads Google Fonts)
- **State:** any
- **Live or fake:** n/a
- **Steps to reproduce:**
  1. Open browser devtools (or Playwright console capture).
  2. Navigate to any page (e.g. `/accounts`, `/copy-trading`).
- **Observed:** Console error on every page: `Loading the stylesheet 'https://fonts.googleapis.com/css2?family=Space+Grotesk...' violates the following Content Security Policy directive: "style-src 'self' 'unsafe-inline'"`. The CSP `style-src` policy does not include `https://fonts.googleapis.com` as a permitted source, yet `App.razor` links to it. In any CSP-enforcing browser/proxy/HSTS strict environment this blocks the branded font from loading.
- **Expected:** Either (a) `style-src` in `SecurityHeaders.cs` allows `https://fonts.googleapis.com https://fonts.gstatic.com`, or (b) the font files are self-hosted under `/fonts/` (no external dependency, more secure). Zero console errors on any page.
- **Evidence:** `tmp-audit/C/accounts-empty/01-accounts.png` (console capture)
- **Suspected root cause (read-only lead):** `src/Web/Security/SecurityHeaders.cs` line ~11 — `style-src 'self' 'unsafe-inline'` does not list `fonts.googleapis.com`; `src/Web/Components/App.razor` line ~20–22 loads the font externally.
- **Which tier SHOULD catch it:** E2E (CSP violation in console)
- **Regression-test sketch:** Navigate to `/`; assert zero `console.error()` messages matching `Content Security Policy`.

### C-05 — /copy-trading/{id} route returns 404 (no dedicated detail page; blank browser navigation)
- **Severity:** medium
- **Route / feature:** `/copy-trading/{profileId}`
- **State:** populated (valid profile ID)
- **Live or fake:** n/a
- **Steps to reproduce:**
  1. Create a copy profile. Get its ID.
  2. Navigate directly in browser (or share URL) to `/copy-trading/{profileId}`.
- **Observed:** HTTP 404 — the route does not exist. Profile detail is opened via a `MudBlazor` dialog triggered by clicking the table row in the `/copy-trading` page. Direct URL navigation to a specific profile (e.g. for deep-linking, sharing, bookmarking) returns 404. A 404 console error is also emitted.
- **Expected:** Either (a) a dedicated `/copy-trading/{id}` Blazor page renders the profile detail (preferred — enables deep-linking and browser back-button support), or (b) navigating to `/copy-trading/{id}` redirects to `/copy-trading` and opens the correct profile dialog automatically.
- **Evidence:** `tmp-audit/C/copy-detail/01-valid-profile.png`
- **Suspected root cause (read-only lead):** `src/Web/Components/Pages/CopyTrading.razor` — detail is an inline `CopyProfileDialog` via `IDialogService`; no `@page "/copy-trading/{Id:guid}"` route exists anywhere in the app.
- **Which tier SHOULD catch it:** E2E
- **Regression-test sketch:** Navigate to `/copy-trading/{profileId}` → assert 200 and profile name visible, not 404.

### C-06 — Copy profile detail is a MudBlazor dialog (confirm mandate 7 compliance — OK) but dialog does not trap keyboard focus correctly
- **Severity:** low
- **Route / feature:** `/copy-trading` — row-click dialog (`CopyProfileDialog`)
- **State:** populated
- **Live or fake:** n/a
- **Steps to reproduce:**
  1. Navigate to `/copy-trading` with at least one profile.
  2. Click a profile row to open the detail dialog.
  3. Open the source account MudSelect dropdown inside the dialog.
  4. Attempt to press Escape — the first Escape closes the dropdown but not the dialog; a second Escape closes the dialog itself.
  5. The `Cancel` button inside the dialog is obscured by the open `mud-overlay` from the dropdown popover and cannot be clicked while the dropdown is open.
- **Observed:** When the dropdown popover is open inside the dialog, a `mud-overlay` div intercepts pointer events — making the `Cancel` button unclickable. Users must close the dropdown first (click outside or Escape) before they can dismiss the dialog.
- **Expected:** The overlay should not block the dialog's own Cancel button; Cancel should be reachable at all times. This is a MudBlazor dialog + popover z-index interaction issue.
- **Evidence:** `tmp-audit/C/copy-trading/03-source-dropdown.png`
- **Suspected root cause (read-only lead):** MudBlazor `MudSelect`/`MudPopover` overlay sits above the dialog button bar; likely requires `PopoverOptions.ThemeScrollbarWidth` or a custom z-index fix.
- **Which tier SHOULD catch it:** E2E
- **Regression-test sketch:** Open New Profile dialog → open source dropdown → click Cancel → assert dialog dismissed without error.

### C-07 — Copy profile dialog destinations table shows raw `True`/`False` booleans (poor UX)
- **Severity:** low
- **Route / feature:** `/copy-trading` — `CopyProfileDialog` destinations summary table
- **State:** populated (profile with destinations)
- **Live or fake:** n/a
- **Steps to reproduce:**
  1. Create a copy profile with at least one destination account.
  2. Click the profile row to open the `CopyProfileDialog`.
  3. Observe the destinations table columns: Reverse, Partial, Pending, Trailing.
- **Observed:** The boolean columns render raw C# `True`/`False` strings (e.g. `Reverse: False`, `Partial: True`). The underlying template uses `@context.reverse`, `@context.mirrorPartialClose` etc. directly without formatting.
- **Expected:** Display human-readable checkmarks/icons or "Yes"/"No" — not raw .NET bool `ToString()` output. Uses the `@context.reverse` dynamic bool directly; should use `@(context.reverse ? "✓" : "–")` or a `MudIcon`.
- **Evidence:** `tmp-audit/C/copy-trading-running/02-profile-dialog-running.png`
- **Suspected root cause (read-only lead):** `src/Web/Components/Dialogs/CopyProfileDialog.razor` lines 29–33 — `@context.reverse`, `@context.mirrorPartialClose`, `@context.copyPendingOrders`, `@context.copyTrailingStop` output raw booleans.
- **Which tier SHOULD catch it:** E2E
- **Regression-test sketch:** Open copy profile dialog → assert destinations table does not contain text "True" or "False" (uses icons/Yes/No instead).

### C-08 — OAuth callback with invalid/missing state: graceful error shown (OK) — no finding; confirm
- **Severity:** n/a (no bug — informational)
- **Route / feature:** `/openapi/callback`
- **State:** invalid state / missing state param
- **Live or fake:** n/a
- **Steps to reproduce:**
  1. Navigate to `/openapi/callback?code=testcode&state=invalid_xyz`.
  2. Navigate to `/openapi/callback?code=testcode` (no state).
- **Observed:** Both cases show user-friendly error messages — "Something went wrong / This authorization link is invalid or expired." and "Missing authorization state. Please start again." respectively. No ErrorBoundary, no circuit crash.
- **Expected:** (As observed — this is correct behavior. No bug.)
- **Evidence:** `tmp-audit/C/oauth-callback/01-invalid-state.png`, `tmp-audit/C/oauth-callback/02-missing-state.png`

---

<!-- end Lane C -->

---

## Lane D — Prop Firm, Prop Guard & Compliance

Routes: `/prop-firm`, `/prop-guard`, `/compliance`

<!-- Lane D findings appended below -->

### D-01 — PropGuard "New rule" is an inline page form, not a MudBlazor dialog (mandate 7 violation)
- **Severity:** medium
- **Route / feature:** `/prop-guard`
- **State:** any
- **Live or fake:** fake
- **Steps to reproduce:**
  1. Navigate to `/prop-guard`.
  2. Observe "New rule" section directly on the page with Name field, Account select, numeric inputs, and "Create rule" button.
- **Observed:** Create-rule UI is an inline card on the left half of the page, no dialog. Confirmed by Playwright: `.mud-dialog` count = 0 on page load.
- **Expected:** Per CLAUDE.md mandate 7: every add/create/edit action opens a MudBlazor dialog via `IDialogService.ShowAsync`. A "New rule" toolbar button should open `NewPropRuleDialog`.
- **Evidence:** `C:/Users/afhac/source/cMind/tmp-audit/D/prop-guard/01-empty-state.png`
- **Suspected root cause:** `src/Web/Components/Pages/PropGuard.razor` — the create form is the left `MudItem` with fields and a "Create rule" button, no dialog involvement.
- **Which tier SHOULD catch it:** E2E — `PropGuardTests` only checks "New rule" text is visible, not that it opens a dialog.
- **Regression-test sketch:** `await page.ClickAsync("text=New Rule"); await Assertions.Expect(page.Locator(".mud-dialog")).ToBeVisibleAsync();`

---

### D-02 — PropFirm: all rows share a single `_equityInput` field — recording equity for row B overwrites row A's input
- **Severity:** high
- **Route / feature:** `/prop-firm` — "Record equity" column
- **State:** populated (2+ challenges)
- **Live or fake:** fake
- **Steps to reproduce:**
  1. Create two prop-firm challenges.
  2. Navigate to `/prop-firm`.
  3. Type `111000` in the equity input of the first row.
  4. Click "Record" on the second row.
- **Observed:** Both rows share `@bind-Value="_equityInput"`. The single `decimal _equityInput` field holds the last-typed value regardless of which row. Clicking "Record" on any row submits `_equityInput` to that row's ID — so if you typed in row 1, row 2 gets that value.
- **Expected:** Each row should have its own equity input, or the input should be inside the dialog/inline form scoped to the specific challenge.
- **Evidence:** `C:/Users/afhac/source/cMind/tmp-audit/D/prop-firm/04-two-challenges-equity-input.png`; `src/Web/Components/Pages/PropFirm.razor:39,64,126`
- **Suspected root cause:** `PropFirm.razor` line 64: `private decimal _equityInput;` — a single field used in all rows' `@bind-Value`. Line 39: `<MudNumericField T="decimal" @bind-Value="_equityInput" .../>` repeated per row.
- **Which tier SHOULD catch it:** E2E — no test creates two challenges and verifies independent equity recording.
- **Regression-test sketch:** Create two challenges; type different values in each row's input; assert each row's `currentEquity` is distinct after recording.

---

### D-03 — PropFirm table does not display breach cause — user cannot see WHY a challenge failed
- **Severity:** medium
- **Route / feature:** `/prop-firm` — challenge table
- **State:** terminal/failed
- **Live or fake:** fake
- **Steps to reproduce:**
  1. Create a challenge with 5% max daily loss.
  2. Record equity below 95 000 (e.g. 94 000) to trigger `DailyLoss` breach.
  3. Navigate to `/prop-firm`.
- **Observed:** Status chip shows "Failed" with no additional detail. The API response includes `breach: "DailyLoss"` but the table has no "Breach" column. Confirmed by Playwright: `text=DailyLoss` not visible on the page.
- **Expected:** The breach cause (DailyLoss / MaxDrawdown / ConsistencyRule / etc.) should be visible alongside the "Failed" chip so the user understands the violation.
- **Evidence:** `C:/Users/afhac/source/cMind/tmp-audit/D/prop-firm/09-failed-challenge-no-breach-detail.png`; `PropFirmEndpoints.cs:177` (breach projected but never rendered).
- **Suspected root cause:** `PropFirm.razor` table headers: `Name|Kind|Phase|Status|Equity|Peak|Days|...` — no Breach column. `_challenges` has `context.breach` but it is never rendered.
- **Which tier SHOULD catch it:** E2E — `PropFirmTests.Challenge_stop_start_and_breach_flow` asserts `breach == "DailyLoss"` at the API level but does not assert it is visible in the UI.
- **Regression-test sketch:** After equity triggers breach: `await Assertions.Expect(page.Locator("text=DailyLoss")).ToBeVisibleAsync();`

---

### D-04 — PropFirm: no detail/drill-down page for a challenge — violates mandate 11 detail-page requirement
- **Severity:** medium
- **Route / feature:** `/prop-firm` — table rows
- **State:** populated / terminal/failed
- **Live or fake:** fake
- **Steps to reproduce:**
  1. Create a challenge.
  2. Navigate to `/prop-firm`.
  3. Try to click on the challenge name or find a "View" / eye icon.
- **Observed:** No detail page or row-click drill-down exists. The table has lifecycle buttons (Start/Stop/Delete) and a Record-equity inline input, but no way to view challenge history, rule details, or equity progression chart.
- **Expected:** Each row should have a detail/drill-down (dialog or page) showing full rule config, equity history, and breach details. Mandate 11 states "Row's view/eye control is E2E-driven for a terminal entity."
- **Evidence:** `src/Web/Components/Pages/PropFirm.razor` — table `<MudTd>` last column only has Start/Stop/Delete icon buttons, no view/detail button.
- **Suspected root cause:** Detail view was never implemented; the page only lists challenges without an exploration path.
- **Which tier SHOULD catch it:** E2E — no smoke test for a challenge detail page/dialog; `PageSmokeTests` has no `/prop-firm/{id}` entry.
- **Regression-test sketch:** `await page.ClickAsync("[aria-label='View challenge']"); await Assertions.Expect(page.Locator(".mud-dialog, [data-testid=challenge-detail]")).ToBeVisibleAsync();`

---

### D-05 — Compliance: "Erase my account" fires immediately with no confirmation dialog
- **Severity:** high
- **Route / feature:** `/settings/legal` (and Settings dialog > Legal section)
- **State:** any
- **Live or fake:** fake
- **Steps to reproduce:**
  1. Navigate to `/settings/legal`.
  2. Click "Erase my account".
- **Observed:** `Erase()` method immediately posts to `/api/compliance/erase`. No `IDialogService.ShowMessageBox` or equivalent confirmation step. If clicked accidentally, the account is erased.
- **Expected:** A MudBlazor `ShowMessageBox` confirmation dialog ("Are you sure? This will permanently erase your account and all data.") before the DELETE is sent.
- **Evidence:** `src/Web/Components/Pages/Compliance.razor:45,76-81` — `OnClick="Erase"` directly calls the erase API.
- **Suspected root cause:** `Compliance.razor` `Erase()` method has no guard; no `IDialogService` injected in the component.
- **Which tier SHOULD catch it:** E2E — `ComplianceTests` tests export but not erase, and does not assert a confirmation step.
- **Regression-test sketch:** `await page.ClickAsync("text=Erase my account"); await Assertions.Expect(page.Locator(".mud-dialog")).ToBeVisibleAsync();`

---

### D-06 — PropGuard: Delete rule has no confirmation dialog
- **Severity:** medium
- **Route / feature:** `/prop-guard` — rules table
- **State:** populated
- **Live or fake:** fake
- **Steps to reproduce:**
  1. Create a prop guard rule.
  2. Click the delete icon next to a rule.
- **Observed:** `DeleteRule(id)` fires immediately via `MudIconButton OnClick`. No confirmation. A misclick deletes a production guard rule silently.
- **Expected:** A `ShowMessageBox` confirmation (especially for rules that may be protecting real accounts with active bots).
- **Evidence:** `src/Web/Components/Pages/PropGuard.razor:67` — `OnClick="@(() => DeleteRule((Guid)r.id))"` with no confirmation.
- **Suspected root cause:** `PropGuard.razor` delete handler goes directly to `DeleteAsync`. No `IDialogService` involved.
- **Which tier SHOULD catch it:** E2E.
- **Regression-test sketch:** `await page.ClickAsync("[aria-label='Delete rule']"); await Assertions.Expect(page.Locator(".mud-dialog")).ToBeVisibleAsync();`

---

### D-07 — CSP blocks Google Fonts stylesheet: `style-src` does not whitelist `fonts.googleapis.com`
- **Severity:** medium
- **Route / feature:** All pages (global)
- **State:** any
- **Live or fake:** fake
- **Steps to reproduce:**
  1. Open browser devtools Console on any page (e.g. `/prop-firm`).
  2. Observe: `Refused to load the stylesheet 'https://fonts.googleapis.com/css2?...' because it violates the Content Security Policy directive: "style-src 'self' 'unsafe-inline'"`.
- **Observed:** `App.razor` lines 20-22 load `Space Grotesk` and `JetBrains Mono` from `https://fonts.googleapis.com`. `SecurityHeaders.cs` line 11 sets `style-src 'self' 'unsafe-inline'` with no `https://fonts.googleapis.com` allowance. The font request is blocked by the browser, so the custom fonts do not load; the app falls back to system fonts. Confirmed by Playwright console error capture in E2E tests (every page reports this error).
- **Expected:** Either: (a) `style-src` in `SecurityHeaders.cs` is extended to `'self' 'unsafe-inline' https://fonts.googleapis.com` and `font-src` to `'self' data: https://fonts.gstatic.com`, or (b) the Google Fonts link tags are removed and fonts self-hosted in `wwwroot/`.
- **Evidence:** `src/Web/Components/App.razor:20-22`; `src/Web/Security/SecurityHeaders.cs:11`
- **Suspected root cause:** CSP was tightened (`font-src 'self' data:`) without updating the external Google Fonts `<link>` tags that were already in the document shell.
- **Which tier SHOULD catch it:** E2E — no test currently asserts absence of CSP console errors; `AccessibilityTests` and `PageSmokeTests` do not scan console output.
- **Regression-test sketch:** `page.Console += (_, e) => { if (e.Type == "error" && e.Text.Contains("Content Security Policy")) cspErrors.Add(e.Text); }; cspErrors.Should().BeEmpty();`

---

### D-08 — PropFirm: no HelpTip on `PropGuard.razor`; PropGuard page has no `<HelpTip>` at all
- **Severity:** low
- **Route / feature:** `/prop-guard`
- **State:** any
- **Live or fake:** fake
- **Steps to reproduce:**
  1. Navigate to `/prop-guard`.
  2. Inspect for `HelpTip` tooltip icon near the page heading.
- **Observed:** `/prop-guard` has no `<HelpTip>` component. `/prop-firm` has one (`PropFirm.razor:14`). Per CLAUDE.md Web mandate: "Every control gets `<HelpTip Text="…" />`".
- **Expected:** A `<HelpTip>` adjacent to the page heading explaining what Prop Guard does and how auto-flatten works.
- **Evidence:** `src/Web/Components/Pages/PropGuard.razor` — no `HelpTip` in file. `PropFirm.razor:14` has one for reference.
- **Suspected root cause:** Omitted during initial implementation.
- **Which tier SHOULD catch it:** E2E — `HelpTipTests` checks help tips on specific pages; `/prop-guard` is not in that list.
- **Regression-test sketch:** `await page.GotoAsync("/prop-guard"); await Assertions.Expect(page.Locator("[data-testid=help-tip]")).ToBeVisibleAsync();`

---

### D-09 — PropGuard rules table uses raw `<table>`/`<MudSimpleTable>` without `Breakpoint` or `DataLabel` — horizontal overflow on mobile
- **Severity:** medium
- **Route / feature:** `/prop-guard` — rules table
- **State:** populated
- **Live or fake:** fake
- **Steps to reproduce:**
  1. Set viewport to 360px wide.
  2. Create a prop guard rule.
  3. Navigate to `/prop-guard`.
- **Observed:** The rules table is a `MudSimpleTable` (line 50) with raw `<thead>/<tbody>` rows — no `Breakpoint` attribute, no `DataLabel` on cells. On 360px the 6-column table (Name / Account / Max live / Auto-flatten / On / actions) overflows horizontally.
- **Expected:** Either use `MudTable` with `Breakpoint="Breakpoint.Sm"` and `DataLabel` on each `MudTd`, or replace with a card stack at mobile width. Empty state (`MobileLayoutTests`) passes because it has zero rows.
- **Evidence:** `src/Web/Components/Pages/PropGuard.razor:50-72`
- **Suspected root cause:** `MudSimpleTable` was used instead of `MudTable`; no responsive breakpoint configured.
- **Which tier SHOULD catch it:** E2E — `MobileLayoutTests` tests `/prop-guard` but at empty state (no rows). A populated mobile test would catch the overflow.
- **Regression-test sketch:** Seed a rule, navigate at 360px, assert `document.documentElement.scrollWidth <= document.documentElement.clientWidth`.

---

### D-10 — PropFirm table column count (8 visible headers) causes overflow on small tablets without `DataLabel` on all cells
- **Severity:** low
- **Route / feature:** `/prop-firm` — challenge table
- **State:** populated
- **Live or fake:** fake
- **Steps to reproduce:**
  1. Create a challenge.
  2. Navigate to `/prop-firm` at 768px width.
- **Observed:** The table has 9 header columns (Name, Kind, Phase, Status, Equity, Peak, Days, Record equity, Actions). `Breakpoint="Breakpoint.Sm"` is set correctly, and `DataLabel` is present on all `MudTd`. Mobile-empty and mobile-populated tests pass at 360px. However the "Record equity" column embeds two controls (a `MudNumericField` + a `MudButton`) in a cell — on 360px this cell occupies significant vertical space and makes the card view tall.
- **Expected:** The inline equity recording UX could be moved to a dialog or a smaller input; on mobile the "Record equity" DataLabel card row is unusually tall.
- **Evidence:** `src/Web/Components/Pages/PropFirm.razor:38-41`; `C:/Users/afhac/source/cMind/tmp-audit/D/prop-firm/08-mobile-populated.png`
- **Suspected root cause:** Design choice; the dual-control cell is not responsive-aware.
- **Which tier SHOULD catch it:** E2E — visual inspection; current mobile test only checks `scrollWidth` not layout quality.
- **Regression-test sketch:** Verify card height for "Record equity" cell does not exceed 80px on 360px viewport.

---

### D-11 — Compliance: GDPR export returns internal user UUID in JSON response visible to user
- **Severity:** low
- **Route / feature:** `/settings/legal` — "Export my data"
- **State:** any
- **Live or fake:** fake
- **Steps to reproduce:**
  1. Navigate to `/settings/legal`.
  2. Click "Export my data".
  3. Read the JSON export panel rendered in the `<pre>` tag.
- **Observed:** The export JSON includes `"id": "<user-uuid>"` (the internal `UserId` strong-ID value). This is the user's primary key, a system-internal identifier. Confirmed from `ComplianceEndpoints.cs:67`: `user = new { id = uid.Value, ... }`.
- **Expected:** GDPR export should expose user-facing identity (email, role, created date) but not internal database UUIDs. At minimum the key should be labelled something meaningful, or omitted — the user has no use for their own database ID.
- **Evidence:** `src/Web/Endpoints/ComplianceEndpoints.cs:67`; `C:/Users/afhac/source/cMind/tmp-audit/D/compliance/02-gdpr-export.png`
- **Suspected root cause:** `ComplianceEndpoints.cs` line 67 directly projects `id = uid.Value` into the export object without considering whether a UUID is meaningful to end users.
- **Which tier SHOULD catch it:** Integration or E2E — `ComplianceTests` asserts the email is present but does not assert absence of raw UUID.
- **Regression-test sketch:** `exportText.Should().NotMatchRegex(@"""id""\s*:\s*""[0-9a-f\-]{36}""");`

---

## Lane E — AI Features & Agent Surface

Routes: `/ai/*` (build, debate, digest, exposure, optimize, review, tune), `/agent`, `/agent-studio`, `/alerts`, `/settings/ai`, `/mcp`

<!-- Lane E findings appended below -->


**Audit summary (2026-07-13):** All 14 routes reachable, render without ErrorBoundary. 0 blocker, 2 high, 3 medium, 4 low. Static code audit cross-referenced with existing E2E suite.

### E-01 - AiFeatureNotice hardcodes "Anthropic API key" — misleading for all other providers

- **Severity:** medium
- **Route / feature:** all AI feature pages, /agent, /alerts, /mcp
- **State:** no-ai-key
- **Live or fake:** n/a
- **Steps to reproduce:** 1. No AI provider. 2. Navigate to /ai/review. 3. Read banner and dialog.
- **Observed:** Banner: "AI features are turned off until an Anthropic API key is added in Settings -> AI." Dialog: "Add your Anthropic API key in Settings -> AI to use AI features." App supports Anthropic, OpenAI, Azure, Gemini, Ollama, ONNX. Ollama/ONNX users get wrong guidance.
- **Expected:** Provider-agnostic: "AI disabled — add a provider in Settings -> AI."
- **Evidence:** tmp-audit/E/e01-provider-text/evidence.txt; src/Web/Components/AiFeatureNotice.razor:12,49.
- **Suspected root cause:** Copy written when only Anthropic existed; not updated after multi-provider epic.
- **Which tier SHOULD catch it:** E2E
- **Regression-test sketch:** `(await page.Locator("[data-testid=ai-not-configured]").InnerTextAsync()).Should().NotContain("Anthropic");`

---

### E-02 - AiFeatureNotice fires intrusive dialog on /alerts and /mcp — AI not required there

- **Severity:** high
- **Route / feature:** /alerts, /mcp
- **State:** no-ai-key
- **Live or fake:** n/a
- **Steps to reproduce:** 1. No AI. 2. Navigate to /alerts. 3. Modal immediately: "Add your Anthropic API key". 4. Navigate to /mcp. 5. Same modal again.
- **Observed:** AiFeatureNotice._promptPending = !_enabled triggers ShowMessageBox on every page load when AI absent. Alert rule creation and MCP key creation are pure DB writes with no AI dependency. Users are blocked by an irrelevant modal.
- **Expected:** Auto-pop dialog only on @inherits AiFeaturePageBase pages. /alerts and /mcp: static informational banner only, never a blocking dialog.
- **Evidence:** tmp-audit/E/e03-alerts-no-gate/evidence.txt, tmp-audit/E/e04-mcp-wrong-notice/evidence.txt; AiFeatureNotice.razor:39,43-51; Alerts.razor:16; Mcp.razor:21.
- **Suspected root cause:** AiFeatureNotice reused on AI-independent pages without suppressing the dialog path.
- **Which tier SHOULD catch it:** E2E (navigate to /mcp with no AI key; assert no dialog; assert key can be created)
- **Regression-test sketch:** `(await page.Locator(".mud-dialog").IsVisibleAsync()).Should().BeFalse("no blocking dialog on /mcp when AI unconfigured");`

---

### E-03 - /mcp shows AI-not-configured notice — MCP key creation is AI-independent

- **Severity:** medium
- **Route / feature:** /mcp
- **State:** no-ai-key
- **Live or fake:** n/a
- **Steps to reproduce:** 1. No AI. 2. Navigate to /mcp. 3. See yellow warning banner.
- **Observed:** MCP keys page carries AiFeatureNotice rendering a warning banner. MCP Bearer tokens (mcpk_...) work regardless of AI provider. Banner confuses users.
- **Expected:** No AI notice on /mcp. Key creation is unconditional.
- **Evidence:** tmp-audit/E/e04-mcp-wrong-notice/evidence.txt; Mcp.razor:21.
- **Suspected root cause:** AiFeatureNotice copy-pasted without verifying AI dependency.
- **Which tier SHOULD catch it:** E2E (AiPagesTests.Ai_page_shows_not_configured_notice currently expects /mcp to show the notice — that expectation is itself wrong)
- **Regression-test sketch:** `(await page.Locator("[data-testid=ai-not-configured]").IsVisibleAsync()).Should().BeFalse("MCP keys page must not show AI notice");`

---

### E-04 - /agent-studio has no AI not-configured notice; Start enabled when AI absent

- **Severity:** high
- **Route / feature:** /agent-studio
- **State:** no-ai-key
- **Live or fake:** n/a
- **Steps to reproduce:** 1. No AI. 2. Navigate to /agent-studio. 3. Create agent, click Start.
- **Observed:** No AiFeatureNotice anywhere. Start button enabled for Draft/Stopped agents. Clicking Start transitions to Running but agent never executes AI decisions. No user feedback.
- **Expected:** AiFeatureNotice banner when AI absent; Start disabled with tooltip "AI provider required."
- **Evidence:** tmp-audit/E/e08-agent-studio-no-notice/evidence.txt; AgentStudio.razor — no AiFeatureNotice.
- **Suspected root cause:** AgentStudio added after AiFeatureNotice pattern; gating missed.
- **Which tier SHOULD catch it:** E2E
- **Regression-test sketch:** `await Assertions.Expect(page.Locator("[data-testid=ai-not-configured]")).ToBeVisibleAsync();`

---

### E-05 - All 14 AI/Agent pages un-localized — zero @L["key"] usage; gate does not cover them

- **Severity:** medium
- **Route / feature:** /ai/build, /ai/review, /ai/debate, /ai/sentiment, /ai/digest, /ai/exposure, /ai/optimize, /ai/tune, /ai/currency-strength, /agent, /agent-studio, /alerts, /settings/ai, /mcp; also AiFeatureNotice.razor
- **State:** any non-English locale
- **Live or fake:** n/a
- **Steps to reproduce:** 1. Switch locale to Arabic. 2. Visit any AI page. 3. All strings remain English.
- **Observed:** None of the 14 pages inject IStringLocalizer or use @L["key"]. NoHardcodedUiTextTests.MigratedRazorFiles() covers only 5 shell files — build never catches this.
- **Expected:** All strings via @L["key"]; keys in all 22 locales; gate extended.
- **Evidence:** tmp-audit/E/e05-no-localization/evidence.txt. CLAUDE.md mandate 9.
- **Suspected root cause:** AI features shipped before gate was extended; MigratedRazorFiles() never updated.
- **Which tier SHOULD catch it:** unit (NoHardcodedUiTextTests — add all 14 AI files)
- **Regression-test sketch:** Add each AI page path to MigratedRazorFiles(); run until all strings migrated.

---

### E-06 - AiPagesWithDataTests tests deleted /assistant route — stale test, false coverage

- **Severity:** low
- **Route / feature:** tests/E2ETests/AiPagesWithDataTests.cs
- **State:** populated
- **Live or fake:** n/a
- **Steps to reproduce:** 1. See [InlineData("/assistant")] in AiPagesWithDataTests. 2. Run — passes trivially. 3. /assistant returns 404.
- **Observed:** Test navigates to 404; checks no ErrorBoundary — trivially true. Does not cover /agent-studio.
- **Expected:** Replace "/assistant" with "/agent-studio".
- **Evidence:** tmp-audit/E/e06-stale-test-route/evidence.txt; AiPagesWithDataTests.cs:17.
- **Suspected root cause:** /assistant removed; test not updated.
- **Which tier SHOULD catch it:** E2E (self)
- **Regression-test sketch:** [InlineData("/agent-studio")] replacing [InlineData("/assistant")].

---

### E-07 - /ai/digest, /ai/exposure, /ai/tune, /ai/optimize, /ai/build missing fake-LLM E2E output tests

- **Severity:** medium
- **Route / feature:** /ai/digest, /ai/exposure, /ai/tune, /ai/optimize, /ai/build
- **State:** AI configured (fake LLM)
- **Live or fake:** fake
- **Steps to reproduce:** 1. Review AiFeatureLocalTests — covers Review, Sentiment, Debate, Currency-strength, Settings. 2. Note 5 routes absent.
- **Observed:** Five AI pages not driven with fake LLM. No test clicks submit and asserts ai-output renders canned reply. Silent API breaks go undetected.
- **Expected:** Each AI page has AiLocalCollection test asserting output contains AiLocalFixture.CannedReply.
- **Evidence:** tmp-audit/E/e07-ai-pages-missing-e2e/evidence.txt; AiFeatureLocalTests.cs.
- **Suspected root cause:** Tests written for first-shipped pages; not extended as new AI pages added.
- **Which tier SHOULD catch it:** E2E (AiFeatureLocalTests in AiLocalCollection)
- **Regression-test sketch:** `await page.ClickAsync("button:has-text('Generate digest')"); await AssertOutputAsync(page);`

---

### E-08 - /agent, /alerts, /agent-studio tables no Breakpoint/DataLabel — overflow on mobile with data

- **Severity:** low
- **Route / feature:** /agent, /alerts, /agent-studio
- **State:** populated, mobile (360px)
- **Live or fake:** n/a
- **Steps to reproduce:** 1. Create 2-3 mandates/rules/agents. 2. View at 360px. 3. Horizontal scroll.
- **Observed:** /agent and /alerts use MudSimpleTable (raw table, no breakpoint). /agent-studio MudTable missing Breakpoint="Breakpoint.Sm"; MudTd has no DataLabel. MobileLayoutTests uses empty DB so overflow never triggered.
- **Expected:** MudSimpleTable -> MudTable Breakpoint="Breakpoint.Sm" + DataLabel on every MudTd.
- **Evidence:** tmp-audit/E/e09-tables-no-mobile/evidence.txt; Agent.razor:62,99; Alerts.razor:39; AgentStudio.razor:28.
- **Suspected root cause:** MudSimpleTable used for quick impl; Nodes.razor responsive pattern not followed.
- **Which tier SHOULD catch it:** E2E (MobileLayoutTests with data-seeded variant)
- **Regression-test sketch:** Seed one mandate, /agent on mobile, assert scrollWidth <= innerWidth + 1.

---

### E-09 - /agent mandate Create button not AI-gated despite banner saying AI disabled

- **Severity:** low
- **Route / feature:** /agent
- **State:** no-ai-key
- **Live or fake:** n/a
- **Steps to reproduce:** 1. No AI. 2. Navigate to /agent. 3. Fill mandate form, click Create. 4. Observe success.
- **Observed:** Banner says "AI features are turned off" implying creation fails. Create button uses Disabled="_busy" only — mandate created in DB but never runs. No explanation to user.
- **Expected:** Either gate creation on AI configured (disabled + tooltip), or allow creation and show "This mandate will not run until an AI provider is configured."
- **Evidence:** tmp-audit/E/e02-agent-no-gate/evidence.txt; Agent.razor:16,47.
- **Suspected root cause:** AiFeatureNotice used without wiring EnabledChanged to gate the form.
- **Which tier SHOULD catch it:** E2E
- **Regression-test sketch:** With no AI, submit mandate — assert button disabled OR "will not run" notice renders.

---


---

## Lane F — Quant / Institutional-Edge Suite

Routes: `/quant/execution`, `/quant/health`, `/quant/integrity`, `/quant/positioning`, `/quant/regimes`, `/quant/sizing`, `/quant/tca`, `/journal`

<!-- Lane F findings appended below — static code audit (all 8 routes) -->

**Audit summary (2026-07-13):** All 8 routes reachable and render without ErrorBoundary. Static code
audit performed (Playwright live run not required — all pages are input-form tools with no ambient state,
and the existing E2E suite covers the happy-path API round-trip). 8 bugs found: 2 high, 3 medium, 3 low.

### F-01 — All 8 Quant/Journal pages use zero localization (hardcoded English throughout)
- **Severity:** high
- **Route / feature:** `/quant/execution`, `/quant/health`, `/quant/integrity`, `/quant/positioning`, `/quant/regimes`, `/quant/sizing`, `/quant/tca`, `/journal`
- **State:** all states
- **Live or fake:** n/a
- **Steps to reproduce:** 1. Switch app locale to any non-English language (e.g. Arabic). 2. Navigate to any quant page or `/journal`.
- **Observed:** Every `Label`, `HelperText`, `MudText`, snackbar message, `HelpTip Text`, radio option label, button label, and empty-state message is a hard-coded English string literal. No `IStringLocalizer<Ui>` injection, no `@L["key"]` usage in any of the 8 files. The `ui-translations.json` file has navigation keys (`nav.integrity`, `nav.journal`, etc.) but zero content keys for these pages.
- **Expected:** All user-facing strings use `@L["key"]` and have translations in every locale per CLAUDE.md mandate 9.
- **Evidence:** grep of all 8 `.razor` files confirms zero `IStringLocalizer` references.
- **Suspected root cause:** `src/Web/Components/Pages/Quant/` — all 8 .razor files written without localization wiring. `NoHardcodedUiTextTests.MigratedRazorFiles()` does not include quant/journal pages so the build never catches this.
- **Which tier SHOULD catch it:** unit (`NoHardcodedUiTextTests` must include these files)
- **Regression-test sketch:** Add all 8 paths to `NoHardcodedUiTextTests.MigratedRazorFiles()`.

---

### F-02 — Journal page fetches and silently discards `entries` list — per-trade rows never rendered
- **Severity:** high
- **Route / feature:** `/journal`
- **State:** populated (user has backtest/run instances)
- **Live or fake:** n/a
- **Steps to reproduce:** 1. Log in as a user with completed backtest instances. 2. Navigate to `/journal`. 3. View the populated-state branch (`_data.summary.total > 0`).
- **Observed:** `JournalData` record contains `List<JournalEntryDto> entries`. The API (`JournalEndpoints.cs`) returns this array populated. The Razor template's populated-state branch shows only the summary stats table and insights alerts. `_data.entries` is never referenced anywhere in the markup. Per-trade rows (cbot name, symbol, kind, status) are silently dropped.
- **Expected:** Individual journal entries should be displayed in a table or card list below the summary.
- **Evidence:** `src/Web/Components/Pages/Quant/Journal.razor` lines 60-62: `entries` declared in record, never used in template. `JournalEndpoints.cs` lines 50-56 returns entries array that is discarded.
- **Suspected root cause:** `src/Web/Components/Pages/Quant/Journal.razor` — `entries` field in `JournalData` never referenced in markup.
- **Which tier SHOULD catch it:** E2E (`JournalTests` should assert entries list renders when data present)
- **Regression-test sketch:** `await Assertions.Expect(page.Locator("[data-testid=journal-entries]")).ToBeVisibleAsync()` with seeded instances.

---

### F-03 — All 8 quant/journal pages missing from MobileLayoutTests; no mobile E2E path exists
- **Severity:** medium
- **Route / feature:** `/quant/execution`, `/quant/health`, `/quant/integrity`, `/quant/positioning`, `/quant/regimes`, `/quant/sizing`, `/quant/tca`, `/journal`
- **State:** mobile (360px)
- **Live or fake:** n/a
- **Steps to reproduce:** 1. Review `MobileLayoutTests.MobileRoutes()` and `NoOverflowRoutes()`. 2. Note all 8 quant/journal routes are absent from both lists. 3. Review all `QuantXxxTests.cs` files — none use `NewAuthedMobilePageAsync`.
- **Observed:** No quant or journal route appears in `MobileLayoutTests`. All per-feature quant E2E tests use `NewAuthedPageAsync` (desktop) only. Mobile shell render and no-horizontal-scroll are untested for the entire quant suite.
- **Expected:** Every route in `PageSmokeTests.Routes()` must also appear in `MobileLayoutTests` per CLAUDE.md (mobile-first, no horizontal scroll 320-1920px).
- **Evidence:** `tests/E2ETests/MobileLayoutTests.cs` lines 14-27; all `QuantXxxTests.cs` files.
- **Suspected root cause:** Quant routes added to `PageSmokeTests` but never added to `MobileLayoutTests`.
- **Which tier SHOULD catch it:** E2E (`MobileLayoutTests`)
- **Regression-test sketch:** Add all 8 routes to `MobileRoutes()` and `NoOverflowRoutes()` arrays.

---

### F-04 — `QuantExecution`: zero-quantity order accepted and returns a nonsensical all-zero schedule
- **Severity:** medium
- **Route / feature:** `/quant/execution`
- **State:** populated (total quantity set to 0)
- **Live or fake:** n/a
- **Steps to reproduce:** 1. Navigate to `/quant/execution`. 2. Set "Total quantity (lots)" to `0`. 3. Press "Build schedule".
- **Observed:** `MudNumericField` has `Min="0"`, so `0` is accepted. The page calls `POST /api/quant/execution-schedule` with `TotalQuantity=0`. The endpoint invokes `IExecutionScheduler.Schedule(0, ...)` which produces all-zero slices — rendered in the result table without any warning.
- **Expected:** Page should validate `_total > 0` before submitting and show a snackbar warning "Total quantity must be greater than zero."
- **Evidence:** `src/Web/Components/Pages/Quant/QuantExecution.razor` line 17 (`Min="0"`) and `Build()` method — no positivity guard.
- **Suspected root cause:** Missing client-side guard in `Build()` before API call.
- **Which tier SHOULD catch it:** E2E (`QuantExecutionTests` — unhappy path: zero quantity)
- **Regression-test sketch:** Set total=0, click build, assert snackbar contains "greater than zero".

---

### F-05 — `QuantTca`: negative fill quantities accepted without validation
- **Severity:** medium
- **Route / feature:** `/quant/tca`
- **State:** populated
- **Live or fake:** n/a
- **Steps to reproduce:** 1. Navigate to `/quant/tca`. 2. Enter fill line `1.1005, -100`. 3. Press "Analyze cost".
- **Observed:** `ParseFills()` accepts any float as quantity including negatives. A negative-quantity fill reaches `ITransactionCostAnalyzer.Analyze()` and may produce nonsensical (negative) slippage with no user-facing error.
- **Expected:** Negative fill quantities rejected with a snackbar validation message before the API call.
- **Evidence:** `src/Web/Components/Pages/Quant/QuantTca.razor` `ParseFills()` method (lines 101-113) — only checks parse success, not value sign.
- **Suspected root cause:** `ParseFills()` missing `qty > 0` guard.
- **Which tier SHOULD catch it:** unit (TCA domain) + E2E (`QuantTcaTests` — negative qty unhappy path)
- **Regression-test sketch:** Enter `1.1005, -100`, click analyze, assert snackbar shows validation error.

---

### F-06 — Equity-curve input mode never exercised by E2E on any of the 4 affected quant pages
- **Severity:** low
- **Route / feature:** `/quant/sizing`, `/quant/health`, `/quant/integrity`, `/quant/regimes`
- **State:** populated (equity mode selected)
- **Live or fake:** n/a
- **Steps to reproduce:** Review `QuantSizingTests`, `QuantHealthTests`, `QuantIntegrityTests`, `QuantRegimesTests` — all tests use the default "Periodic returns" radio mode only.
- **Observed:** All 4 pages expose an "Equity / balance curve" radio that sends `Equity=numbers, Returns=null`. No E2E test selects this radio and submits an equity curve. The equity-curve code path (`ReturnSeries.FromEquityCurve`) is untested at E2E level.
- **Expected:** Each quant E2E with a mode toggle must test both returns and equity-curve modes.
- **Evidence:** All four `QuantXxxTests.cs` files — returns-mode only; no equity radio click.
- **Suspected root cause:** E2E tests written for happy-path returns mode only.
- **Which tier SHOULD catch it:** E2E
- **Regression-test sketch:** `await page.GetByText("Equity / balance curve").ClickAsync(); // submit equity series; assert result renders`.

---

### F-07 — Journal: no create/edit/delete actions — manual journal entries feature absent
- **Severity:** low
- **Route / feature:** `/journal`
- **State:** all
- **Live or fake:** n/a
- **Steps to reproduce:** 1. Navigate to `/journal`. 2. Look for "New entry" button or per-row edit/delete controls.
- **Observed:** The journal is fully read-only. No way to add manual entries, annotate trades, or delete rows. The audit scope and CLAUDE.md mandate 7 (add/create/edit in a dialog) imply manual entries should exist.
- **Expected:** "New entry" toolbar button (MudSpacer pattern) opens a MudBlazor dialog; per-row edit and delete icon buttons.
- **Evidence:** `src/Web/Components/Pages/Quant/Journal.razor` — entire template; `JournalEndpoints.cs` — only GET endpoint, no POST/PUT/DELETE.
- **Suspected root cause:** Feature not implemented — no domain model for manual journal entries.
- **Which tier SHOULD catch it:** E2E (create + edit + delete dialog round-trip)
- **Regression-test sketch:** `await page.ClickAsync("[data-testid=journal-new-entry]"); await Assertions.Expect(page.Locator("role=dialog")).ToBeVisibleAsync()`.

---

### F-08 — All 7 quant event-handler methods lack CancellationToken / disposal guard (ObjectDisposedException risk)
- **Severity:** low
- **Route / feature:** `/quant/execution`, `/quant/health`, `/quant/integrity`, `/quant/positioning`, `/quant/regimes`, `/quant/sizing`, `/quant/tca`
- **State:** all
- **Live or fake:** n/a
- **Steps to reproduce:** Rapid navigation away from a quant page immediately after pressing an analyze/calculate button.
- **Observed:** All 7 quant Razor pages have async event-handler methods that call `Http.PostAsJsonAsync(...)` with no `CancellationToken` and no check for component disposal before mutating `_result`. If the user navigates away while the request is in-flight, the continuation assigns `_result` on a disposed `ComponentBase`, producing an unobserved `ObjectDisposedException` that surfaces as a logged error on the Blazor Server circuit.
- **Expected:** Implement `IDisposable` + `CancellationTokenSource` per component, pass `_cts.Token` to `PostAsJsonAsync`, and check cancellation before state mutation.
- **Evidence:** All 7 `Quant*.razor` files — `Build()` / `Analyze()` / `Assess()` / `Read()` / `Calculate()` methods share this pattern.
- **Suspected root cause:** Pattern copied across all 7 quant pages without lifecycle disposal wiring.
- **Which tier SHOULD catch it:** E2E (rapid-nav test) / code review
- **Regression-test sketch:** Navigate to `/quant/sizing`, click "Recommend size", immediately navigate to `/`, assert no `.blazor-error-ui` visible.

---

## Lane G — Economic Calendar & Currency Strength

Routes: `/economic-calendar`, `CalendarSeries`, `/ai/currency-strength`, `/ai/sentiment`

**Audit summary (2026-07-13):** All 4 routes reachable and render without ErrorBoundary.
Existing E2E suite (13 tests) all pass. 6 bugs found: 1 high, 3 medium, 2 low.
No live FRED/BLS key present — source-less gate verified. Fake LLM active.

### G-01 — Refresh button available to non-owner users but POST /refresh returns 403

- **Severity:** high
- **Route / feature:** `/ai/currency-strength` — "Refresh now" button
- **State:** populated (AI configured, user is non-owner)
- **Live or fake:** fake
- **Steps to reproduce:**
  1. Log in as a regular user (not owner).
  2. Navigate to `/ai/currency-strength`.
  3. Click "Refresh now".
- **Observed:** `POST /api/ai/currency-strength/refresh` returns 403 (requires `Owner` policy). The UI shows a generic "Refresh failed" snackbar with no explanation of why.
- **Expected:** The "Refresh now" button is either hidden or disabled with a tooltip for non-owner users, so they never trigger a doomed action. Mandate 11 — "a control that would be a no-op or a 409 must be disabled, not enabled."
- **Evidence:** `src/Web/Endpoints/CurrencyStrengthEndpoints.cs:54` — `.RequireAuthorization(Core.Constants.AuthPolicies.Owner)` on the refresh POST; `src/Web/Components/Pages/Ai/AiCurrencyStrength.razor:12-13` — `Disabled="Busy"` does not gate by role.
- **Suspected root cause:** `src/Web/Endpoints/CurrencyStrengthEndpoints.cs:54` (endpoint is Owner-only) vs `src/Web/Components/Pages/Ai/AiCurrencyStrength.razor:12-13` (button never checks role).
- **Which tier SHOULD catch it:** E2E (non-owner authed user clicks Refresh, asserts button disabled or 403 snackbar absent)
- **Regression-test sketch:** `page.Locator("[data-testid=cs-refresh]").Should().BeDisabledAsync()` when logged in as a non-owner user with AI configured.

---

### G-02 — CalendarSeries page missing HasConfiguredSource gate — shows empty state instead of actionable notice

- **Severity:** medium
- **Route / feature:** `/economic-calendar/series/{Code}`
- **State:** enabled (white-label + feature toggle ON) but no FRED/BLS key configured
- **Live or fake:** n/a
- **Steps to reproduce:**
  1. Deploy without a FRED/BLS API key (default out-of-box).
  2. Navigate directly to `/economic-calendar/series/US.CPI`.
- **Observed:** Page shows `@L["calendar.empty"]` (a neutral "no events" alert). There is no mention of needing to configure a data source.
- **Expected:** Same actionable `[data-testid=calendar-source-required]` notice that the main `/economic-calendar` page shows, consistent with mandate 11 (clear actionable notice when dependency is absent).
- **Evidence:** `src/Web/Components/Pages/CalendarSeries.razor:79` — only checks `CalendarEnablement.IsEnabled(...)`, never calls `CalendarEnablement.HasConfiguredSource(...)`. Compare with `src/Web/Components/Pages/EconomicCalendar.razor:94-99`.
- **Suspected root cause:** `src/Web/Components/Pages/CalendarSeries.razor:78-85` — missing `_hasSource` guard.
- **Which tier SHOULD catch it:** E2E (navigate to series page with no source configured, assert `[data-testid=calendar-source-required]` visible)
- **Regression-test sketch:** `await page.Locator("[data-testid=calendar-source-required]").WaitForAsync(...)` on `/economic-calendar/series/US.CPI` with the base fixture (no source key).

---

### G-03 — AiFeatureNotice hardcodes "Anthropic API key" — misleads users of OpenAI / local providers

- **Severity:** medium
- **Route / feature:** `/ai/currency-strength`, `/ai/sentiment`, and every AI page using `<AiFeatureNotice />`
- **State:** key-absent
- **Live or fake:** fake / n/a
- **Steps to reproduce:**
  1. Deploy with no AI provider configured.
  2. Navigate to any AI feature page (e.g. `/ai/sentiment`).
- **Observed:** Banner and dialog say: "AI features are turned off until an **Anthropic API key** is added in Settings → AI." (AiFeatureNotice.razor:12 and :49). The app now supports Anthropic, OpenAI, Azure, Gemini, and OpenAI-compatible local providers.
- **Expected:** Provider-agnostic message, e.g. "AI features are turned off until an AI provider is configured in Settings → AI."
- **Evidence:** `src/Web/Components/AiFeatureNotice.razor:12,49` — hardcoded "Anthropic API key" in both the banner `<span>` and the dialog body string.
- **Suspected root cause:** The notice was written before multi-provider AI shipped (memory: `multi-provider-ai-shipped.md`); copy was never updated.
- **Which tier SHOULD catch it:** unit (string value test) or E2E (assert no "Anthropic" text on keyless banner when no provider is configured)
- **Regression-test sketch:** `(await page.Locator("[data-testid=ai-not-configured]").InnerTextAsync()).Should().NotContain("Anthropic")`.

---

### G-04 — AiCurrencyStrength tables use Breakpoint.None — horizontal overflow on mobile with data

- **Severity:** medium
- **Route / feature:** `/ai/currency-strength` — ranking, forecast, and pair tables
- **State:** populated (after a successful Refresh returns data)
- **Live or fake:** fake
- **Steps to reproduce:**
  1. Log in as owner, navigate to `/ai/currency-strength`.
  2. Click "Refresh now" (with AI configured via fake LLM).
  3. View on a 360px-wide phone (or resize browser to 360px).
- **Observed:** All three `MudTable`s (`cs-ranking`, `cs-forecast`, `cs-matrix`) are set to `Breakpoint="Breakpoint.None"`, meaning MudBlazor never collapses them to stacked cards. The pair-outlook table (5+ columns) will overflow sideways at phone widths.
- **Expected:** `Breakpoint="Breakpoint.Sm"` on all tables per `src/Web/CLAUDE.md` ("every `MudTable` sets `Breakpoint="Breakpoint.Sm"`"). The overflow-x wrapper on the pair table (line 85) partially mitigates it but produces a scrollable region inside the page — still a UX problem on 360px.
- **Evidence:** `src/Web/Components/Pages/Ai/AiCurrencyStrength.razor:50,68,86` — all three tables have `Breakpoint="Breakpoint.None"`.
- **Suspected root cause:** Tables were authored with `Breakpoint.None` to avoid incorrect card collapsing before DataLabel attrs were added; DataLabel attrs are present but Breakpoint was never changed.
- **Which tier SHOULD catch it:** E2E mobile (assert no horizontal overflow after data loads, not just when `_view is null`)
- **Regression-test sketch:** After clicking Refresh and waiting for `[data-testid=cs-ranking]` to appear: `page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth + 1")` should be `true`.

---

### G-05 — Currency-strength E2E does not assert AI narrative (canned reply) rendered after Refresh

- **Severity:** low
- **Route / feature:** `/ai/currency-strength` — `[data-testid=cs-narrative]` after Refresh
- **State:** populated, fake LLM active
- **Live or fake:** fake
- **Steps to reproduce:**
  1. Run `AiFeatureLocalTests.Currency_strength_page_is_ai_enabled_and_refresh_runs`.
  2. Note that after `refresh.ClickAsync()` + 1.5s wait, neither `[data-testid=cs-narrative]` nor the canned reply text is asserted.
- **Observed:** Test passes even if the AI narrative section (`[data-testid=cs-narrative]`) never renders or contains the canned reply. The test only checks no crash — it does NOT assert the AI output is visible (`AiLocalFixture.CannedReply`).
- **Expected:** After a successful Refresh, `[data-testid=cs-narrative]` should be visible and contain `AiLocalFixture.CannedReply` when using the fake LLM (per mandate 2: "Playwright E2E test that boots the AI-configured fixture, exercises the feature through the UI, and asserts the AI output renders").
- **Evidence:** `tests/E2ETests/AiFeatureLocalTests.cs:83-97` — no assertion on `cs-narrative` or canned reply.
- **Suspected root cause:** Test was written to "not crash" level; the assertion for the AI output was not added.
- **Which tier SHOULD catch it:** E2E (ai-local collection)
- **Regression-test sketch:** `await page.Locator("[data-testid=cs-narrative]").WaitForAsync(...)` + `(await page.Locator("[data-testid=cs-narrative]").InnerTextAsync()).Should().Contain(AiLocalFixture.CannedReply)`.

---

### G-06 — EconomicCalendar and CalendarSeries pages are missing HelpTip

- **Severity:** low
- **Route / feature:** `/economic-calendar`, `/economic-calendar/series/{Code}`
- **State:** any
- **Live or fake:** n/a
- **Steps to reproduce:**
  1. Navigate to `/economic-calendar` or `/economic-calendar/series/US.CPI`.
  2. Inspect page header area for a HelpTip icon.
- **Observed:** Neither page has a `<HelpTip Text="…" />` component. Both `/ai/currency-strength` and `/ai/sentiment` have one. `src/Web/CLAUDE.md` mandates "Every control gets `<HelpTip Text="…" />`".
- **Expected:** A contextual `<HelpTip>` in the page title row explaining what the calendar shows and how to configure a data source.
- **Evidence:** `src/Web/Components/Pages/EconomicCalendar.razor:17-25` — no HelpTip in the header stack; `src/Web/Components/Pages/CalendarSeries.razor:15-19` — same.
- **Suspected root cause:** HelpTip was omitted during initial calendar implementation.
- **Which tier SHOULD catch it:** E2E (assert HelpTip icon visible, tap opens tooltip)
- **Regression-test sketch:** `(await page.Locator("[data-testid=help-tip]").CountAsync()).Should().BeGreaterThan(0)` on the calendar page.

<!-- Lane G findings appended below -->

---

## Lane H — Settings, White-label & Feature Toggles

Routes: `/settings/deployment`, `/settings/features`, `/settings/legal`, branding

<!-- Lane H findings appended below -->

### H-01 — `/settings/features`, `/settings/legal`, `/nodes` pages have no `IStringLocalizer<Ui>` — all user-facing strings are hard-coded; localization gate does not scan them

- **Severity:** high
- **Route / feature:** `/settings/features`, `/settings/legal`, `/nodes`
- **State:** any authenticated session
- **Live or fake:** n/a
- **Steps to reproduce:**
  1. Set browser/user locale to Arabic (ar) via `/set-culture?culture=ar`.
  2. Navigate to `/settings/features` (owner-only) or `/settings/legal` or `/nodes`.
  3. Inspect all visible text.
- **Observed:** Every visible string on these three pages is hard-coded English. No `@inject IStringLocalizer<Ui> L` present. Strings include: page titles ("Features", "Legal & Privacy", "Agreements", "Your data (GDPR)", "Nodes"), body paragraphs, button labels ("Export my data", "Erase my account", "New Node"), chip labels ("Accepted", "Action required"), `aria-label` attributes ("Clean backtest data", "Delete node"), snackbar messages ("Recorded", "Failed", "Added", "Toggle failed", "Local node enabled/disabled", "Export failed", "Account erased — you will be signed out shortly.", "Erase failed").
- **Expected:** All user-facing strings go through `@L["key"]`; keys exist in all 22 locales; Arabic RTL renders correctly on these pages.
- **Evidence:** tmp-audit/H/hardcoded-strings/summary.txt
- **Suspected root cause:**
  - `src/Web/Components/Pages/FeatureSettings.razor` — no L injected
  - `src/Web/Components/Pages/Compliance.razor` — no L injected
  - `src/Web/Components/Pages/Nodes.razor` — no L injected
  - `tests/UnitTests/Localization/NoHardcodedUiTextTests.cs` `MigratedRazorFiles()` — only covers 5 layout/dialog files; these settings pages are never added to the scanned set
- **Which tier SHOULD catch it:** unit (NoHardcodedUiTextTests gate should scan these files); also ResourceParityTests if keys were added
- **Regression-test sketch:** Add `"Pages/FeatureSettings.razor"`, `"Pages/Compliance.razor"`, `"Pages/Nodes.razor"` to `MigratedRazorFiles()` in `NoHardcodedUiTextTests`; the test then fails until all strings are extracted to `@L["key"]`.

---

### H-02 — `GET /api/nodes/` is not gated by `NodesUi=Hidden` — admin can read node data when node surface is hidden

- **Severity:** medium
- **Route / feature:** `GET /api/nodes/` when `App__Branding__NodesUi=Hidden`
- **State:** NodesUi=Hidden deployment (set via `/settings/deployment` or env var)
- **Live or fake:** n/a
- **Steps to reproduce:**
  1. Set `branding.nodesUi` to `Hidden` via `/settings/deployment` (owner).
  2. Confirm nav link `/nodes` is gone and page `/nodes` redirects to `/`.
  3. As an admin user (non-owner), call `GET /api/nodes/` directly.
- **Observed:** `GET /api/nodes/` returns 200 with node list. Only `POST /api/nodes/` (add) and `DELETE /api/nodes/{id}` are gated by `AllowsManualManagement`. The read endpoint has no NodesUi mode check.
- **Expected:** In `Hidden` mode, `GET /api/nodes/` should also return 404 (consistent with nav + page being hidden). The entire `/api/nodes` group (except auto-discovery `POST /api/nodes/register`) should be 404 in Hidden mode.
- **Evidence:** tmp-audit/H/nodes-api-hidden/summary.txt — `src/Web/Endpoints/NodeEndpoints.cs` line 37; `tests/E2ETests/NodesHiddenTests.cs` — no API assertion
- **Suspected root cause:** `NodeEndpoints.cs` line 37: `g.MapGet("/", ...)` — no `AllowsManualManagement` or `IsPageVisible` guard; comment on line 32 intentionally snapshots `RestrictNodesToOwner` at startup but does not comment on Hidden GET gating.
- **Which tier SHOULD catch it:** E2E (`NodesHiddenTests` — add an API-level assertion that `GET /api/nodes/` → 404 in Hidden mode)
- **Regression-test sketch:** In `NodesHiddenTests`, add: `(await page.APIRequest.GetAsync($"{app.BaseUrl}/api/nodes/")).Status.Should().Be(404);`

---

### H-03 — Brand name change via `/settings/deployment` takes effect in `BrandingThemeProvider` (via `IOptionsMonitor.OnChange`) but app-bar `[data-testid=app-product-name]` does not re-render live in an open Blazor circuit — no E2E asserts the live reflection

- **Severity:** medium
- **Route / feature:** `/settings/deployment` → `branding.productName` → app-bar
- **State:** owner-set override with an already-open authenticated session
- **Live or fake:** n/a
- **Steps to reproduce:**
  1. Log in as owner. Note app-bar shows "cMind".
  2. Navigate to `/settings/deployment` → Branding tab → edit "Product name" → set to "AcmeTrade" → save.
  3. Without reloading, navigate back to `/` (the dashboard).
  4. Observe app-bar text.
- **Observed (expected):** `BrandingThemeProvider` updates via `OnChange`, but `MainLayout.razor` reads `Branding.Branding.ProductName` as a render-time property — Blazor Server does NOT automatically re-render the layout when the injected service's backing field changes unless `StateHasChanged()` is called or a `CascadingValue` changes. The brand name in the app-bar likely shows "cMind" until the circuit is refreshed (F5). No E2E test exercises the "change → navigate → see updated name in same circuit" flow.
- **Expected:** After saving `branding.productName` override, any subsequent navigation (SPA-style) within the same circuit should reflect the new name in the app-bar without a full page reload.
- **Evidence:** `src/Web/Components/Layout/MainLayout.razor` line 28 — reads `@Branding.Branding.ProductName`; `src/Web/Branding/BrandingThemeProvider.cs` — updates on `IOptionsMonitor.OnChange` but has no notification mechanism to trigger Blazor circuit re-render; `tests/E2ETests/BrandingTests.cs` — only asserts static value, not live change.
- **Suspected root cause:** `MainLayout` injects `IBrandingThemeProvider` as a scoped service — Blazor Server wires this per-circuit. When the backing `_branding` field changes, the component has no subscriber to call `StateHasChanged()`. The MudBlazor `MudThemeProvider` re-renders because `Theme` property change propagates via `MudThemeProvider`'s own parameter binding, but plain `@Branding.Branding.ProductName` bindings do not.
- **Which tier SHOULD catch it:** E2E
- **Regression-test sketch:** `await page.GotoAsync("/settings/deployment"); /* save product name override */ await page.GotoAsync("/"); await Assertions.Expect(page.Locator("[data-testid=app-product-name]")).ToHaveTextAsync("AcmeTrade");` — this would expose whether the live circuit sees the update.

---

### H-04 — `/settings/features` feature toggle list uses raw `dynamic` API response — if a flag name changes, the display shows the enum internal name (no label mapping); snackbars use raw flag name without localization

- **Severity:** low
- **Route / feature:** `/settings/features`
- **State:** any owner session
- **Live or fake:** n/a
- **Steps to reproduce:**
  1. Navigate to `/settings/features` as owner.
  2. Observe the "Feature" column.
- **Observed:** Feature column shows raw `FeatureFlag` enum names (e.g. `PortfolioAgent`, `EconomicCalendar`) as returned by the API — PascalCase without word-splitting or localized labels. Snackbar messages like "PortfolioAgent enabled" are not user-friendly. (Compare: `WhiteLabelCatalog.SplitPascal()` is used in the catalog to turn `PortfolioAgent` → `Portfolio Agent`, but `FeatureSettings.razor` does not use it.)
- **Expected:** The Feature column should show a human-readable label (split-pascal at minimum: "Portfolio Agent", "Economic Calendar"); the toggle snackbar should show the label not the enum name.
- **Evidence:** `src/Web/Components/Pages/FeatureSettings.razor` line 22 — `@context.flag` (raw string); `src/Web/Endpoints/FeatureEndpoints.cs` line 21 — returns `kv.Key.ToString()` (enum name).
- **Suspected root cause:** `FeatureEndpoints` returns the raw enum name; `FeatureSettings.razor` displays it verbatim.
- **Which tier SHOULD catch it:** E2E (assert a friendly label in the feature column, not a raw enum name)
- **Regression-test sketch:** `await Assertions.Expect(page.Locator("td:has-text('Portfolio Agent')")).ToBeVisibleAsync();`

---

### H-05 — Disabling the `Registration` feature flag via `/settings/features` correctly gates the API, but the `/register` page remains loadable (shows a "closed" notice rather than 404) — inconsistent with the feature-toggle protocol for all other feature flags

- **Severity:** low
- **Route / feature:** `/register` when `FeatureFlag.Registration=false`
- **State:** feature flag off
- **Live or fake:** n/a
- **Steps to reproduce:**
  1. Toggle `Registration` off in `/settings/features`.
  2. Navigate to `/register` directly (as an anonymous user).
- **Observed:** `/register` returns HTTP 200 and renders the branded auth shell with a "Registration is not available." notice. All other feature flags cause the nav link to disappear AND the underlying API to return 404. The page itself stays accessible (intentional per the `Register.razor` design), but this differs from the pattern used by every other feature. No test explicitly verifies this asymmetry is deliberate.
- **Expected (by convention):** The `FeatureToggleTests` description says "hides its nav and 404s its api" — the `/register` page does not nav-hide (it's anonymous, has no nav link to hide) and stays 200. This is a documented carve-out but has no inline comment explaining why the page stays 200 rather than 404-ing like a gated Blazor page.
- **Evidence:** `src/Web/Components/Pages/Register.razor` — no feature-gate check at page level; `FeatureToggleTests.cs` — only tests CopyTrading; `RegistrationTests.cs` line 24 — covers the "closed" state but does not assert `GET /register → 200` (it may 404 in future if someone adds a gate).
- **Suspected root cause:** `/register` is a static-rendered page using vanilla JS fetch — the feature gate lives at the API level (`/api/register/config` and `/api/register`), not the page level.
- **Which tier SHOULD catch it:** E2E (assert the exact HTTP status of `GET /register` when feature off, to pin the behavior)
- **Regression-test sketch:** `(await page.APIRequest.GetAsync($"{app.BaseUrl}/register")).Status.Should().Be(200, "register page stays accessible showing closed notice when feature off");`

---

## Lane I — Dashboard, Shell, PWA, Mobile & Accessibility

Routes: `/` (dashboard), nav menu, `/mcp` landing, PWA, help tips; cross-cutting mobile/a11y/page-health

<!-- Lane I findings appended below -->


---

## Lane I — Dashboard, Shell, PWA, Mobile & Accessibility (audit results)

**Audit run:** 2026-07-13 against app on `http://localhost:5080`, owner `owner@e2e.local`, empty DB (no bots/accounts), Edge headless.
**Routes covered:** 40 smoke-tested + mobile 360px (4 routes) + RTL Arabic + PWA checks.
**All 40 routes returned HTTP 200 with shell rendered — zero crashes, zero ErrorBoundary trips.**

### I-01 — Google Fonts stylesheet blocked by CSP on every page — custom fonts not loading
- **Severity:** high
- **Route / feature:** All pages (global `App.razor`)
- **State:** any
- **Live or fake:** n/a
- **Steps to reproduce:** 1. Open any page. 2. Open browser console. 3. Observe CSP violation.
- **Observed:** Console error on every page: `Loading the stylesheet 'https://fonts.googleapis.com/css2?family=Space+Grotesk...' violates the following Content Security Policy directive: "style-src 'self' 'unsafe-inline'". The action has been blocked.` Space Grotesk and JetBrains Mono fonts fall back to system fonts; `.app-brand-word` loses its bespoke brand kerning.
- **Expected:** Either (a) CSP `style-src` includes `https://fonts.googleapis.com` and `font-src` includes `https://fonts.gstatic.com`, or (b) fonts are self-hosted under `wwwroot/` and the external `<link>` is removed.
- **Evidence:** `tmp-audit/I/` — `_smoke.png` screenshots; console error captured for every route.
- **Suspected root cause:** `src/Web/Security/SecurityHeaders.cs` lines 11/13 — `style-src 'self' 'unsafe-inline'` and `font-src 'self' data:` do not allow `fonts.googleapis.com`/`fonts.gstatic.com`. `App.razor` lines 20-22 still reference them.
- **Which tier SHOULD catch it:** E2E — `BrandIdentityTests` or a CSP test asserting zero console errors on page load.
- **Regression-test sketch:** `Assert.Empty(consoleErrors.Where(e => e.Contains("Content Security Policy")))` on dashboard load.

### I-02 — PWA manifest served at `/manifest.webmanifest` but `<link rel=manifest>` href is relative — breaks in sub-path deployments; no test covers manifest reachability
- **Severity:** medium
- **Route / feature:** PWA — manifest link in `App.razor`
- **State:** any
- **Live or fake:** n/a
- **Steps to reproduce:** 1. Fetch `GET /manifest.json` — returns 404. 2. Fetch `GET /manifest.webmanifest` — returns 200 (`application/manifest+json`). 3. Inspect `App.razor` line 48: `<link rel="manifest" href="manifest.webmanifest" />` (relative, no leading `/`). 4. Deploy under a sub-path (e.g. `/cmind/`) and observe broken install prompt.
- **Observed:** `/manifest.json` returns 404; actual manifest is at `/manifest.webmanifest` (200). The `<link>` uses a relative href (no leading `/`) which breaks in any sub-path deployment. `service-worker.js` correctly caches `/manifest.webmanifest` with absolute path.
- **Expected:** `<link rel="manifest" href="/manifest.webmanifest" />` with absolute path.
- **Evidence:** `tmp-audit/I/audit-lane-i-results.json` (pwa.manifestStatus=404) and `audit-lane-i-results2.json` (manifestChecks).
- **Suspected root cause:** `App.razor` line 48 uses `href="manifest.webmanifest"` (relative). No E2E asserts manifest is reachable.
- **Which tier SHOULD catch it:** E2E — assert `GET /manifest.webmanifest` returns 200 and `<link rel="manifest">` href starts with `/`.
- **Regression-test sketch:** `Assert.Equal(200, (await page.APIRequest.GetAsync("/manifest.webmanifest")).Status)`.

### I-03 — "Customize" button and dashboard dialog title are hardcoded English — not localized; visible in Arabic RTL session
- **Severity:** medium
- **Route / feature:** `/` (dashboard) — Customize button and `DashboardCustomizeDialog.razor`
- **State:** any locale (observed in Arabic RTL)
- **Live or fake:** n/a
- **Steps to reproduce:** 1. Switch locale to Arabic (`/set-culture?culture=ar`). 2. Navigate to `/`. 3. "Customize" button top-right reads English. 4. Click it — dialog title "Customize dashboard"; reset button "Reset to default" (hardcoded).
- **Observed:** `Index.razor` line 27: `>Customize</MudButton>`. `DashboardCustomizeDialog.razor` lines 11, 39: `Customize dashboard`, `Reset to default` — no `@L["..."]`. RTL audit: `sampleEnglishButtons: ["cBots","Customize","Customize"]`.
- **Expected:** All user-facing text via `@L["key"]` with translations in `tools/i18n/ui-translations.json` for all 23 locales.
- **Evidence:** `tmp-audit/I/rtl_dashboard_detail.png`.
- **Suspected root cause:** `src/Web/Components/Pages/Index.razor:27` and `src/Web/Components/Dialogs/DashboardCustomizeDialog.razor:11,39`. `NoHardcodedUiTextTests` build gate appears to not be scanning these strings — possible gap in its razor parser regex.
- **Which tier SHOULD catch it:** Unit — `NoHardcodedUiTextTests` build gate (should catch on build); E2E — RTL smoke asserting no English-only buttons after locale switch.
- **Regression-test sketch:** `Assert.DoesNotContain("Customize", await page.Locator("button").AllTextContentsAsync())` after switching to Arabic.

### I-04 — Dashboard empty state (no trading accounts) shows no actionable notice — KPI cards render but with placeholder zeros; mandate 11 violated
- **Severity:** medium
- **Route / feature:** `/` (dashboard) — empty state, no trading accounts
- **State:** empty (no accounts seeded)
- **Live or fake:** n/a
- **Steps to reproduce:** 1. Start app with fresh DB (no trading accounts). 2. Navigate to `/`. 3. Observe dashboard.
- **Observed:** KPI area renders 5 stat cards with zero/placeholder values; ApexCharts renders with empty series (1 chart visible). No empty-state notice saying "Connect a trading account to start" or pointing user to `/accounts`. The "Customize" button and chart appear, giving the impression the page is fully functional when it is not.
- **Expected:** When no trading accounts are connected, dashboard should show a dependency-gating notice (mandate 11): "No trading account connected — go to Accounts to add one" with a link/button. KPI cards should be hidden or show a disabled/locked state.
- **Evidence:** `tmp-audit/I/dashboard_desktop.png`, `tmp-audit/I/dashboard_detail.png`.
- **Suspected root cause:** `Index.razor` does not check for empty accounts list before rendering KPI section. No `CalendarEnablement`-style gating for the dashboard data dependency.
- **Which tier SHOULD catch it:** E2E — `DashboardTests` (empty state): assert `[data-testid=dashboard-empty-notice]` visible and "Accounts" link present when no accounts seeded.
- **Regression-test sketch:** `Assert.True(await page.Locator("[data-testid=dashboard-empty-notice]").IsVisibleAsync())` when no accounts.

### I-05 — RTL mode: `<html dir=rtl>` set correctly but no E2E asserts computed flow direction on nav/shell elements
- **Severity:** low
- **Route / feature:** Shell/layout — RTL mode (Arabic locale)
- **State:** Arabic locale
- **Live or fake:** n/a
- **Steps to reproduce:** 1. Switch to Arabic. 2. Navigate to `/`. 3. `html[dir]=rtl`, `html[lang]=ar` correct. Nav renders Arabic text. Only 2 sub-elements carry explicit `dir=rtl` attribute — MudBlazor v8 relies on `html[dir]` cascade.
- **Observed:** RTL works visually (Arabic nav text, reversed layout). No E2E asserts `getComputedStyle(nav).direction === "rtl"` programmatically. If MudBlazor v8 RTL behavior changes, regression would only be caught visually.
- **Expected:** At least one E2E assertion on computed direction of a key shell element (nav, appbar) in Arabic locale.
- **Evidence:** `tmp-audit/I/rtl_dashboard_detail.png`.
- **Suspected root cause:** RTL E2E tests assert locale switch occurs but do not assert computed CSS direction on elements.
- **Which tier SHOULD catch it:** E2E.
- **Regression-test sketch:** `Assert.Equal("rtl", await page.Locator("nav").EvaluateAsync<string>("el => getComputedStyle(el).direction"))` in Arabic locale.

### I-06 — `/economic-calendar/series/US.CPI` shows blank chart area on first load — no loading indicator while data fetches
- **Severity:** low
- **Route / feature:** `/economic-calendar/series/{code}` — series detail chart
- **State:** fresh DB (no historical series data yet)
- **Live or fake:** n/a
- **Steps to reproduce:** 1. Navigate to `/economic-calendar/series/US.CPI` immediately after app start (no historical data ingested yet). 2. Page renders without crash. 3. Chart area is blank with no loading skeleton/spinner.
- **Observed:** `hasChartEco=0`; no `.apexcharts-canvas` at `networkidle`; no loading indicator visible. User sees a blank white area.
- **Expected:** Loading skeleton or spinner while async chart data is fetched; or a "No data yet — historical data is being ingested" notice.
- **Evidence:** `tmp-audit/I/eco_series.png`.
- **Suspected root cause:** `EconomicCalendarSeries.razor` makes an async API call post-render; blank state has no fallback UI. Fresh DB has no series data.
- **Which tier SHOULD catch it:** E2E — series detail test should either seed data and assert chart, or assert loading skeleton present.
- **Regression-test sketch:** `Assert.True(await page.Locator(".apexcharts-canvas, [data-testid=series-loading]").CountAsync() > 0)`.

### I-07 — Service worker registered; offline fallback exists but no E2E tests the offline path
- **Severity:** low
- **Route / feature:** PWA — offline fallback (`/offline.html`)
- **State:** offline
- **Live or fake:** n/a
- **Steps to reproduce:** 1. Service worker registered at scope `http://localhost:5080/`. 2. `/offline.html` returns 200 with content. 3. No E2E simulates network offline and asserts the fallback renders.
- **Observed:** `swRegistered=true`, `offlineStatus=200`, `hasContent=true`. PWA shell criteria met (manifest + SW). Offline path entirely untested.
- **Expected:** E2E using `context.SetOfflineAsync(true)` navigates and asserts `/offline.html` or "offline" message renders.
- **Evidence:** `tmp-audit/I/audit-lane-i-results2.json` (offline section).
- **Suspected root cause:** PWA offline scenario not included in any E2E test class.
- **Which tier SHOULD catch it:** E2E.
- **Regression-test sketch:** `await context.SetOfflineAsync(true); await page.GotoAsync("/"); Assert.Contains("offline", (await page.TextContentAsync("body"))!, StringComparison.OrdinalIgnoreCase)`.

---

## Fix Status (post-remediation)

**Approach:** root-caused why the mandates didn't prevent these (see
`plans/audit-root-cause-and-gate-hardening.md`), hardened the enforcement gates so each *class* can't
recur, then fixed bugs across cross-cutting infra + 4 parallel worktree lanes (D/E/F/G) + main-tree
lanes (A/B/C/H).

### Recurrence-prevention gates added (CLAUDE.md mandate 12 — census, never opt-in)
- `NoHardcodedUiTextTests` → **census ratchet** over ALL components vs `pending-localization.txt` (78→ baseline). Root cause: the old gate watched 5 enrolled files while ~78 pages shipped un-localized.
- `NoWallClockInRazorTests` — no `DateTime.UtcNow/.Now` in `.razor` (A-02 class).
- `RouteExistenceTests` — no E2E may navigate a dead route (E-06 `/assistant` class).
- `DestructiveActionConfirmTests` — every DELETE/erase must confirm; ratchet baseline (13→10).
- `DomainExceptionMappingTests` — `/api` domain/persistence errors map to 400/409, not 500 (**live-verified**).

### Fixed + verified
- **Cross-cutting:** C-04/D-07/I-01 (CSP fonts), C-01/C-02/C-03 (DomainException→400/409, live 3/3), E-02/E-03 (AiFeatureNotice opt-in modal), shared `ConfirmDialog`.
- **Auth (critical):** A-08 (MustChangePassword enforced) + owner-seeder fix (was breaking the whole authed E2E suite); A-01 (pending notice), A-02 (TimeProvider), A-05 (time-based lockout column), A-06 (delete-user confirm). Verified live.
- **cBots:** B-01/B-02 (instance detail: no GUID, not-found notice — **live-verified**), B-03/B-04/B-06 (delete confirm + feedback + Load guard).
- **Copy/Users:** C-07 (Yes/No), H-02 (node API gated when Hidden — **live-verified**).
- **Lane D (merged, 12/12 E2E live):** D-01..D-11 — incl. D-02 per-row equity (data-corruption), detail dialog, breach cause, erase/delete confirms, GDPR id removal, responsive tables.
- **Lane E (merged):** E-04 (agent-studio AI gate), E-06 (stale test), E-07 (fake-LLM output asserts), E-08 (responsive tables), E-09 (agent create gate).
- **Lane F (merged, live-verified):** F-02 (journal entries render), F-04/F-05 (input guards), F-06 (equity-mode E2E), F-07 (journal notes CRUD aggregate + migration), F-08 (disposal guards).
- **Lane G (merged):** G-01 (owner-gate refresh), G-02 (series source gate), G-04 (mobile tables), G-05 (assert AI narrative), G-06 (HelpTips).

### Remaining (tracked; medium/low, no data-loss/security/crash)
- A-03 (fix flaky MFA-mobile test), A-07 (reset-password dialog vs snackbar).
- B-05 (backtest null-date validation), B-08 (instance route smoke), B-09 (navigate to new instance).
- C-05 (copy-profile deep-link route), C-06 (dialog focus trap).
- H-03 (brand live-refresh in open circuit), H-04 (feature-label mapping), H-05 (pin registration-closed behavior).
- I-02 (manifest href), I-04 (dashboard empty-state notice), I-05 (RTL computed-direction assert), I-06 (calendar-series loading indicator), I-07 (offline-path E2E).
- **Localization debt (ratcheted):** ~78 pages carry hard-coded English (A-04/B-07/E-05/F-01/H-01/I-03). The census gate now blocks any *new* un-localized page and forces the baseline to only shrink; new strings added during fixes were localized (keys seeded across all 23 locales). Paying down the full baseline is a separate, tracked effort.
- **ui-translations.json ↔ resx drift:** subagents seeded new keys directly into resx (parity green); reconcile into `ui-translations.json` before the next `gen-resx.ps1` run so the pipeline stays source-of-truth (`tmp-audit/keys-*.json` hold the fragments).
