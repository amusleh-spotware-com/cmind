# Plan — Full App Localization / i18n (Blazor Server + Minimal API + MCP)

> **Goal.** Make the entire platform fully localizable and ship translations for **every language
> cTrader supports** (~23). No user-facing string may be hard-coded anywhere — enforced by a build
> **gate** (analyzer + arch-guard test + CLAUDE.md mandate) so every *new* feature is born localized
> and a non-localized string is impossible to merge.
>
> **Non-goal.** Localizing wire formats, protocol/log text, MCP tool schemas, cBot source, or
> AI-model system prompts (those stay invariant English — see §11). Nothing in this doc is
> implemented yet; this is the design + phased task list.

---

## 1. Current state (audit)

| Area | Finding |
|---|---|
| Localization infra | **None.** No `AddLocalization`, no `IStringLocalizer`, no `.resx`, no `RequestLocalization`. |
| Persistence anchor | ✅ `Core/Access/UserProfile.Locale` already exists — validated **BCP-47** string (`en-US`). Reuse as the per-user culture store. |
| Domain errors | ✅ `Core/Constants/DomainErrors.cs` already uses stable **message keys** (`domain.name.required`), not English prose. Domain is already i18n-ready — presentation resolves keys. |
| UI surface | ~80 `.razor` (Pages, Dialogs, Layout, Dashboard, Ai, Quant components) — all literal English (`MudText`, `Label=`, `aria-label=`, snackbar `.Add("…")`, page titles). |
| Layout / theme | `MainLayout.razor` → `MudThemeProvider` (no `RightToLeft`). `App.razor` `<html>` has no dynamic `lang`/`dir`. Hard-coded `aria-label`s. |
| Endpoints | Minimal API (`src/Web/Endpoints/*`) returns some user-facing strings (validation/error text). |
| Emails/notifications | Registration verification + alerts render text server-side, **out of request scope** (background) — must localize from stored `UserProfile.Locale`, not request culture. |
| Formatting | Code already correctly uses `CultureInfo.InvariantCulture` for **parse/wire** (CSV, cbotset, tryparse). Display formatting (`ToString("0.#", InvariantCulture)`) currently also invariant — display paths must move to `CurrentCulture` (§9). |
| MudBlazor built-ins | Data grid / date picker / pagination strings need `MudLocalizer` wiring. |
| Blazor mode | Interactive **Server** + some **static SSR** auth pages (Login/Register/TwoFactorChallenge). Culture is fixed per-request/per-circuit → language change needs a full reload endpoint (§6). |

**Implication:** greenfield i18n on a large surface, but two big head starts — `UserProfile.Locale`
persistence and key-based domain errors.

---

## 2. Supported languages (cTrader parity)

cTrader Web/Desktop/Mobile ships **23 interface languages** (Spotware). Target the same set. English
is the invariant fallback; every missing key falls back up the chain to `en`.

| # | Language | Culture | RTL | | # | Language | Culture | RTL |
|--|--|--|--|--|--|--|--|--|
| 1 | English | `en` | | 13 | Korean | `ko` | |
| 2 | Arabic | `ar` | ✅ | 14 | Malay | `ms` | |
| 3 | Czech | `cs` | | 15 | Polish | `pl` | |
| 4 | German | `de` | | 16 | Portuguese (BR) | `pt-BR` | |
| 5 | Greek | `el` | | 17 | Russian | `ru` | |
| 6 | Spanish | `es` | | 18 | Slovak | `sk` | |
| 7 | French | `fr` | | 19 | Slovenian | `sl` | |
| 8 | Hungarian | `hu` | | 20 | Serbian | `sr` | |
| 9 | Indonesian | `id` | | 21 | Thai | `th` | |
| 10 | Italian | `it` | | 22 | Turkish | `tr` | |
| 11 | Japanese | `ja` | | 23 | Vietnamese | `vi` | |
| 12 | | | | 24 | Chinese (Simplified) | `zh-Hans` | |

