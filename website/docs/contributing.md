---
slug: /contributing
title: Contributing
description: How to contribute to cMind — human or AI-assisted PRs welcome. First contribution in 10 minutes.
sidebar_position: 5
---

# Contributing to cMind 🛠️

Thanks for being here. cMind gets better every single time someone opens an issue, reports precise
cTrader behavior, fixes a typo in these very docs, or ships a PR. **You do not need to be a .NET
wizard** — testers, traders, and doc-fixers are as valued as the folks writing aggregates.

:::tip[The canonical guide lives in the repo]
This page is the friendly on-ramp. The full, always-current process — ground rules, coding
conventions, review flow — is in **[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md)**.
:::

## Your first contribution in ~10 minutes

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
dotnet restore
dotnet build          # 0 warnings, or CI will politely refuse you
dotnet test           # unit + integration + E2E
```

Found something to fix? Branch it, change it, add a test, and open a PR. That&apos;s the whole loop.

## Ways to help (not all of them are code)

| Contribution | Effort | Where |
|---|---|---|
| 🐛 Report a reproducible bug | 10 min | [Bug report](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) |
| 💡 Suggest a feature | 10 min | [Feature request](https://github.com/amusleh-spotware-com/cmind/issues/new?template=feature_request.yml) |
| 📖 Improve these docs | 15 min | Edit under `website/docs/` and PR |
| 🧪 Add a missing test | 30 min | `tests/UnitTests` · `IntegrationTests` · `E2ETests` |
| 🧠 Report exact cTrader behavior | 10 min | [Open a Discussion](https://github.com/amusleh-spotware-com/cmind/discussions) |

## The house rules (short version)

cMind moves **real money**, so a few things are non-negotiable — and honestly, they make the codebase
a joy to work in:

- **Strict Domain-Driven Design.** Business logic lives on aggregates and value objects, never in
  endpoints or UI. (There&apos;s a friendly playbook for it in the repo.)
- **Three test tiers, every change.** Unit + integration + E2E, *including* failure paths (dropped
  connections, rejected orders, dead nodes). Green tests are the price of admission.
- **Zero warnings.** `TreatWarningsAsErrors=true`. Modern C# 14 idioms.
- **No secrets, no magic strings, never `DateTime.UtcNow`** (inject `TimeProvider` instead).
- **Docs in the same commit.** Change behavior → update its doc. Yes, that includes this site.

Full detail, with the *why* behind each rule, in
[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md) and
[AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md).

## Contributing with AI 🤖

We genuinely welcome **AI-assisted PRs** — this project is built to be worked on by agents as well as
humans. If you&apos;re driving Claude, Copilot, or similar: point it at
[AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md), let it read the nested
`CLAUDE.md` files, and hold it to the same bar (tests, zero warnings, DDD). A good AI PR is
indistinguishable from a good human PR — same review, same welcome.

## Be excellent to each other

We have a [Code of Conduct](https://github.com/amusleh-spotware-com/cmind/blob/main/CODE_OF_CONDUCT.md).
The gist: be kind, assume good faith, and remember there&apos;s a person (or a person&apos;s agent) on
the other end. Ask questions early — that&apos;s a strength, not a bother.

Welcome aboard. We can&apos;t wait to see what you build. 🎉
