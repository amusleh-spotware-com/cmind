---
name: translate-localization
description: >-
  MANDATORY translation playbook for localizing this app and its docs site into every supported
  language. Invoke WHENEVER you add or change a user-facing string, a doc under website/docs/**, or
  any other localizable content, and translations must be produced. Covers the two translation
  surfaces (app UI resources in tools/i18n/ui-translations.json + Docusaurus docs under
  website/i18n/<locale>/...), the full locale list, and the required workflow: translate ONLY the
  changed segments (translation memory), batched across locales, on the cheapest model. Triggers on: "translate",
  "localize", "add a string", "new doc", "i18n", "update translations", "localization gate",
  any change that adds/edits user-facing text or documentation.
---

# Translation & localization — the enforced way

This repo is **born fully localized** (CLAUDE.md mandates 8 + 9). Every user-facing string and every
doc ships translated into **all** supported languages **in the same commit**. The build fails
otherwise (`ResourceParityTests`, `NoHardcodedUiTextTests`, docs `npm run i18n:check`). This skill is
how you produce those translations fast and cheap.

## Locales (23 total — 1 base + 22 satellites)

`en` (base) + `ar cs de el es fr hu id it ja ko ms pl pt-BR ru sk sl sr th tr vi zh-Hans`

Sources of truth — keep in sync:
- App: `src/Core/Constants/SupportedCultures.cs`
- Docs: `website/docusaurus.config.ts` → `i18n.locales`

`ar` is **RTL** (`localeConfigs.ar.direction = 'rtl'`) — verify RTL renders.

## The two translation surfaces

1. **App UI strings** — `tools/i18n/ui-translations.json`. English under `cultures.en` is the base;
   every other culture is a full block with the **same keys**. After editing, regenerate the `.resx`:
   ```bash
   pwsh tools/i18n/gen-resx.ps1
   ```
   A missing/blank key in any culture fails `ResourceParityTests`. Domain stays key-based
   (`DomainErrors`), not translated here.

2. **Docs site** — for every `website/docs/**/*.{md,mdx}`, ship a translated counterpart at
   `website/i18n/<locale>/docusaurus-plugin-content-docs/current/<same-rel-path>` for **every**
   non-default locale. Verify with:
   ```bash
   cd website && npm run i18n:check
   ```

## Rule #1 — translate ONLY the diff (translation memory)

This is where the tokens and time go, and it is almost always avoidable. Mature i18n pipelines
(Crowdin, Weblate, Lokalise, i18next) **never re-translate unchanged content** — they hash each source
segment and send only *new or changed* segments to the translator, reusing the existing translation for
everything else. Do the same here. **Never re-translate a whole file when only a few keys / a few
paragraphs changed.** Re-translating the full 4,500-key UI file or a full 100-line doc × 22 locales for
a 3-key or 2-paragraph edit is the single biggest waste (it cost ~2M tokens and ~15 min last time).

Concretely, before translating anything, compute the **delta** vs the last committed English:

- **UI:** the set of keys that are **new or whose English value changed** — nothing else.
  ```bash
  # changed/added en keys vs HEAD → tools/i18n/_new/en.json (only the delta)
  node -e '
    const fs=require("fs"),cp=require("child_process");
    const cur=JSON.parse(fs.readFileSync("tools/i18n/ui-translations.json","utf8")).cultures.en;
    const old=JSON.parse(cp.execSync("git show HEAD:tools/i18n/ui-translations.json")).cultures.en;
    const d={}; for(const k in cur) if(cur[k]!==old[k]) d[k]=cur[k];
    fs.mkdirSync("tools/i18n/_new",{recursive:true});
    fs.writeFileSync("tools/i18n/_new/en.json",JSON.stringify(d,null,2));
    console.log(Object.keys(d).length,"changed keys");'
  ```
- **Docs:** the **sections that changed** (`git diff -- website/docs/<file>` → which heading-blocks were
  touched). Re-translate **only those blocks** and splice them into each existing locale file; leave every
  untouched section's existing translation **verbatim**. Full-file translation is only for a **brand-new**
  doc (no prior translation exists).

If the delta is empty, there is nothing to translate — stop. (A pure code change that adds no key and no
doc edit needs no translation.)

## Rule #2 — batch locales; don't spawn 22 agents for a small delta

A sub-agent has fixed overhead (system prompt, tool round-trips, its own reasoning). Spawning 22 of them
to translate a handful of strings pays that overhead 22×. Pick the strategy by **delta size**:

| Delta | Strategy | Calls |
|---|---|---|
| **UI: ≤ ~30 changed keys** (the common case) | **One** cheap sub-agent translates the delta into **all 22 locales at once**, emitting a single JSON `{ "<loc>": { key: value, … }, … }`. | 1 |
| **UI: > ~30 keys** | Split into a few batches, or fan out per locale (below). One agent's *output* shouldn't approach its limit — a huge single JSON truncates. | ~2–22 |
| **Docs: changed sections only** | One sub-agent per locale, but each is handed **only the changed section(s)** to translate + splice — not the whole file. | ≤22 (tiny each) |
| **Docs: brand-new file** | One sub-agent per locale, full file (unavoidable). | 22 |

The English source is read **once** into a small delta file (`_new/en.json` or the extracted section);
agents read that, **not** the 4,500-key JSON or unrelated sections. Reading the whole `ui-translations.json`
into an agent is the classic token bomb — never do it.

**Batched-UI agent (the default for UI):** one sub-agent, low effort, cheapest model. Input:
`tools/i18n/_new/en.json` (delta only). Output: **Write** `tools/i18n/_new/_all.json` =
`{ "ar": {…}, "cs": {…}, … , "zh-Hans": {…} }` — every locale, same keys, values translated, `{0}`
preserved. Then merge on the main thread (one script sets `cultures[loc][key]=value` for each and writes
`ui-translations.json` once), validate parity + placeholders, delete `_new/`, run `gen-resx.ps1`. One
agent instead of 22; only the delta instead of the whole file.

## Model, effort, batching hygiene

- **Cheapest/fastest model, low reasoning effort** — translation is low-reasoning. Pass `model: "haiku"`
  (or cheaper). Never Opus/Sonnet for bulk translation.
- **Finalize English first, then translate once** — don't re-translate after every small English edit;
  batch all English changes for the feature-area, translate the delta once at the end. Note the docs gate
  tracks a **content hash** per English doc, so **any** English doc edit (even prose-only / a typo) makes
  `npm run i18n:check` fail until you re-translate the changed sections and run `npm run i18n:sync`.
- When you **do** fan out per locale (large delta / new doc): **one agent per locale, never two for the
  same file; WAIT for all to finish before committing** (a late agent overwrites a file you already
  fixed / leaves a stray change). For docs each locale writes a **different** file → parallel is safe;
  `ui-translations.json` is **one shared file** → agents must write per-locale `_new/<loc>.json`, never
  edit the shared JSON concurrently (they clobber each other), then merge on the main thread.
- Every fan-out prompt still MUST give the **ordered heading list** (docs) and the **exact key set** (UI),
  or agents silently drop/merge — see the write protocol and templates below.

### Sub-agent WRITE PROTOCOL — the #1 cause of failed/retried translations

Sub-agents kept failing and needing re-runs for two avoidable reasons. Every sub-agent prompt MUST
spell these out, or agents thrash:

- **The target file usually ALREADY EXISTS, and the `Write` tool refuses to overwrite a file the agent
  hasn't `Read` this session.** So the agent's steps are exactly: **(1) `Read` the English source →
  (2) `Read` the existing target file → (3) `Write` the target once** with the full translation. Skipping
  the target-`Read` makes `Write` fail; the agent then improvises with the shell and corrupts the file.
- **NEVER write the file with a shell command — `Bash` heredoc / `echo` / `sed` / `printf` / PowerShell /
  Python all corrupt non-ASCII (Cyrillic, CJK, Arabic, em-dash `—`) and silently truncate.** The **only**
  allowed writer is the **`Write` tool**, in **one** call with the entire file. If `Write` seems blocked,
  the fix is to `Read` the target first — never to reach for the shell.
- **Translate EVERY line — do not summarize, merge, or drop content.** Tell the agent the source's
  **line count, bullet count (`- ` lines) and table-row count**, and require the output to match. Short
  output = dropped content = redo. (Real failures this cost us: a Vietnamese file that dropped ~20 of 43
  bullets and half the table; a Spanish file the agent wrote via shell that came out 12 corrupt lines.)
- Do not route around a deny/permission prompt via an alternate tool — if `Write` is blocked, `Read` the
  target and retry `Write`; report back rather than using PowerShell.

### Ready-to-use prompt — batched UI delta (the default; ONE agent, all 22 locales)

```
Translate app UI strings into ALL 22 target locales at once.

