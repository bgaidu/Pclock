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
if not exist "tests\Batch4Tests.cs" ( echo [FAIL] tests\Batch4Tests.cs missing & exit /b 1 )
echo [OK] gate passed: tests + Release + Debug + manifest
exit /b 0
