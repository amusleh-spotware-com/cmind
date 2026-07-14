# Releasing cMind

cMind releases are **tag-driven**: pushing a SemVer tag runs
[`.github/workflows/release.yml`](.github/workflows/release.yml), which builds, signs, and publishes
everything and creates the GitHub Release. No manual uploads.

While pre-1.0 stabilises, **every release is a pre-release** (`-alpha.N`, later `-beta.N` / `-rc.N`).

## Versioning

SemVer 2.0.0, tag form `vX.Y.Z[-suffix]`:

- `VersionPrefix` (`X.Y.Z`) is the single source of truth in [`Directory.Build.props`](Directory.Build.props).
- The tag suffix (`alpha.N`) is passed to the build as `-p:VersionSuffix=` — no source edit per alpha.
- A suffix ⇒ GitHub **pre-release** and it stays out of "Latest".
- Image tags and the Helm chart version both equal the version **without** the leading `v`.

## What a tag publishes

| Artifact | Location |
|---|---|
| Images `cmind-{web,mcp,node-agent,copy-agent,tests}` | `ghcr.io/amusleh-spotware-com/cmind-*:<version>` (+ `sha-<commit>`), cosign-signed, provenance-attested, SBOM |
| Helm chart | `oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind` + `cmind-<version>.tgz` on the release |
| CtraderCliNode binaries | self-contained zips: `linux-x64`, `linux-arm64`, `win-x64`, `osx-arm64` |
| Checksums | `SHA256SUMS.txt` over every asset |

## Cut a release

1. **Green `main`.** CI must be green on the commit you tag; the release job re-gates on build +
   unit + integration and will not publish on red.
2. **Update the changelog.** Move `Unreleased` entries under a new `## [X.Y.Z-alpha.N]` heading in
   [`CHANGELOG.md`](CHANGELOG.md), commit to `main`.
3. **Tag and push:**

   ```bash
   git tag v1.0.0-alpha.1
   git push origin v1.0.0-alpha.1
   ```

4. **Watch the run** under Actions → *Release*. On success the GitHub Release appears with all assets,
   auto-generated notes appended to the changelog snippet.

## Bumping the next alpha

Just push the next tag — `v1.0.0-alpha.2`, then `-alpha.3`, … When ready to graduate: `-beta.1`,
`-rc.1`, then the final `v1.0.0` (no suffix, published as a normal — non-pre — release).

## Prerequisites (one-time, org/repo settings)

- GHCR publishing uses the built-in `GITHUB_TOKEN` (`packages: write`) — no PAT needed for CI.
- Make each `cmind-*` GHCR package **public** (or document the pull secret) if consumers are external.
- cosign signing + provenance use OIDC (`id-token: write`) — already granted in the workflow.
