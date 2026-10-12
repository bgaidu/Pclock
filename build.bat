@echo off
setlocal
cd /d "%~dp0"

rem 优先使用 .NET Framework 4.x 自带的编译器。程序使用了 C# 6 字符串插值等特性，
rem .NET 3.5 的 csc 无法编译，因此此处直接要求 4.0+（多数 Win7 机器已随系统/常用软件安装）。
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo [错误] 未找到 .NET Framework 4.x 编译器。
  echo 请安装 .NET Framework 4.0 或更高版本（https://dotnet.microsoft.com/download/dotnet-framework）。
  pause
  exit /b 1
)
echo 使用编译器: %CSC%

"%CSC%" /nologo /target:winexe /out:PCLock.exe /win32manifest:app.manifest /optimize+ /codepage:65001 ^
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Security.dll ^
  src\Program.cs src\App.cs src\Store.cs src\Protection.cs src\Watchdog.cs src\Questions.cs src\ChineseData.cs src\EnglishData.cs src\LockForm.cs src\SettingsForm.cs src\MaskForm.cs src\ScreenSaver.cs src\Constants.cs

if errorlevel 1 (
  echo [错误] 编译失败。
  pause
  exit /b 1
)
echo.
echo 编译成功: %~dp0PCLock.exe
pause
