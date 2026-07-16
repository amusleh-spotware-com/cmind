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
   then translate the doc **once** at the end. The docs structural-parity gate only checks heading levels,
   so a **prose-only** English change won't fail the build; batch those and re-translate at feature end.
   Each sub-agent prompt MUST include the **explicit list of headings in order** (a doc with 13 headings
   → say "13 headings, this order: …") — without it agents silently drop/merge sections.
2. **Fan out.** Spawn **one sub-agent per target locale** (22 of them) — run them in parallel /
   background where possible. Each sub-agent handles exactly one language end-to-end:
   - UI: add/update that culture's keys in `tools/i18n/ui-translations.json`.
   - Docs: write the translated file(s) under `website/i18n/<locale>/...`.
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