STEP 1 — Read the delta with the Read tool: <ROOT>/tools/i18n/_new/en.json
  (a small flat JSON of only the keys that changed: key -> English value).
STEP 2 — Write <ROOT>/tools/i18n/_new/_all.json ONCE with the Write tool. Shape:
  { "ar": { <same keys>: <Arabic> }, "cs": {…}, "de": {…}, "el": {…}, "es": {…},
    "fr": {…}, "hu": {…}, "id": {…}, "it": {…}, "ja": {…}, "ko": {…}, "ms": {…},
    "pl": {…}, "pt-BR": {…}, "ru": {…}, "sk": {…}, "sl": {…}, "sr": {…}, "th": {…},
    "tr": {…}, "vi": {…}, "zh-Hans": {…} }

HARD RULES: Use ONLY Read + Write (never a shell — it corrupts Unicode). Every locale must have the
EXACT same key set as the input. Translate values only; keep placeholders like `{0}` and `×` verbatim;
keep product/technical terms (cTrader, cBot, cID, CSV, SL, TP, lot, pip). No prose back.
```
Then on the main thread: load `_all.json`, set `cultures[loc][key]=value` for each, write
`ui-translations.json` once, validate parity + `{0}`, delete `_new/`, run `pwsh tools/i18n/gen-resx.ps1`.

### Ready-to-use prompt — docs, CHANGED SECTIONS only (edited doc)

```
Update the <LANGUAGE> (`<loc>`) translation of an edited doc — only the changed section(s).

