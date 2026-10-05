@echo off
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
echo Using: %CSC%
"%CSC%" /nologo /target:winexe /out:PCLock.exe /win32manifest:app.manifest /optimize+ /codepage:65001 ^
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Security.dll ^
  src\Program.cs src\App.cs src\Store.cs src\MathUtil.cs src\Protection.cs src\Watchdog.cs src\LockForm.cs src\SettingsForm.cs src\Constants.cs
if errorlevel 1 (
  echo [BUILD FAILED]
  exit /b 1
)
echo [BUILD OK] %~dp0PCLock.exe