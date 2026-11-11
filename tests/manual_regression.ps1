# Semi-automated helper for the B-section (real-machine) regression checklist.
# Run on the target Windows machine as Administrator:
#   powershell -ExecutionPolicy Bypass -File tests\manual_regression.ps1
# For each B item type P (pass), F (fail) or S (skip). Results are appended to
# docs\manual-regression-results.csv so each round stays auditable.
# Keep this file ASCII-only: Windows PowerShell 5.1 reads .ps1 as ANSI.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$list = Join-Path $root 'docs\回归验证清单.md'
$out = Join-Path $root 'docs\manual-regression-results.csv'

if (-not (Test-Path $list)) { throw "Checklist not found: $list" }
$lines = Get-Content -Path $list -Encoding UTF8
$items = @()
foreach ($line in $lines) {
    if ($line -match '^\|\s*(B\d+)\s*\|([^|]*)\|') {
        $items += [pscustomobject]@{ Id = $matches[1]; Desc = $matches[2].Trim() }
    }
}
if ($items.Count -eq 0) { throw "No B items found in $list" }

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "WARNING: not running as Administrator. Several B items require elevation."
}

Write-Host "PCLock manual regression: $($items.Count) items. P=pass F=fail S=skip Q=quit"
$stamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
if (-not (Test-Path $out)) {
    Add-Content -Path $out -Value 'timestamp,id,description,result,notes' -Encoding UTF8
}
foreach ($it in $items) {
    Write-Host ""
    Write-Host "[$($it.Id)] $($it.Desc)"
    $answer = Read-Host 'Result (P/F/S/Q)'
    $answer = $answer.Trim().ToUpperInvariant()
    if ($answer -eq 'Q') { break }
    if ($answer -notin @('P','F','S')) { $answer = 'S' }
    $note = ''
    if ($answer -eq 'F') { $note = Read-Host 'Short failure note' }
    $note = $note -replace ',', ';'
    Add-Content -Path $out -Value "$stamp,$($it.Id),`"$($it.Desc -replace '\"','')\",$answer,`"$note`"" -Encoding UTF8
}
Write-Host "Results written to $out"
