# Semi-automated helper for the B-section (real-machine) regression checklist.
# ASCII-only on purpose: Windows PowerShell 5.1 reads .ps1 files as ANSI.
# The checklist filename is non-ASCII, so it is located by CONTENT, not by path.
#
# Run on the target Windows machine as Administrator:
#   powershell -ExecutionPolicy Bypass -File tests\manual_regression.ps1
#   powershell ... -Mode failed                 # only items not P last time
#   powershell ... -Mode batch -Batch "B4"     # one group / id prefix
#   powershell ... -ListBatches                # show groups and counts, no prompts
#
# Results append to docs\manual-regression-results.csv so every round stays
# auditable. Failures from THIS run are also written to
# docs\manual-regression-failures.txt for easy sharing.

param(
    [ValidateSet('all', 'failed', 'batch')]
    [string]$Mode = 'all',
    [string]$Batch = '',
    [switch]$ListBatches,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$docs = Join-Path $root 'docs'
$out = Join-Path $docs 'manual-regression-results.csv'
$failOut = Join-Path $docs 'manual-regression-failures.txt'

# Locate the checklist by content (it contains rows like "| B12 | ... |").
$list = $null
foreach ($f in Get-ChildItem -Path $docs -Filter '*.md') {
    if ($f.PSIsContainer) { continue }
    $head = Get-Content -LiteralPath $f.FullName -Encoding UTF8 -TotalCount 250
    if (@($head | Where-Object { $_ -match '^\|\s*B\d+\s*\|' }).Count -gt 0) {
        $list = $f.FullName
        break
    }
}
if (-not $list) { throw 'Regression checklist not found in docs\*.md' }

$items = @()
foreach ($line in (Get-Content -LiteralPath $list -Encoding UTF8)) {
    if ($line -match '^\|\s*(B\d+)\s*\|([^|]*)\|') {
        $cols = $line.Split('|')
        $batchCol = ''
        if ($cols.Count -ge 6) { $batchCol = $cols[$cols.Count - 2].Trim() }
        $items += [pscustomobject]@{
            Id    = $matches[1]
            Desc  = $matches[2].Trim()
            Batch = $batchCol
        }
    }
}
if ($items.Count -eq 0) { throw "No B items found in $list" }

if ($ListBatches) {
    Write-Host "Checklist: $list"
    Write-Host "Total items: $($items.Count)"
    Write-Host ''
    $items | Group-Object -Property Batch | Sort-Object Name | ForEach-Object {
        $label = $_.Name
        if (-not $label) { $label = '(blank)' }
        Write-Host ("  {0,-16} {1,3} items" -f $label, $_.Count)
    }
    exit 0
}

# "failed" mode: keep only items whose LATEST recorded result is not P.
if ($Mode -eq 'failed') {
    $last = @{}
    if (Test-Path -LiteralPath $out) {
        foreach ($row in (Import-Csv -LiteralPath $out)) {
            if ($row.id) { $last[$row.id] = $row.result }
        }
    } else {
        Write-Host 'No previous result file; running the full list instead.'
    }
    $items = @($items | Where-Object {
        -not $last.ContainsKey($_.Id) -or $last[$_.Id] -ne 'P'
    })
}

if ($Mode -eq 'batch') {
    if (-not $Batch.Trim()) { throw '-Mode batch requires -Batch, e.g. -Batch "B4"' }
    $key = $Batch.Trim()
    $items = @($items | Where-Object {
        $_.Batch -like "*$key*" -or $_.Id -like "$key*"
    })
}

if ($items.Count -eq 0) { Write-Host 'Nothing to run for this selection.'; exit 0 }

if ($DryRun) {
    $ids = ($items | ForEach-Object { $_.Id }) -join ', '
    Write-Host ("Selected {0} items (mode={1}): {2}" -f $items.Count, $Mode, $ids)
    exit 0
}

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host 'WARNING: not running as Administrator. Several B items require elevation.'
}

Write-Host "PCLock manual regression: $($items.Count) items (mode=$Mode). P=pass F=fail S=skip Q=quit"
$stamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
if (-not (Test-Path -LiteralPath $out)) {
    Add-Content -LiteralPath $out -Value 'timestamp,id,description,result,notes' -Encoding UTF8
}

$runResults = @()
foreach ($it in $items) {
    Write-Host ''
    Write-Host ("[{0}] ({1}) {2}" -f $it.Id, $it.Batch, $it.Desc)
    $answer = (Read-Host 'Result (P/F/S/Q)').Trim().ToUpperInvariant()
    if ($answer -eq 'Q') { break }
    if ($answer -notin @('P', 'F', 'S')) { $answer = 'S' }
    $note = ''
    if ($answer -eq 'F') { $note = Read-Host 'Short failure note' }
    $note = $note -replace ',', ';'
    $safeDesc = $it.Desc -replace '"', ''
    Add-Content -LiteralPath $out -Value "$stamp,$($it.Id),`"$safeDesc`",$answer,`"$note`"" -Encoding UTF8
    $runResults += [pscustomobject]@{ Id = $it.Id; Desc = $it.Desc; Result = $answer; Notes = $note }
}

$nP = @($runResults | Where-Object { $_.Result -eq 'P' }).Count
$nF = @($runResults | Where-Object { $_.Result -eq 'F' }).Count
$nS = @($runResults | Where-Object { $_.Result -eq 'S' }).Count

if ($nF -gt 0) {
    $lines = @("PCLock manual regression failures - $stamp", "Total F: $nF", '')
    foreach ($r in ($runResults | Where-Object { $_.Result -eq 'F' })) {
        $lines += ("[{0}] {1} -> {2}" -f $r.Id, $r.Desc, $r.Notes)
    }
    Set-Content -LiteralPath $failOut -Value $lines -Encoding UTF8
    Write-Host "Failures written to $failOut"
}

Write-Host ''
Write-Host ("Summary: P={0} F={1} S={2} (of {3} shown)" -f $nP, $nF, $nS, $items.Count)
Write-Host "Results appended to $out"
