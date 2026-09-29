@echo off
setlocal
cd /d "%~dp0"

rem 优先使用 .NET 3.5 编译器（Win7 自带，产物在 Win7 上无需额外运行库）
set CSC=%WINDIR%\Microsoft.NET\Framework\v3.5\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework64\v3.5\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo [错误] 未找到 .NET Framework 编译器。
  echo 请在"控制面板 - 程序 - 启用或关闭 Windows 功能"中启用 .NET Framework 3.5 后重试。
  pause
  exit /b 1
)
echo 使用编译器: %CSC%

"%CSC%" /nologo /target:winexe /out:PCLock.exe /win32manifest:app.manifest /optimize+ /codepage:65001 ^
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  src\Program.cs src\App.cs src\Store.cs src\MathUtil.cs src\Protection.cs src\Watchdog.cs src\LockForm.cs src\SettingsForm.cs

if errorlevel 1 (
  echo [错误] 编译失败。
  pause
  exit /b 1
)
echo.
echo 编译完成: %~dp0PCLock.exe
pause
