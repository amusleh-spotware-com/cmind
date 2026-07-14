# User Time Zone — align every displayed time to the user's zone

## Goal

Every time the app shows to a user renders in **that user's** time zone, not the server's. The zone
defaults to the user's **local (browser-detected)** zone on first visit, and the user can change it to
**any** IANA zone from Settings. The choice is **persisted in the DB** (on the user profile) and carried
across sessions/devices via cookie + login, exactly like the existing language (`Locale`) mechanism.

## Current state (investigated)

- **Storage/compute is already correct.** All timestamps are `DateTimeOffset` produced by an injected
  `TimeProvider.GetUtcNow()` (mandate 4, enforced by `ArchitectureGuardTests` +
  `NoWallClockInRazorTests`). Everything on the wire/DB is UTC. Good — no data migration of timestamps
  needed.
- **Display is broken for multi-user.** UI converts UTC→local with `.ToLocalTime()`,
  `.LocalDateTime`, or raw `ToString("yyyy-MM-dd HH:mm")` / `ToString("g")`. In **Blazor Server** these
  resolve against the **server host's** `TimeZoneInfo.Local` (or the raw UTC offset), so **every user
  sees the server's zone**, not their own. Confirmed sites (non-exhaustive — a census gate will find the
  rest):
  - `Components/Pages/Index.razor` (`Time.GetUtcNow().ToLocalTime().ToString("HH:mm:ss")`)
  - `Components/Pages/CBots.razor` (`v.Value.LocalDateTime.ToString("yyyy-MM-dd HH:mm")`)
  - `Components/Pages/EconomicCalendar.razor`, `CalendarSeries.razor`, `InstanceDetail.razor`
    (`EffectiveAt.ToLocalTime().DateTime.ToString("g")`)
  - `Components/Pages/Ai/AiCurrencyStrength.razor`, `Dashboard/DashCurrencyStrength.razor`
    (`AsOf.ToLocalTime().ToString("g")`)
  - `Components/Pages/Alerts.razor`, `Agent.razor` (`dto.ToString("yyyy-MM-dd HH:mm")` — raw offset)
  - `Dashboard/DashActivityFeed.razor`, `DashAgents.razor` (relative "x ago" — TZ-agnostic, fine)
- **No per-user TZ concept.** `UserProfile` carries `Locale` (BCP-47) but nothing for time zone.
- **The pattern to mirror is localization**, already shipped end-to-end:
  - VO `Core.Localization.CultureName` (validated, `TryFrom`/`From`, `DomainException` on bad input).
  - `UserProfile.Locale` (nullable string, validated in `Create`), `AppUser.SetLocale(CultureName)`.
  - `/set-culture` endpoint (`LocalizationEndpoints`): validate → write `.AspNetCore.Culture` cookie →
    persist to profile if signed in → `LocalRedirect`.
  - `SignInAsync` (`AuthEndpoints`) seeds the culture cookie from `user.Profile.Locale` at login.
  - `LanguageSwitcher.razor` force-navigates (`forceLoad: true`) because a Blazor circuit can't flip
    culture live. Settings surface = a **Language** section in `SettingsDialog.razor`.
  - Middleware `app.UseRequestLocalization()` + `RequestLocalizationOptions` in `Program.cs`.

Time zone will reuse every one of these seams.

## Design

### 1. Domain (`src/Core`)

- **New value object `Core.Time.TimeZoneId`** (`readonly record struct`), mirroring `CultureName`:
  - `TryFrom(string?, out TimeZoneId)` / `From(string?)` — validate via
    `TimeZoneInfo.TryFindSystemTimeZoneById` (.NET 10 + ICU resolves **IANA and Windows** ids on all
    OSes). Store the **canonical IANA** id (`TimeZoneInfo.TryConvertWindowsIdToIanaId` when a Windows id
    comes in) so the DB value is portable Linux↔Windows.
  - Throw `DomainException(DomainErrors.TimeZoneNotSupported)` (new constant) on bad input.
  - Expose `ToTimeZoneInfo()`.
