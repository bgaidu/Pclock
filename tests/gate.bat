@echo off
rem ===========================================================================
rem PCLock quality gate (P1-11): unit tests -> Release -> Debug -> manifest.
rem Returns 0 only when every step passes; returns 1 immediately on any failure.
rem ASCII-only comments on purpose: cmd.exe reads .bat with the system ANSI
rem code page, and non-ASCII bytes may be parsed as command separators.
rem Unit tests already include the question-bank quality gate and the
rem negative mutation suite, so no extra bank-check script is needed here.
rem A failed build does NOT delete the old exe under bin\; the CI workflow
rem has a separate artifact-timestamp check. Verify timestamps locally too.
rem ===========================================================================
setlocal EnableDelayedExpansion
cd /d "%~dp0.."
set CI=true

echo === 1/4 Unit tests (tests\run.bat) ===
call tests\run.bat
if !ERRORLEVEL! NEQ 0 (
  echo [FAIL] unit tests
  exit /b 1
)

echo === 2/4 Release build ===
call build.bat Release
if !ERRORLEVEL! NEQ 0 (
  echo [FAIL] build.bat Release
  exit /b 1
)
if not exist "bin\Release\PCLock.exe" (
  echo [FAIL] bin\Release\PCLock.exe missing
  exit /b 1
)

echo === 3/4 Debug build ===
call build.bat Debug
if !ERRORLEVEL! NEQ 0 (
  echo [FAIL] build.bat Debug
  exit /b 1
)
if not exist "bin\Debug\PCLock.exe" (
  echo [FAIL] bin\Debug\PCLock.exe missing
  exit /b 1
)

echo === 4/4 Source/test manifest sanity ===
if not exist "src\Policy.cs"       ( echo [FAIL] src\Policy.cs missing & exit /b 1 )
if not exist "src\Store.Extras.cs" ( echo [FAIL] src\Store.Extras.cs missing & exit /b 1 )
if not exist "src\Housekeeping.cs" ( echo [FAIL] src\Housekeeping.cs missing & exit /b 1 )
if not exist "src\BankSet.cs"      ( echo [FAIL] src\BankSet.cs missing & exit /b 1 )
if not exist "src\UiSafe.cs"       ( echo [FAIL] src\UiSafe.cs missing & exit /b 1 )
if not exist "tests\Batch4Tests.cs" ( echo [FAIL] tests\Batch4Tests.cs missing & exit /b 1 )
if not exist "tests\Batch5Tests.cs" ( echo [FAIL] tests\Batch5Tests.cs missing & exit /b 1 )
rem Embedded file version must be present, otherwise Explorer cannot show it
rem (gen_version.bat emits the assembly attributes; an empty value means the
rem attribute block was dropped or the script failed silently).
powershell -NoProfile -Command "$v=(Get-Item 'bin\Release\PCLock.exe').VersionInfo.FileVersion; if (-not $v) { exit 1 }; Write-Host ('[OK] FileVersion=' + $v)"
if errorlevel 1 ( echo [FAIL] exe has no FileVersion & exit /b 1 )
echo [OK] gate passed: tests + Release + Debug + manifest
echo === 5/5 UI visibility guard ===
rem Control.Visible returns false while the parent Form has not been shown yet,
rem so "if (ctrl.Visible)" is never true during constructor-time rendering.
rem That exact pattern shipped a NullReferenceException in v1.6.4: the option
rem buttons never got their Text/Tag assigned, then (int)null threw on click.
rem Implemented with PowerShell Select-String on purpose: findstr regex with
rem spaces in the pattern gets split into arguments and silently reports
rem "not found" (errorlevel 1), which would make this guard a false green.
powershell -NoProfile -Command "$bad = Get-ChildItem -Path src -Recurse -Filter *.cs | Select-String -Pattern 'if\s*\([^)]*\.Visible\s*\)' | Where-Object { $t = $_.Line.TrimStart(); -not ($t.StartsWith('//') -or $t.StartsWith('*') -or $t.StartsWith('/*')) }; if ($bad) { $bad | ForEach-Object { Write-Host ('  ' + $_.Path + ':' + $_.LineNumber + '  ' + $_.Line.Trim()) }; exit 1 }"
if errorlevel 1 (
  echo [FAIL] src uses Control.Visible as a condition - unreliable before Show
  exit /b 1
)
echo [OK] no Control.Visible conditions
exit /b 0
