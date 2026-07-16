#!/usr/bin/env node
// i18n parity gate — MANDATORY.
//
// Every documentation file under website/docs/**/*.{md,mdx} MUST have a translated counterpart in
// EVERY non-default locale, at i18n/<locale>/docusaurus-plugin-content-docs/current/<same-rel-path>,
// AND that counterpart must be STRUCTURALLY in sync with the English source: the same sequence of
// Markdown heading levels (#, ##, ###, …). Heading TEXT is translated (so it differs), but a heading
// added to or removed from the English doc that is not mirrored in a locale is a stale translation and
// FAILS the build. This is what stops an English-only doc edit from silently shipping with the other
// languages left behind (see CLAUDE.md mandate #8).
//
// Pre-existing drift is grandfathered via an OPT-OUT RATCHET: scripts/i18n-drift-baseline.txt lists
// the `locale<TAB>relpath` pairs that were already out of sync when the gate landed. The gate fails on:
//   • any MISSING translation,
//   • any drift NOT in the baseline (a doc you changed without re-syncing its translations), and
//   • any baseline entry that is NO LONGER drifting (the ratchet only tightens — remove it).
// So the baseline can only shrink; new stale translations can never be introduced.
//
// Exit 0 = all present, and drift == baseline exactly. Exit 1 otherwise (details listed).
//
// Usage:  node scripts/check-i18n-parity.mjs [--summary]
//         node scripts/check-i18n-parity.mjs --list-drift   (prints current drift as locale<TAB>rel, for regenerating the baseline)

import { readFileSync, readdirSync, existsSync } from 'node:fs';
import { join, relative, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const DOCS_DIR = join(ROOT, 'docs');
const BASELINE_FILE = join(ROOT, 'scripts', 'i18n-drift-baseline.txt');

// Non-default locales — must mirror docusaurus.config.ts i18n.locales minus the defaultLocale ("en").
const LOCALES = [
  'ar', 'cs', 'de', 'el', 'es', 'fr', 'hu', 'id', 'it', 'ja', 'ko', 'ms',
  'pl', 'pt-BR', 'ru', 'sk', 'sl', 'sr', 'th', 'tr', 'vi', 'zh-Hans',
];

function walk(dir) {
  const out = [];
  for (const e of readdirSync(dir, { withFileTypes: true })) {
    const p = join(dir, e.name);
    if (e.isDirectory()) out.push(...walk(p));
    else if (/\.(md|mdx)$/.test(e.name)) out.push(p);
  }
  return out;
}

// The sequence of heading levels in a Markdown doc (e.g. [1, 2, 2, 3, 2]). Skips fenced code blocks
// (``` / ~~~) so a shell `# comment` inside a code sample is never mistaken for a heading, and skips
// the YAML frontmatter block. Language-agnostic: only the # depth matters, not the heading text.
function headingLevels(filePath) {
  const lines = readFileSync(filePath, 'utf8').split(/\r?\n/);
  const levels = [];
  let inFence = false;
  let fenceMarker = '';
  let inFrontmatter = false;
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    if (i === 0 && line.trim() === '---') { inFrontmatter = true; continue; }
    if (inFrontmatter) { if (line.trim() === '---') inFrontmatter = false; continue; }
    const fence = line.match(/^\s*(```+|~~~+)/);
    if (fence) {
      if (!inFence) { inFence = true; fenceMarker = fence[1][0]; }
      else if (fence[1][0] === fenceMarker) { inFence = false; }
      continue;
    }
    if (inFence) continue;
    const h = line.match(/^(#{1,6})\s/);
    if (h) levels.push(h[1].length);
  }
  return levels;
}

const key = (locale, rel) => `${locale}\t${rel}`;

const docs = walk(DOCS_DIR).map((p) => relative(DOCS_DIR, p).split('\\').join('/'));
const englishLevels = Object.fromEntries(docs.map((rel) => [rel, headingLevels(join(DOCS_DIR, rel))]));

const missing = [];       // key strings
const currentDrift = new Set(); // key strings

for (const locale of LOCALES) {
  const base = join(ROOT, 'i18n', locale, 'docusaurus-plugin-content-docs', 'current');
  for (const rel of docs) {
    const target = join(base, rel);
    if (!existsSync(target)) { missing.push(key(locale, rel)); continue; }
    const en = englishLevels[rel];
    const loc = headingLevels(target);
    if (en.length !== loc.length || en.some((v, i) => v !== loc[i])) currentDrift.add(key(locale, rel));
  }
}

if (process.argv.includes('--list-drift')) {
  console.log([...currentDrift].sort().join('\n'));
  process.exit(0);
}

const baseline = new Set(
  existsSync(BASELINE_FILE)
    ? readFileSync(BASELINE_FILE, 'utf8').split(/\r?\n/).map((l) => l.trim()).filter((l) => l && !l.startsWith('#'))
    : [],
);

const summaryOnly = process.argv.includes('--summary');
const unexpectedDrift = [...currentDrift].filter((k) => !baseline.has(k)).sort();
const resolvedBaseline = [...baseline].filter((k) => !currentDrift.has(k)).sort();

const totalExpected = docs.length * LOCALES.length;
const present = totalExpected - missing.length;
console.log(`i18n docs parity: ${present}/${totalExpected} translations present (${docs.length} docs × ${LOCALES.length} locales).`);
console.log(`structural drift: ${currentDrift.size} current, ${baseline.size} grandfathered in the baseline.`);

let failed = false;

if (missing.length) {
  failed = true;
  console.error(`\n✗ Missing ${missing.length} translation file(s):`);
  for (const k of (summaryOnly ? missing.slice(0, 10) : missing)) {
    const [locale, rel] = k.split('\t');
    console.error(`    - i18n/${locale}/docusaurus-plugin-content-docs/current/${rel}`);
  }
}

if (unexpectedDrift.length) {
  failed = true;
  console.error(`\n✗ ${unexpectedDrift.length} translation(s) drifted from the English structure and are NOT grandfathered — you changed a doc's headings without re-syncing its translations. Re-sync them (or, only if truly pre-existing, add to scripts/i18n-drift-baseline.txt):`);
  for (const k of (summaryOnly ? unexpectedDrift.slice(0, 20) : unexpectedDrift)) {
    const [locale, rel] = k.split('\t');
    console.error(`    - ${locale}: ${rel}`);
  }
}

if (resolvedBaseline.length) {
  failed = true;
  console.error(`\n✗ ${resolvedBaseline.length} baseline entr(y/ies) are no longer drifting — the ratchet only tightens, so remove them from scripts/i18n-drift-baseline.txt:`);
  for (const k of (summaryOnly ? resolvedBaseline.slice(0, 20) : resolvedBaseline)) {
    const [locale, rel] = k.split('\t');
    console.error(`    - ${locale}: ${rel}`);
  }
}

if (!failed) {
  console.log('✓ Every doc is translated; structural drift matches the (shrink-only) baseline exactly.');
  process.exit(0);
}

console.error('\nEvery new/changed doc must ship — fully and structurally in sync — in all languages. See CLAUDE.md mandate #8.');
process.exit(1);