- **`UserProfile.TimeZone`** — new nullable `string? TimeZone { get; init; }`, validated in
  `UserProfile.Create(...)` through `TimeZoneId` (blank → null, like `Locale`).
- **`AppUser.SetTimeZone(TimeZoneId tz)`** → `Profile = Profile with { TimeZone = tz.Value };`
  (mirror `SetLocale`, Entities.cs).
- **`Core.Constants.SupportedTimeZones`** — a curated, ordered list for the picker (major zones grouped
  by region) plus a helper to enumerate the full `TimeZoneInfo.GetSystemTimeZones()` for "any zone".
  Native display names via `TimeZoneInfo.DisplayName`. (Curated list keeps the UI usable; validation
  still accepts any system zone.)

### 2. Persistence (`src/Infrastructure`)

- Map `Profile.TimeZone` with `HasMaxLength(64)` next to `Locale` in `DataContext.cs`.
- **New EF migration** `AddUserTimeZone` (migrations were squashed to a single `InitialCreate`; add a new
  one, don't edit the snapshot by hand): `dotnet ef migrations add AddUserTimeZone -p src/Infrastructure
  -s src/Infrastructure -o Persistence/Migrations`. Adds one nullable `TimeZone` column to the owned
  profile columns.

### 3. Effective-zone resolution (`src/Web`)

- **`IUserTimeZone` (scoped)** + impl resolving the current request/circuit's zone in priority order:
  1. Signed-in user's **persisted** `Profile.TimeZone`.
  2. **Cookie** `.Cmind.TimeZone` (browser-detected or last choice).
  3. App default `App:DefaultTimeZone` (white-label knob, below) → else **UTC**.
  Returns a cached `TimeZoneInfo`. Registered like `ICurrentUser`.
- Cookie name/const in `Core.Constants`.

### 4. Default = user's local zone (browser detection)

Blazor Server has no server-side view of the browser zone, so detect it once with JS interop and persist:
- Tiny JS: `Intl.DateTimeFormat().resolvedOptions().timeZone` (already how every web app does it).
- A small `TimeZoneInitializer` component mounted in the root layout: after first interactive render, if
  the user has **no persisted TZ and no cookie**, read the browser zone and silently `POST /set-timezone`
  (see §6) → sets cookie + persists to profile → triggers a one-time `forceLoad` refresh so the circuit
  re-resolves. Subsequent visits skip (cookie/profile present).
- Validate the incoming id server-side via `TimeZoneId.TryFrom`; ignore junk (fall back to default).
- Result: **first-ever visit already shows the user's local zone**, with zero clicks, and it's now in the
  DB for every future device.

### 5. Display — one helper, remove every `.ToLocalTime()`

- **`Web` extension/service `ToUserTime(this DateTimeOffset utc, IUserTimeZone tz)`** →
  `TimeZoneInfo.ConvertTime(utc, tz.Zone)`; plus formatting helpers that render with **`CurrentCulture`**
  (localization mandate) — e.g. `FormatDateTime`, `FormatTime`, `FormatDate`. Consider a
  `<UserTime Value="..." Format="..."/>` component so pages don't each inject the service.
- **Replace all display conversions** in `.razor` (and any endpoint/email that formats a time for a
  human) with the helper. The searches above found ~15 sites; the census gate (§10) enumerates the full
  set and keeps it at zero.
- **Wire/parse stays InvariantCulture + UTC** — MCP tool payloads, JSON API responses, `data-*`
  attributes, ApexCharts series values remain ISO-8601 UTC; only **presentation** localizes. Charts with
  a time axis either receive user-zone-shifted timestamps or a tz-aware label formatter (decide per
  chart; dashboard "updated HH:mm:ss" goes through the helper).

### 6. Change zone from Settings

- **`/set-timezone` endpoint** (new `TimeZoneEndpoints`, mirror `LocalizationEndpoints`): accept
  `tz` + `redirectUri`, validate via `TimeZoneId.TryFrom`, write `.Cmind.TimeZone` cookie (365d,
  essential, Lax), persist to `Profile.TimeZone` if signed in, `LocalRedirect(SafeRedirect(...))`. Also
  accept a silent JSON POST form for §4. `AllowAnonymous` (cookie works pre-login; profile persist gated
  on `current.UserId`).
- **`TimeZoneSwitcher.razor`** (mirror `LanguageSwitcher`): a `MudAutocomplete`/`MudSelect` over
  `SupportedTimeZones` (searchable — 400+ zones), current zone checkmarked; on pick force-navigate to
  `/set-timezone?tz=…&redirectUri=…` (`forceLoad: true`, circuit can't flip zone live).
- **New "Time zone" section in `SettingsDialog.razor`** (clone the Language section: title/description/
  current-chip + switcher). Add `Section.TimeZone` to the enum + nav link (icon
  `Icons.Material.Filled.Schedule`).

### 7. Apply at login

- In `SignInAsync` (`AuthEndpoints`), after the culture cookie, seed `.Cmind.TimeZone` from
  `user.Profile.TimeZone` when present, so the first post-login circuit already renders in the user's
  zone.

### 8. White-label (mandate 10)

- Add **`App:DefaultTimeZone`** to `AppOptions` (default `UTC`) — the zone for users who haven't chosen
  and whose browser wasn't detected. Register in `Core/WhiteLabel/WhiteLabelCatalog`, surface in owner
  **Settings → Deployment**, reach via `IWhiteLabelSettings`/`IOptionsMonitor<AppOptions>` overlay so an
  owner can retune at runtime. `WhiteLabelCatalogParityTests` will fail otherwise.

### 9. Localization (mandates 8 & 9)

- New UI keys in `tools/i18n/ui-translations.json` for **all** `SupportedCultures` + `gen-resx.ps1`:
  `settings.section.timezone`, `timezone.title`, `timezone.description`, `timezone.current`,
  `timezone.search`, `timezone.detected`. `NoHardcodedUiTextTests` + `ResourceParityTests` enforce.
- New error key `domain.timezone.not_supported` in `DomainErrors` (+ mapping if surfaced).
- **Everything documented (mandate 8, full coverage).** New canonical page
  `website/docs/features/time-zone.md` covering: how zones resolve (profile→cookie→`App:DefaultTimeZone`
  →UTC), first-visit browser detection, changing zone in Settings, the `App:DefaultTimeZone` deployment
  knob (also in `deployment/` + `operations/`), that storage/API stay UTC, and the owner white-label
  behavior (cross-link `white-label-owner-settings.md`). Update `localization.md`, `white-label-owner-
  settings.md`, and any feature doc that shows timestamps. Add the page id to `website/sidebars.ts`.
- **Localized docs:** the new/changed pages ship a translated counterpart in **every**
  `website/i18n/<locale>/docusaurus-plugin-content-docs/current/…` locale (all 22) in the same commit —
  parity gate `npm run i18n:check` fails the build otherwise. `npm run build` clean (no broken links).

### 10. Census gate (mandate 12)

- **`NoServerLocalTimeInRazorTests`** (opt-out ratchet, model on `NoWallClockInRazorTests`): scan every
  `.razor` for `.ToLocalTime()`, `.LocalDateTime`, and `DateTime(Offset)?…ToString(<time-format>)` not
  routed through the user-TZ helper/component; fail on any hit not in a shrink-only
  `pending-server-localtime.txt`. This is the real, machine-enforced guarantee that "all times align to
  the user's zone."
- Extend `RouteExistenceTests` with `/set-timezone`.

### 11. Tests — all three tiers, EVERY bit (mandate 2)

> **Non-negotiable:** every piece of this feature is exercised at **unit AND integration AND E2E** —
> the VO, the profile mutation, the resolver priority order, the cookie, the endpoint, the login seed,
> the browser-detection default, the display helper on **every** converted page, the white-label default
> knob, the Settings switcher, and each failure path. No bit ships untested at all three tiers where it
> can be exercised.

- **Unit:** `TimeZoneId` (valid IANA, Windows→IANA normalize, invalid throws, blank), `AppUser.SetTimeZone`
  (persists, doesn't wipe other profile fields — clone `AppUserLocaleTests`), `UserProfile.Create`
  validation, the `ToUserTime`/formatter with a `FakeTimeProvider` + fixed zone (e.g. UTC→`America/New_York`
  DST boundary) asserting the wall-clock string.
- **Integration (real Postgres):** `POST/GET /set-timezone` sets cookie + persists `Profile.TimeZone`;
  invalid tz ignored (no cookie, no write); login (`SignInAsync`) emits the TZ cookie from a seeded
  profile; round-trip the column. Clone `LocalizationFlowTests`.
- **E2E (Playwright, mobile+desktop):** open Settings → Time zone → pick a zone → a known timestamp on a
  page shifts by the expected offset and shows no server-zone value; first-visit browser detection sets a
  sensible default (drive `Intl` via context `TimezoneId`); the census gate test. Add to
  `PageSmokeTests`/`RouteCoverageTests` as applicable.
- Coverage must not drop; failure paths (bad tz, absent cookie, undetected browser, DST boundary,
  Windows→IANA id) covered. Target 100% line/branch on new code.

### Tier-by-tier coverage matrix (every bit)

| Bit | Unit | Integration | E2E |
|---|---|---|---|
| `TimeZoneId` validate/normalize/throw | ✔ | — | — |
| `UserProfile.TimeZone` validation | ✔ | ✔ (round-trip) | — |
| `AppUser.SetTimeZone` | ✔ | ✔ (persist) | — |
| EF column + migration | — | ✔ | — |
| `IUserTimeZone` priority (profile>cookie>default) | ✔ | ✔ | ✔ |
| `/set-timezone` (cookie+persist+reject junk) | — | ✔ | ✔ |
| Login seeds TZ cookie | — | ✔ | ✔ |
| Browser-detect default (first visit) | ✔ (parse) | — | ✔ (context `TimezoneId`) |
| Display helper / `<UserTime>` per page | ✔ (format) | — | ✔ (shifted value shown) |
| `App:DefaultTimeZone` white-label | ✔ | ✔ | ✔ (Deployment settings) |
| Settings → Time zone switcher | — | — | ✔ (mobile+desktop) |
| Census gate no server-local-time | ✔ (`NoServerLocalTimeInRazorTests`) | — | — |

## Definition of done

- [ ] `TimeZoneId` VO + `UserProfile.TimeZone` + `AppUser.SetTimeZone`; Core has no infra deps.
- [ ] EF migration `AddUserTimeZone`; column mapped.
- [ ] `IUserTimeZone` resolver + `.Cmind.TimeZone` cookie + `/set-timezone` endpoint.
- [ ] Browser-detect default via JS interop, silently persisted on first visit.
- [ ] Every `.razor`/email time goes through the user-TZ helper; **zero** `.ToLocalTime()`/`.LocalDateTime`
      display sites; wire/API stays UTC+Invariant.
- [ ] Settings → Time zone section + `TimeZoneSwitcher`; login seeds the cookie.
- [ ] `App:DefaultTimeZone` catalogued + Deployment-surfaced + `IWhiteLabelSettings`-reachable.
- [ ] All UI strings localized in every locale; docs page + all i18n translations; parity gates green.
- [ ] `NoServerLocalTimeInRazorTests` census gate added and green; `/set-timezone` in RouteExistence.
- [ ] Unit + integration + E2E green, failure paths included; coverage not lowered.
- [ ] `dotnet build` 0 warnings; analyzer sweep clean on touched projects; `get_file_problems` clean.
