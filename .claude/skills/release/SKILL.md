---
name: release
description: Cut a new versioned GitHub release for cMind. Bumps the last release tag, verifies the release gates are green, then pushes a SemVer tag which triggers .github/workflows/release.yml to build & publish everything (signed GHCR images web/mcp/node-agent/copy-agent/tests, the Helm OCI chart, self-contained CtraderCliNode zips, SBOM + SHA256SUMS) and an auto-generated changelog. Invoke on "/release", "cut a release", "publish a release", "new alpha/beta/rc/stable release", "bump the version and release".
---

# Release cMind

Publishing is **outward-facing and effectively irreversible** (it pushes signed packages to GHCR and a
public GitHub release). The user invoking `/release` is the authorization — but still state the exact
version you will cut and what it triggers **before** pushing the tag, and stop if a preflight gate fails.

The whole mechanism is a **tag push**: `.github/workflows/release.yml` triggers on `tags: ['v*']`, and a
tag with a `-alpha`/`-beta`/`-rc` suffix publishes a GitHub **pre-release**. The per-version **changelog is
auto-generated** from the commits/PRs since the previous tag (`generate_release_notes: true`), appended to
the stable artifact template in `.github/release-body.md` — so you do **not** hand-write a changelog file;
good commit messages are the changelog.

## Steps

1. **Preflight — stop on any failure.**
   - On `main`, and up to date: `git fetch`, then working tree **clean** (`git status --porcelain` empty)
     and not behind `origin/main`. A dirty tree or unpushed work → commit/push first, don't release it.
   - Gates green (the release workflow re-runs the full suite in-image, but catch failures locally first):
     - `dotnet build` — 0 warnings, 0 errors.
     - `dotnet test tests/UnitTests` — includes `ReleaseParityTests` (keeps Dockerfiles/workflow/Helm in
       sync) and the census gates. Must be green.
     - `cd website && node scripts/check-i18n-parity.mjs` — docs translations in sync.
   - Confirm the latest CI run on `main` is green (`gh run list --branch main --limit 5`). Don't tag a red
     commit — **but distinguish a real regression from a known flaky E2E.** The E2E job flakes on unrelated
     tests under CI load (e.g. a `CopyTradingTests` 30s timeout with 425/426 passing). If the only failure is an
     E2E test **unrelated to the tagged change** and it passes in isolation, it's a flake: `gh run rerun <id>
     --failed`, wait for green, then tag. A failure in a test the change actually touches, or a build/unit/gate
     failure, is a real red — fix it, never rerun-around it.

2. **Compute the next version** from the most recent tag
   (`git tag --sort=-v:refname | head -1`, e.g. `v1.0.0-alpha.1`). Default bump = **increment the
   pre-release counter** (`v1.0.0-alpha.1` → `v1.0.0-alpha.2`). Honor an argument if given:
   - `/release` (no arg) → bump the pre-release number (alpha.N → alpha.N+1). If the last tag is a stable
     `vX.Y.Z`, start a new `-alpha.1` on the next patch (`vX.Y.(Z+1)-alpha.1`).
   - `/release beta` | `rc` → move channel and reset the counter (`-alpha.7` → `-beta.1`).
   - `/release stable` → drop the pre-release suffix at the current core version (`v1.0.0-alpha.7` →
     `v1.0.0`).
   - `/release patch` | `minor` | `major` → bump that core component and start `-alpha.1`.
   - `/release vX.Y.Z[-suffix]` → use that exact version (validate it's SemVer and strictly greater than
     the last tag).
   Never reuse or go backwards from an existing tag.

3. **State the plan, then tag.** Tell the user the exact version (e.g. "cutting `v1.0.0-alpha.2` —
   pre-release; builds 5 signed images + Helm + node zips + SBOM"). Then:
   ```bash
   git tag -a vX.Y.Z-suffix -m "vX.Y.Z-suffix"
   git push origin vX.Y.Z-suffix
   ```

4. **Report.** Give the tag and the Actions run URL (`gh run list --workflow=release.yml --limit 1`), and
   note it's a pre-release when the suffix is present. Do not edit `.github/release-body.md` per version
   (it's the stable template); the changelog rides `generate_release_notes`.

## Notes

- Pre-release tags (`-alpha`/`-beta`/`-rc`) → GitHub pre-release; a bare `vX.Y.Z` → a full release. Match
  the user's intent; default to the same channel as the last tag.
- If `ReleaseParityTests` fails, a Dockerfile/workflow/Helm drifted from a new service/image — fix that in
  a normal commit first; never tag around it.
- The tag must point at a commit already on `origin/main` (push code first, tag second).
