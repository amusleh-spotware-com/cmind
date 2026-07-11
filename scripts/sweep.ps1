# Analyzer sweep (Windows). Surfaces info-level CA/IDE rules `dotnet build` hides.
# Usage: ./scripts/sweep.ps1 [proj.csproj ...]   (no args = every src/**/*.csproj)
[CmdletBinding()]
param([Parameter(ValueFromRemainingArguments)] [string[]] $Projects)

$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

if (-not $Projects) {
  $Projects = git ls-files 'src/**/*.csproj'
}

$fail = $false
foreach ($proj in $Projects) {
  Write-Host "== analyzer sweep: $proj =="
  dotnet format analyzers $proj --verify-no-changes --severity info
  if ($LASTEXITCODE -ne 0) {
    Write-Warning "analyzer diagnostics in $proj (run without --verify-no-changes to autofix)"
    $fail = $true
  }
}

if ($fail) { Write-Error 'Analyzer sweep FAILED.'; exit 1 }
