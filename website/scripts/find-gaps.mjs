#!/usr/bin/env node
// Gap detector for the i18n docs grind. A locale's doc counts as DONE only if it exists AND its body
// differs from the English source (catches both missing files and untranslated English-placeholder
// copies that some translation agents leave behind). Writes gaps.json = { locale: [relPath, ...] }
// for re-dispatch, and prints a per-locale summary.

import { readdirSync, readFileSync, existsSync, writeFileSync, rmSync } from 'node:fs';
import { join, relative, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const DOCS = join(ROOT, 'docs');
const LOCALES = ['ar','cs','de','el','es','fr','hu','id','it','ja','ko','ms','pl','pt-BR','ru','sk','sl','sr','th','tr','vi','zh-Hans'];

function walk(d) {
  const out = [];
  for (const e of readdirSync(d, { withFileTypes: true })) {
    const p = join(d, e.name);
    if (e.isDirectory()) out.push(...walk(p));
    else if (/\.(md|mdx)$/.test(e.name)) out.push(p);
  }
  return out;
}

// Strip YAML front matter, collapse whitespace — for an "is this still English?" comparison.
function body(text) {
  let t = text.replace(/^﻿/, '');
  if (t.startsWith('---')) {
    const end = t.indexOf('\n---', 3);
    if (end !== -1) t = t.slice(t.indexOf('\n', end + 1) + 1);
  }
  return t.replace(/\s+/g, ' ').trim();
}

const docs = walk(DOCS)
  .map((p) => relative(DOCS, p).split('\\').join('/'))
  .filter((r) => r !== 'intro.md');

const srcBody = new Map(docs.map((r) => [r, body(readFileSync(join(DOCS, r), 'utf8'))]));

const PRUNE = process.argv.includes('--prune');
const gaps = {};
let doneTotal = 0;
let pruned = 0;
for (const locale of LOCALES) {
  const base = join(ROOT, 'i18n', locale, 'docusaurus-plugin-content-docs', 'current');
  const need = [];
  for (const r of docs) {
    const t = join(base, r);
    if (!existsSync(t)) { need.push(r); continue; }
    const tb = body(readFileSync(t, 'utf8'));
    const src = srcBody.get(r);
    // Compact scripts (CJK/Thai) render far fewer characters than English for the same content, so a
    // length ratio would false-flag real translations — for them only "missing/empty/English" counts.
    const compact = ['ja', 'ko', 'zh-Hans', 'th'].includes(locale);
    const minRatio = compact ? 0.18 : 0.5;
    const isGap = tb.length === 0 || tb === src || tb.length < src.length * minRatio;
    if (isGap) {
      need.push(r);
      if (PRUNE) { rmSync(t); pruned++; } // remove stub/English copy so it is cleanly re-created
    }
  }
  if (need.length) gaps[locale] = need;
  const done = docs.length - need.length;
  doneTotal += done;
  console.log(`${locale.padEnd(8)} ${done}/${docs.length} done` + (need.length ? `  (${need.length} to do)` : '  ✓'));
}

writeFileSync(join(ROOT, 'gaps.json'), JSON.stringify(gaps, null, 0));
const total = docs.length * LOCALES.length;
console.log(`\nTOTAL ${doneTotal}/${total} translated  ·  ${total - doneTotal} remaining` + (PRUNE ? `  ·  pruned ${pruned} stub/placeholder files` : '') + `  ·  gaps.json written`);
process.exit(Object.keys(gaps).length ? 1 : 0);
