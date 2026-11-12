@echo off
rem PCLock offline upgrade helper launcher.
rem ASCII-only comments: cmd.exe reads .bat with the system ANSI code page,
rem and non-ASCII bytes may be parsed as command separators.
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0upgrade.ps1" %*
set RC=%ERRORLEVEL%
echo.
if not "%RC%"=="0" (
  echo [FAILED] Upgrade did not complete. Nothing changed, or rollback has run.
) else (
  echo [OK] Upgrade finished. Now start PCLock.exe as administrator once.
)
echo.
if not "%PCLOCK_NO_PAUSE%"=="1" pause
exit /b %RC%
