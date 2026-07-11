# Run one test tier (Windows). Usage: ./scripts/test.ps1 <unit|integration|e2e|stress|all>
[CmdletBinding()]
param([string] $Tier = 'all')

$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$config = if ($env:CONFIG) { $env:CONFIG } else { 'Release' }
$filter = if ($env:FILTER) { @('--filter', $env:FILTER) } else { @() }

function Run([string] $proj) { dotnet test $proj -c $config @filter }

function Install-Browsers {
  dotnet build tests/E2ETests -c $config
  $ps1 = "tests/E2ETests/bin/$config/net10.0/playwright.ps1"
  if (Test-Path $ps1) { & $ps1 install chromium } else { throw "missing $ps1" }
}

switch ($Tier) {
  'unit'        { Run 'tests/UnitTests' }
  'integration' { Run 'tests/IntegrationTests' }
  'e2e'         { Install-Browsers; Run 'tests/E2ETests' }
  'stress'      { Run 'tests/StressTests' }
  'all'         { Run 'tests/UnitTests'; Run 'tests/IntegrationTests'; Install-Browsers; Run 'tests/E2ETests' }
  default       { throw "Unknown tier '$Tier' (unit|integration|e2e|stress|all)" }
}
