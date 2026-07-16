#!/usr/bin/env node
// Regenerates the i18n content-freshness manifest (scripts/i18n-source-manifest.json) consumed by
// check-i18n-parity.mjs. Run this AFTER you edit an English doc under docs/ AND re-translate it into every
// locale, to record the new hashes.
//
// ENFORCEMENT: if an English doc's content changed since the last sync, EVERY locale's translation must
// also have changed. If a locale file was left untouched while the English changed, this script REFUSES to
// write the manifest and exits non-zero, naming the stale translations — so you cannot "bless" an
// English-only edit without actually re-translating it. First run (no manifest) simply records current state.
//
// Usage: node scripts/i18n-sync-manifest.mjs   (or: npm run i18n:sync)

import { readFileSync, writeFileSync, readdirSync, existsSync } from 'node:fs';
import { join, relative, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const DOCS_DIR = join(ROOT, 'docs');
const MANIFEST_FILE = join(ROOT, 'scripts', 'i18n-source-manifest.json');

const LOCALES = [
  'ar', 'cs', 'de', 'el', 'es', 'fr', 'hu', 'id', 'it', 'ja', 'ko', 'ms',
  'pl', 'pt-BR', 'ru', 'sk', 'sl', 'sr', 'th', 'tr', 'vi', 'zh-Hans',
];

// Identical normalization to check-i18n-parity.mjs contentHash — keep in sync.
function contentHash(filePath) {
  const norm = readFileSync(filePath, 'utf8').replace(/\r\n/g, '\n').replace(/\s+$/, '') + '\n';
  return createHash('sha256').update(norm).digest('hex');
}

function walk(dir) {
  const out = [];
  for (const e of readdirSync(dir, { withFileTypes: true })) {
    const p = join(dir, e.name);
    if (e.isDirectory()) out.push(...walk(p));
    else if (/\.(md|mdx)$/.test(e.name)) out.push(p);
  }
  return out;
}

const localePath = (locale, rel) =>
  join(ROOT, 'i18n', locale, 'docusaurus-plugin-content-docs', 'current', rel);

const docs = walk(DOCS_DIR).map((p) => relative(DOCS_DIR, p).split('\\').join('/')).sort();
const old = existsSync(MANIFEST_FILE) ? JSON.parse(readFileSync(MANIFEST_FILE, 'utf8')) : null;

const next = {};
const missingLocale = [];   // `${locale}: ${rel}`
const staleLocale = [];     // `${locale}: ${rel}` — English changed but this translation did not

for (const rel of docs) {
  const en = contentHash(join(DOCS_DIR, rel));
  const locales = {};
  const englishChanged = old?.[rel] ? old[rel].en !== en : false;
  for (const locale of LOCALES) {
    const p = localePath(locale, rel);
    if (!existsSync(p)) { missingLocale.push(`${locale}: ${rel}`); continue; }
    const h = contentHash(p);
    locales[locale] = h;
    if (englishChanged && old[rel].locales?.[locale] === h) staleLocale.push(`${locale}: ${rel}`);
  }
  next[rel] = { en, locales };
}

let failed = false;
if (missingLocale.length) {
  failed = true;
  console.error(`✗ ${missingLocale.length} translation file(s) missing — create them before syncing:`);
  for (const m of missingLocale) console.error(`    - ${m}`);
}
if (staleLocale.length) {
  failed = true;
  console.error(`\n✗ ${staleLocale.length} translation(s) were NOT updated even though their English doc changed — re-translate them, then re-run the sync (refusing to bless an English-only edit):`);
  for (const s of staleLocale) console.error(`    - ${s}`);
}
if (failed) {
  console.error('\nManifest NOT written.');
  process.exit(1);
}

writeFileSync(MANIFEST_FILE, JSON.stringify(next, null, 2) + '\n');
console.log(`✓ Wrote content manifest for ${docs.length} docs × ${LOCALES.length} locales → scripts/i18n-source-manifest.json`);