> Verify the exact list against the live cTrader ID language dropdown before freezing (Spotware
> occasionally adds languages; News feed already added 9). Persian (`fa`, RTL) is a likely near-term
> add — build the RTL path generically, not Arabic-only.

**Single source of truth:** new `Core/Constants/SupportedCultures.cs` — the ordered list + `IsRightToLeft(culture)`. Middleware, switcher, tests, and the gate all read from it.

---

## 3. Architecture decisions

1. **`Microsoft.Extensions.Localization` + `.resx` + satellite assemblies.** Native .NET, works with
   standard translation tooling (Crowdin/Lokalise/Poedit import `.resx`), zero custom runtime. Chosen
   over JSON/PO — matches the "latest best-practice for Blazor" and the repo's zero-dependency bias.
2. **Resource layout = per-area shared resources**, not one-resx-per-component (avoids 80 resx files
   and duplicate keys). Marker classes under `src/Web/Resources/`:
   - `Ui` (shared/common: buttons, generic labels, nav), `Pages`, `Dialogs`, `Validation`.
   - Domain error text lives in **`src/Core`** as `Resources/DomainErrors.resx` keyed by the
     `DomainErrors.*` key strings, resolved in the presentation layer (Core stays string-key only —
     no `IStringLocalizer` dependency added to Core; Core exposes keys, Web/Infra translate).
3. **Typed access, not magic strings.** Inject `IStringLocalizer<Ui>` (`@inject`), call `L["Key"]`.
   Keys are dotted, semantic (`nav.dashboard`, `dialog.backtest.title`). A generated `LKeys` constants
   class (or `nameof`-style) is optional polish; start with a curated key convention + the parity test.
4. **Fallback chain:** request culture → parent (`pt-BR`→`pt`→`en`) → `en` invariant resx. Never show
   a raw key to a user; missing key in prod = fallback + telemetry warning (caught pre-merge by §8).
5. **Formatting split:** *wire/storage/parse* = `InvariantCulture` (unchanged — keep every existing
   `InvariantCulture` call). *Display* (numbers, %, dates, currency in `.razor`) = `CurrentCulture`.
   Audited in Phase 4.

---

## 4. Resource organization

```
src/Core/
  Resources/
    DomainErrors.resx            # en (default) — key = DomainErrors.* value, e.g. "domain.name.required"
    DomainErrors.ar.resx … .zh-Hans.resx
  Localization/
    IDomainErrorTranslator.cs    # abstraction Core can reference (interface only; impl in Infra/Web)

src/Web/
  Resources/
    Ui.resx / Ui.<culture>.resx
    Pages.resx / Pages.<culture>.resx
    Dialogs.resx / Dialogs.<culture>.resx
    Validation.resx / Validation.<culture>.resx
  Localization/
    SupportedCultures (re-export), CultureCookie helper, LanguageSwitcher.razor, SetCultureEndpoint
```

`Web.csproj`: `<EmbeddedResource Update="Resources\*.resx"><Generator>` off (use `IStringLocalizer`,
not the designer). Satellite assemblies emitted per culture automatically.

---

## 5. Service wiring (`Program.cs`)

```csharp
builder.Services.AddLocalization();                       // resx root = "Resources"
builder.Services.Configure<RequestLocalizationOptions>(o =>
{
    o.SetDefaultCulture(SupportedCultures.Default);        // "en"
    o.AddSupportedCultures(SupportedCultures.All);         // ui + formatting
    o.AddSupportedUICultures(SupportedCultures.All);
    o.FallBackToParentCultures = true;
    o.RequestCultureProviders =                            // order = priority
    [
        new CookieRequestCultureProvider(),               // .AspNetCore.Culture (switcher + login sync)
        new AcceptLanguageHeaderRequestCultureProvider(),  // first-visit browser guess
    ];
});
// MudBlazor built-in strings:
builder.Services.AddTransient<MudLocalizer, AppMudLocalizer>();  // maps Mud keys -> Ui.resx
```

