#!/usr/bin/env bash
# Resolve the release version from a git tag ref of the form vX.Y.Z[-suffix].
# Emits, on stdout, KEY=VALUE lines suitable for $GITHUB_OUTPUT:
#   version        full SemVer, no leading v            (1.0.0-alpha.1)
#   version_prefix numeric core                         (1.0.0)
#   version_suffix pre-release label, empty if none     (alpha.1)
#   is_prerelease  true when a suffix is present         (true)
#
# Usage: scripts/release-version.sh [ref]
#   ref defaults to $GITHUB_REF (refs/tags/vX.Y.Z-...) or `git describe`.
set -euo pipefail

ref="${1:-${GITHUB_REF:-}}"
if [[ -z "$ref" ]]; then
  ref="$(git describe --tags --exact-match 2>/dev/null || true)"
fi

# Strip refs/tags/ and a single leading v.
tag="${ref#refs/tags/}"
tag="${tag#v}"

if [[ ! "$tag" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]]; then
  echo "release-version: '$ref' is not a SemVer tag (expected vX.Y.Z[-suffix])" >&2
  exit 1
fi

prefix="${tag%%-*}"
if [[ "$tag" == *-* ]]; then
  suffix="${tag#*-}"
  prerelease="true"
else
  suffix=""
  prerelease="false"
fi

{
  echo "version=$tag"
  echo "version_prefix=$prefix"
  echo "version_suffix=$suffix"
  echo "is_prerelease=$prerelease"
}