STEP 1 — Read the English source:  <ROOT>/website/docs/<rel-path>.md
STEP 2 — Read the existing target:  <ROOT>/website/i18n/<loc>/docusaurus-plugin-content-docs/current/<rel-path>.md
STEP 3 — The English changed only in this/these section(s): <heading name(s)>. Translate JUST that/those
  section(s) and Write the target ONCE, keeping EVERY other section's existing translation byte-for-byte
  unchanged.

HARD RULES: Read + Write only (no shell). Do not retranslate or reword untouched sections. Preserve
heading levels/order, front-matter, code spans, link paths, table structure, and technical terms.
```

### Ready-to-use prompt — docs, FULL file (brand-new doc only)

```
Translate a NEW documentation page to <LANGUAGE> (`<loc>`).
STEP 1 — Read the English source. STEP 2 — Write the target path once (it may not exist yet).
Translate EVERY line: ~<N> lines, <B> `- ` bullets, a <T>-row table — output must match those counts.
Keep heading order: <explicit ordered heading list>. Front-matter/code/links/table/terms preserved.
Read + Write only; never a shell.
```

## Reconcile + verify on the main thread

- UI: merge `_new/_all.json` (or per-locale files) → `ui-translations.json`; `pwsh tools/i18n/gen-resx.ps1`.
- Docs: `cd website && npm run i18n:sync` (refresh the content-hash manifest), then `npm run i18n:check`.
  If a re-translation **fixed** grandfathered structural drift, delete those now-passing lines from
  `website/scripts/i18n-drift-baseline.txt` (the ratchet only shrinks).
- `dotnet test` the localization gates (`ResourceParityTests`, `NoHardcodedUiTextTests`); `npm run build`
  in `website/` (broken links). Delete the temp `tools/i18n/_new/` before committing.

## Definition of done

- [ ] Only the **delta** was translated — no unchanged key or untouched doc section re-translated.
- [ ] Cheapest model, low effort; UI delta batched (one call) unless it was large enough to split.
- [ ] UI: every culture has the full key set; `gen-resx.ps1` run; `ResourceParityTests` green.
- [ ] Docs: counterpart exists for every locale; `i18n:sync` run; `npm run i18n:check` green; site builds.
- [ ] No hard-coded user-facing text (`NoHardcodedUiTextTests` green); RTL renders for `ar`.
- [ ] Temp `_new/` deleted; all in the **same commit** as the English change.
