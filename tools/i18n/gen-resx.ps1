#!/usr/bin/env pwsh
# Generates the app-shell UI .resx files from tools/i18n/ui-translations.json.
# English ('en') -> src/Web/Resources/Ui.resx (base); every other culture -> Ui.<culture>.resx.
# Run after editing the JSON:  pwsh tools/i18n/gen-resx.ps1
[CmdletBinding()]
param(
    [string]$Json = "$PSScriptRoot/ui-translations.json",
    [string]$OutDir = "$PSScriptRoot/../../src/Web/Resources"
)

$ErrorActionPreference = 'Stop'
$data = Get-Content -Raw -Path $Json | ConvertFrom-Json

$header = @'
<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
'@

function Convert-XmlText([string]$s) {
    return $s.Replace('&', '&amp;').Replace('<', '&lt;').Replace('>', '&gt;')
}

$cultures = $data.cultures.PSObject.Properties
foreach ($culture in $cultures) {
    $name = $culture.Name
    $entries = $culture.Value
    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine($header)
    foreach ($kv in $entries.PSObject.Properties) {
        $val = Convert-XmlText([string]$kv.Value)
        [void]$sb.AppendLine("  <data name=`"$($kv.Name)`" xml:space=`"preserve`"><value>$val</value></data>")
    }
    [void]$sb.AppendLine('</root>')

    $file = if ($name -eq 'en') { 'Ui.resx' } else { "Ui.$name.resx" }
    $path = Join-Path $OutDir $file
    $utf8 = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($path, $sb.ToString(), $utf8)
    Write-Host "Wrote $file ($($entries.PSObject.Properties.Count) keys)"
}
Write-Host "Done: $($cultures.Count) cultures."
