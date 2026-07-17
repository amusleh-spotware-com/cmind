---
name: translate-localization
description: >-
  MANDATORY translation playbook for localizing this app and its docs site into every supported
  language. Invoke WHENEVER you add or change a user-facing string, a doc under website/docs/**, or
  any other localizable content, and translations must be produced. Covers the two translation
  surfaces (app UI resources in tools/i18n/ui-translations.json + Docusaurus docs under
  website/i18n/<locale>/...), the full locale list, and the required workflow: fan out one
  sub-agent PER LANGUAGE using the fastest/cheapest model at low effort. Triggers on: "translate",
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

## Workflow — FAN OUT ONE SUB-AGENT PER LANGUAGE (mandatory)

Do **not** translate all 22 languages inline on the main thread — it burns the main context and is
slow. Instead:

1. **Prepare the base — and BATCH.** Finalize the English source first; nothing translates until English
   is locked. A full doc's 22-locale fan-out is expensive (22 sub-agents × the whole doc), so **do NOT
   re-translate after every small English edit** — make ALL the English changes for the feature-area,
   then translate the doc **once** at the end.
   **A prose-only English edit is NOT exempt** (common misconception): the docs gate tracks a **content
   hash** of each English source in a freshness manifest, so *any* change to an English doc — even a typo
   fix that touches no heading — makes `npm run i18n:check` fail with "changed without re-syncing" until
   you re-translate that doc into **all 22 locales** and run `npm run i18n:sync` to update the manifest.
   So: batch every English edit, then translate + `i18n:sync` once at the end.
   Each sub-agent prompt MUST include the **explicit list of headings in order** (a doc with 13 headings
   → say "13 headings, this order: …") — without it agents silently drop/merge sections.
2. **Fan out.** Spawn **one sub-agent per target locale** (22 of them), background where possible. Each
   sub-agent handles exactly one language end-to-end and **obeys the write protocol below**:
   - **Docs:** each locale writes a **different** file (`website/i18n/<locale>/...`) — safe to run all 22
     in parallel.
   - **UI (`tools/i18n/ui-translations.json`) is ONE shared file — 22 agents editing it in parallel
     clobber each other.** Do **not** have sub-agents edit it concurrently. Instead: write the English
     keys to a temp `tools/i18n/_new/en.json` (flat key→English), have each sub-agent **write only its own
     locale's translations to a separate `tools/i18n/_new/<locale>.json`** (same keys), then **merge on
     the main thread** with a small Node/PowerShell script (load each `_new/<locale>.json`, set
     `cultures[locale][key]=value`, write `ui-translations.json` once) and validate parity + placeholders
     (`{0}`) before deleting the temp dir. This avoids the clobber and lets the fan-out stay parallel.
3. **Model + effort — cheapest/fastest, low effort.** Translation is a low-reasoning task. Each
   sub-agent MUST use the **fastest and cheapest available model** (e.g. Haiku / `claude-haiku-4-5`,
   or an even cheaper one if available) with **low reasoning effort**. Do NOT use Opus/Sonnet for bulk
   translation — waste of tokens and time. Pass `model: "haiku"` (or cheapest) to the Agent tool.
4. **Constraints every sub-agent obeys:**
   - Translate values only. **Never** change keys, placeholders (`{0}`, `{name}`), markdown
     structure, code fences, front-matter ids, or links.
   - Keep the exact same key set as `en` (UI) / same file path + headings (docs).
   - Preserve technical/ubiquitous-language terms (cBot, backtest, ParamSet, Node, cTrader) — don't
     invent synonyms; keep product names untranslated.
   - RTL (`ar`): translate normally; direction is handled by config.
   - **One agent per locale, never two for the same file. WAIT for every agent to finish before you
     commit** — a slow agent that lands after your commit leaves a stray uncommitted change (and can
     overwrite a file you already fixed). Re-run the parity gate after the last agent, then commit once.

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

### Ready-to-use sub-agent prompt template (docs)

```
Translate a documentation page from English to <LANGUAGE> (locale `<loc>`).

STEP 1 — Read the English source with the Read tool:
  <ROOT>/website/docs/<rel-path>.md
STEP 2 — Read the existing target file with the Read tool (it already exists; Write needs this first):
  <ROOT>/website/i18n/<loc>/docusaurus-plugin-content-docs/current/<rel-path>.md
STEP 3 — Write that target path ONCE with the Write tool, containing the full translation.

HARD RULES:
- Use ONLY the Read and Write tools. NEVER Bash/echo/heredoc/sed/PowerShell/Python — they corrupt
  Unicode and truncate. One Write call, whole file.
- Translate EVERY line. The source has ~<N> lines, <B> bullet lines starting `- `, and a <T>-row table.
  Your output MUST contain all <B> bullets and all <T> table rows. Do NOT summarize, merge, or drop.
- Keep heading LEVELS + order: <explicit ordered heading list>.
- Preserve YAML front-matter fences + keys (translate the `description:` VALUE only).
- Do NOT change code spans/backticked identifiers, Markdown link paths (translate only visible text),
  the table structure, or product/technical terms (cTrader, cBot, cID, CSV, SL, TP, lot, pip,
  master/slave, and any others). No prose back — just Write the file.
```

For the **UI** surface, point the agent at `tools/i18n/_new/en.json` (Read it) and have it **Write**
`tools/i18n/_new/<loc>.json` (a new file — no target-Read needed) with the same keys, values translated,
placeholders (`{0}`) preserved. Then merge on the main thread (step 2 above).
5. **Reconcile + verify on the main thread** after fan-out:
   - `pwsh tools/i18n/gen-resx.ps1` (regenerate resx)
   - `cd website && npm run i18n:check` (docs parity)
   - `dotnet test` the localization gates (`ResourceParityTests`, `NoHardcodedUiTextTests`)
   - `npm run build` in `website/` (catches broken links).

## Definition of done

- [ ] English base locked first.
- [ ] All 22 satellites produced via per-language sub-agents on the cheapest/fastest model, low effort.
- [ ] UI: every culture has the full key set; `gen-resx.ps1` run; `ResourceParityTests` green.
- [ ] Docs: translated counterpart exists for every locale; `npm run i18n:check` green; site builds.
- [ ] No hard-coded user-facing text (`NoHardcodedUiTextTests` green); RTL renders for `ar`.
- [ ] All in the **same commit** as the English change.
