# Standard-user DACL check (real-machine item from docs\项目交接文档 1.4).
# Run as the CHILD (standard user) account, while PCLock is running:
#   powershell -ExecutionPolicy Bypass -File tests\check_dacl.ps1
# Expected: the process cannot be terminated ("Access denied").
# Keep this file ASCII-only: Windows PowerShell 5.1 reads .ps1 as ANSI.

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
$admin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
Write-Host "Running as administrator: $admin"
if ($admin) {
    Write-Host "WARNING: run this as a standard user. An admin can always kill the process (SeDebugPrivilege)."
}

$procs = @(Get-Process -Name 'PCLock' -ErrorAction SilentlyContinue)
Write-Host "PCLock processes found: $($procs.Count)"
if ($procs.Count -eq 0) { Write-Host "INCONCLUSIVE: PCLock is not running."; exit 2 }

$blocked = 0
$allowed = 0
foreach ($p in $procs) {
    try {
        Stop-Process -Id $p.Id -ErrorAction Stop
        Write-Host "FAIL: process $($p.Id) was terminated."
        $allowed++
    } catch {
        Write-Host "PASS: process $($p.Id) could not be terminated ($($_.Exception.GetType().Name))."
        $blocked++
    }
}
Write-Host "Summary: blocked=$blocked allowed=$allowed"
if ($allowed -gt 0) { exit 1 } else { exit 0 }
