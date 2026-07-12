#!/usr/bin/env node
// i18n parity gate — MANDATORY.
//
// Every documentation file under website/docs/**/*.{md,mdx} MUST have a translated counterpart in
// EVERY non-default locale, at i18n/<locale>/docusaurus-plugin-content-docs/current/<same-rel-path>.
// A new or changed doc is not "done" until it ships in all languages (see CLAUDE.md mandate #8).
//
// Exit 0 = every doc present in every locale. Exit 1 = missing translations (listed).
//
// Usage:  node scripts/check-i18n-parity.mjs [--summary]

import { readdirSync, existsSync, statSync } from 'node:fs';
import { join, relative, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const DOCS_DIR = join(ROOT, 'docs');

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

const summaryOnly = process.argv.includes('--summary');
const docs = walk(DOCS_DIR).map((p) => relative(DOCS_DIR, p).split('\\').join('/'));

const missingByLocale = {};
let missingCount = 0;

for (const locale of LOCALES) {
  const base = join(ROOT, 'i18n', locale, 'docusaurus-plugin-content-docs', 'current');
  const missing = [];
  for (const rel of docs) {
    const target = join(base, rel);
    if (!existsSync(target)) missing.push(rel);
  }
  if (missing.length) {
    missingByLocale[locale] = missing;
    missingCount += missing.length;
  }
}

const totalExpected = docs.length * LOCALES.length;
const present = totalExpected - missingCount;
console.log(`i18n docs parity: ${present}/${totalExpected} translations present (${docs.length} docs × ${LOCALES.length} locales).`);

if (missingCount === 0) {
  console.log('✓ Every doc is translated in every locale.');
  process.exit(0);
}

console.error(`\n✗ Missing ${missingCount} translations across ${Object.keys(missingByLocale).length} locale(s):\n`);
for (const [locale, missing] of Object.entries(missingByLocale)) {
  if (summaryOnly) {
    console.error(`  ${locale}: ${missing.length} missing`);
  } else {
    console.error(`  ${locale} (${missing.length}):`);
    for (const m of missing) console.error(`    - i18n/${locale}/docusaurus-plugin-content-docs/current/${m}`);
  }
}
console.error('\nEvery new/changed doc must ship in all languages. See CLAUDE.md mandate #8.');
process.exit(1);
