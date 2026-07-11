#!/usr/bin/env pwsh
# Windows-native wrapper for scripts/k8s-e2e.sh (the cross-platform runner).
#
# PREFERRED on Windows: run inside WSL (-Wsl) — native Linux paths + Docker Desktop's WSL integration avoid
# all path-translation issues. Requires docker, kind, helm, kubectl and the .NET SDK on the WSL PATH.
# DEFAULT: git-bash — also works; the .sh converts the MSYS path (/c/...) to a Windows path via `cygpath -m`.
#
#   pwsh scripts/k8s-e2e.ps1                    # git-bash
#   pwsh scripts/k8s-e2e.ps1 -Wsl               # inside WSL (preferred)
#   TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' pwsh scripts/k8s-e2e.ps1 -Wsl   # live in-cluster
param([switch]$Wsl)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

if ($Wsl) {
  if (-not (Get-Command wsl -ErrorAction SilentlyContinue)) { throw "WSL not found; run without -Wsl to use git-bash." }
  Write-Host "==> Running k8s-e2e inside WSL (preferred)" -ForegroundColor Cyan
  wsl bash -lc "cd `$(wslpath -a '$repo') && ./scripts/k8s-e2e.sh $($args -join ' ')"
} else {
  $bash = (Get-Command bash -ErrorAction SilentlyContinue)
  if (-not $bash) { throw "bash (git-bash) not found; install Git for Windows or run with -Wsl." }
  Write-Host "==> Running k8s-e2e via git-bash (paths auto-converted by the script)" -ForegroundColor Cyan
  & $bash.Source "$PSScriptRoot/k8s-e2e.sh" @args
}
exit $LASTEXITCODE
