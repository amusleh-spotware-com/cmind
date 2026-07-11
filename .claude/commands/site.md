---
description: Build the Docusaurus docs site and check for broken links
allowed-tools: Bash(bash scripts/site.sh*), Bash(cd website*)
---

Build the canonical docs site. Arguments: `$ARGUMENTS` = `build` (default) | `serve` | `start`.

Run `bash scripts/site.sh $ARGUMENTS`. A production `build` fails on broken links — run it before any
docs PR. If it reports broken links, fix the offending Markdown (canonical docs live under
`website/docs/`; sidebar ids in `website/sidebars.ts`).