Pipeline (place **before** `MapRazorComponents`, after auth so we can read the user's stored locale):

```csharp
app.UseRequestLocalization();
```

**Login sync:** on successful sign-in, if `UserProfile.Locale` is set, write the culture cookie so the
next request (and the Blazor circuit) starts in the user's language. `AcceptLanguage` only seeds the
*first* anonymous visit.

---

## 6. Culture switching (Blazor Server reality)

A Blazor Server circuit captures `CurrentUICulture` at circuit start — you **cannot** flip language
live inside a circuit reliably. Standard, best-practice pattern:

1. `LanguageSwitcher.razor` (in `MainLayout` app bar + in `SettingsDialog` new **Language** section).
2. Selecting a language POSTs/GETs a **non-Blazor** endpoint `GET /set-culture?culture=xx&redirect=…`
   that sets the `.AspNetCore.Culture` cookie via `CookieRequestCultureProvider.MakeCookieValue`, and
   for a signed-in user **also persists** it to `UserProfile.Locale` (domain method on `AppUser`).
3. Endpoint redirects back with a **full reload** → new circuit boots in the chosen culture, RTL
   re-evaluated. Instant, no flicker beyond one navigation (matches how cTrader itself reloads).
4. Anonymous users: cookie only. Signed-in: cookie + profile (survives new device/login via §5 sync).

---

## 7. RTL support

- `SupportedCultures.IsRightToLeft(CultureInfo)` → `ar` (and future `fa`, `he`, `ur`).
- `App.razor` root: `<html lang="@culture.TwoLetterISOLanguageName" dir="@(rtl ? "rtl" : "ltr")">`.
- `MainLayout.razor`: `<MudThemeProvider RightToLeft="@rtl" … />` — MudBlazor mirrors the whole layout.
- CSS: audit `src/Web/wwwroot` + branding CSS for hard-coded `left/right`; switch to logical
  properties (`margin-inline-start`, `padding-inline-end`) where flips matter. Design tokens only.
- Icons that imply direction (chevrons, nav arrows) flip under RTL — verify in E2E snapshot.

---

## 8. THE GATE — "impossible to add non-localized text" (mandatory)

Three enforcement layers, all required before this epic is "done":

1. **Arch-guard test** (`tests/UnitTests/Localization/NoHardcodedUiTextTests.cs`, Roslyn-based):
   parse every `.razor` + endpoint file and **fail** on a user-facing literal:
   - Non-whitespace text nodes inside `MudText`/headings/`<span>`/etc.
   - Localizable attributes with literal values: `Label`, `Text`, `Title`, `Placeholder`,
     `HelperText`, `aria-label`, `alt`, snackbar `Snackbar.Add("…")`, `MudTooltip Text`.
   - Allowlist: `data-testid`, CSS classes, icon names, `nameof`, wire literals, numeric/format
     strings, and lines annotated `@* i18n-ignore: reason *@`.
   This is the hard wall — a new feature literally can't merge with a bare string.
2. **Resx parity test** (`ResourceParityTests.cs`): every key in each default `.resx` exists in **all**
   23 culture files (or is explicitly listed as fallback-allowed). Fails on missing/extra/empty keys →
   guarantees no user ever sees a raw key or an English string in a translated locale.
3. **Pseudo-localization** culture `qps-ploc` (dev/test only): wraps every resolved string
   (`[!!! Ḃáćķţêśţ !!!]`). An E2E run in pseudo-loc surfaces any un-externalized string (renders in
   plain English = a leak) and truncation/overflow bugs, at 360px width.

**CLAUDE.md mandate (new hard rule #9)** added to root + `src/Web/CLAUDE.md` + `src/Core/CLAUDE.md`:

> **9. Everything user-facing is localized.** No literal user-facing string in `.razor`, endpoints,
> emails, or notifications — inject `IStringLocalizer<…>`, add the key to the area `.resx` **and all
> supported cultures** in the same commit. Domain stays key-based (`DomainErrors`). Display formatting
> uses `CurrentCulture`; wire/parse stays `InvariantCulture`. New string with no translation = broken
> build (arch-guard + parity tests). RTL must render (`dir`, MudBlazor `RightToLeft`). → i18n plan.

Also add to **Definition of Done** checklist and (optional) a `/i18n` skill + `dotnet test
--filter Localization` step in CI (`.github/workflows`).

---

## 9. Formatting & data localization audit (Phase 4)

- **Keep invariant:** every existing `InvariantCulture` in parse/serialize paths (CSV, cbotset dates
  `dd/MM/yyyy HH:mm`, tryparse, wire) — do **not** touch; a locale must never corrupt a backtest arg.
- **Move to `CurrentCulture`:** numbers/percent/dates rendered *in the UI* — dashboard KPI cards,
  rings, tables, chart axis labels, timestamps. Provide a small `Format` helper so components don't
  each pick a culture.
- **Dates/times:** display in user culture + user timezone; storage stays UTC `DateTimeOffset`
  (TimeProvider rule unchanged).
- **Currency/money:** format with culture but keep the **account currency symbol** (don't convert
  amounts — copy-trading money is real; only formatting localizes).
- **Collation/sorting:** user-visible sorted lists use culture-aware compare; keys/ids stay ordinal.

---

## 10. Domain, validation, emails, notifications

- **Domain errors:** `DomainException(code)` already carries a key. Add `IDomainErrorTranslator`
  (Web/Infra impl over `DomainErrors.resx`); the API `ProblemDetails` mapper + Blazor error surfaces
  translate at the edge. Value objects/aggregates stay pure (Core has no `IStringLocalizer`).
- **Validation:** DataAnnotations/EditForm messages via `Validation.resx`
  (`AddDataAnnotationsLocalization`). Server-side endpoint validation returns keys → translated at edge.
- **Emails (registration verify, password reset stubs, alerts):** rendered in a **background** scope
  with **no request culture** → render each using the recipient's stored `UserProfile.Locale`
  (fallback `en`). Add a `using CultureScope(locale)` helper around email/notification composition.
- **Notifications / alert routing:** same — localize from the target user's locale, not the triggering
  request.

---

## 11. Deliberately NOT localized (invariant English)

MCP tool names/descriptions/schemas · cBot source & build logs · AI **system prompts** and
model-facing text (`TradingAgent`, `ResearchDesk` prompt builders — keep English for model quality) ·
structured logs (`LogMessages`) · wire/protocol strings · `data-testid` · exception messages meant for
developers. AI **user-facing output** (digests, reviews shown in UI) → localize by *asking the model to
respond in the user's language* (pass locale into the prompt), not via resx.

---

## 12. Testing (three tiers, per repo mandate)

- **Unit:** `SupportedCultures` correctness; `IsRightToLeft`; resx parity; arch-guard no-hardcoded;
  domain-error translator resolves every `DomainErrors.*` key in every culture.
- **Integration:** `/set-culture` sets cookie + persists `UserProfile.Locale`; `RequestLocalization`
  resolves cookie > Accept-Language > default; login syncs profile→cookie.
- **E2E (Playwright, mobile + desktop):** switch to `ar` → assert `dir="rtl"`, mirrored nav, translated
  nav labels; switch to `de`/`ja` → assert translated strings + no overflow at 360px; pseudo-loc run
  finds zero English leaks. Add culture dimension to `PageSmokeTests` — render **every** route under a
  non-English + an RTL culture without exception. Capture RTL screenshot for docs.

---

## 13. Translation workflow

- **Extraction:** Phase 1 pulls existing English strings into default `.resx` (semi-automated script
  over `.razor` + a manual pass; the arch-guard test drives completeness).
- **Seeding 23 locales:** generate first-pass translations (professional service or AI-assisted seed),
  import via `.resx`. Mark machine-seeded keys for human review; parity test ignores review-state but
  requires presence.
- **Ongoing:** new key → added to default resx + all cultures in the same commit (mandate). CI parity
  test blocks drift. Optional Crowdin/Lokalise sync on `main`.
- Fallback-to-English is always safe, so a not-yet-reviewed locale degrades gracefully, never breaks.

---

## 14. Phased rollout

**Phase 0 — Infra (no visible change).** `SupportedCultures`, packages, `AddLocalization`,
`RequestLocalization`, cookie provider, `/set-culture` endpoint, `App.razor` `lang`/`dir`,
`MudThemeProvider RightToLeft`, `MudLocalizer`, empty area resx + `en` baseline. Login→cookie sync.
*Tests:* culture resolution + RTL wiring integration/E2E.

**Phase 1 — Externalize existing UI.** Replace every literal in Layout, NavMenu, BottomNav, all
Pages, all Dialogs, Dashboard/Ai/Quant components, `aria-label`s, snackbars, page titles → keys in
`Ui`/`Pages`/`Dialogs` resx (`en`). Endpoints + validation → `Validation.resx` + domain translator.
Language switcher in app bar + Settings "Language" section. *Turn the arch-guard test ON at the end of
this phase — it now guards the whole surface.*

**Phase 2 — Translate.** Seed all 23 cultures; parity test green. Emails/notifications localized from
stored locale. RTL polish (CSS logical properties, icon flips).

**Phase 3 — Formatting audit.** Move display formatting to `CurrentCulture` (§9); currency/date/number
helper; culture-aware sorting. Keep all wire/parse invariant.

**Phase 4 — Gate + docs.** CLAUDE.md rule #9 (root + Web + Core) + Definition-of-Done item; pseudo-loc
E2E; `PageSmokeTests` culture dimension; CI filter; `website/docs/features/localization.md` +
`operations` note (how to add a language / a string). Optional `/i18n` skill.

---

## 15. Risks & mitigations

| Risk | Mitigation |
|---|---|
| Blazor circuit can't flip culture live | Full-reload `/set-culture` endpoint (§6) — standard pattern. |
| Static SSR auth pages vs interactive culture mismatch | Middleware sets culture per-request for both; cookie shared. |
| Background emails have no request culture | Explicit `CultureScope(UserProfile.Locale)` (§10). |
| Breaking a backtest/CSV by localizing a wire number | Hard rule: never touch existing `InvariantCulture`; audit only display paths. |
| RTL layout breakage | MudBlazor `RightToLeft` + logical CSS + RTL E2E snapshot. |
| Translation drift / raw keys shown | Resx parity test + English fallback + pseudo-loc leak test. |
| 23× string volume, translator cost | AI-seed + human review; fallback keeps app shippable meanwhile. |
| MCP/AI prompt accidentally localized → worse model output | §11 exclusion list + arch-guard allowlist for prompt builders. |

---

## 16. Definition of done

- [ ] All 23 cTrader cultures selectable; switcher in app bar + Settings; choice persists to
      `UserProfile.Locale` and via cookie.
- [ ] Zero hard-coded user-facing strings — arch-guard test green over `.razor` + endpoints.
- [ ] Resx parity test green: every key present in all 23 cultures; pseudo-loc E2E finds no leak.
- [ ] RTL (`ar`) renders correctly: `dir="rtl"`, mirrored MudBlazor layout, at 360px–1920px.
- [ ] Display formatting uses `CurrentCulture`; all wire/parse still `InvariantCulture` (backtests
      unaffected).
- [ ] Emails/notifications localized from stored locale.
- [ ] CLAUDE.md rule #9 + Definition-of-Done updated (root + Web + Core); CI runs the localization
      filter; `website/docs` localization page added.
- [ ] Three tiers green (unit + integration + E2E), failure/fallback paths covered.

---

### Sources
- [cTrader ID — Language](https://help.ctrader.com/ctrader-id/managing-ctid/language/)
- [Spotware — "cTrader Web 4.0 in 23 languages"](https://m.facebook.com/spotware/photos/did-you-know-ctrader-is-all-about-catering-to-its-international-client-base-this/6355125501179592/)
- [cTrader Algo — Application.Language](https://help.ctrader.com/ctrader-algo/references/Application/Language/)
- [Spotware — News in your native language](https://www.spotware.com/news/ctrader-news-in-your-native-language)
