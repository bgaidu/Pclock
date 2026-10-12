@echo off
setlocal
cd /d "%~dp0"

rem 用法:
rem   build.bat            -> 默认 Debug，输出到 debug\
rem   build.bat Debug      -> 输出到 debug\   （含调试符号 /define:DEBUG /optimize-）
rem   build.bat Release    -> 输出到 release\ （优化开启 /optimize+）
rem 产物与运行时文件统一放在对应子目录下，不污染项目根目录。

set CONFIG=%1
if "%CONFIG%"=="" set CONFIG=Debug
if /I "%CONFIG%"=="debug"   set CONFIG=Debug
if /I "%CONFIG%"=="release" set CONFIG=Release

rem 优先使用 .NET Framework 4.x 自带的编译器。产物面向 .NET 4.0 运行时，
rem 在装有 .NET 4.0+ 的 Win7 上即可运行（如系统未带 4.x，请先安装 .NET Framework 4.x）。
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo [错误] 未找到 .NET Framework 4.x 编译器。
  echo 请安装 .NET Framework 4.0 或更高版本（https://dotnet.microsoft.com/download/dotnet-framework）。
  pause
  exit /b 1
)
echo 使用编译器: %CSC%

rem 按配置选择编译选项
if "%CONFIG%"=="Debug" (
  set OPTS=/debug+ /define:DEBUG;TRACE /optimize-
) else (
  set OPTS=/optimize+
)

rem 输出目录（与脚本同级的 debug\ 或 release\）
set OUTDIR=%~dp0%CONFIG%\
if not exist "%OUTDIR%" mkdir "%OUTDIR%"

"%CSC%" /nologo /target:winexe /out:"%OUTDIR%PCLock.exe" /win32manifest:app.manifest %OPTS% /codepage:65001 ^
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Security.dll ^
  src\Program.cs src\App.cs src\Store.cs src\Protection.cs src\Watchdog.cs src\Questions.cs src\ChineseData.cs src\EnglishData.cs src\LockForm.cs src\SettingsForm.cs src\MaskForm.cs src\ScreenSaver.cs src\Constants.cs

if errorlevel 1 (
  echo [错误] 编译失败。
  pause
  exit /b 1
)

rem 复制运行时所需文件到输出目录（与 PCLock.exe 放在一起）
if exist "PCLock.exe.config" copy /Y "PCLock.exe.config" "%OUTDIR%PCLock.exe.config" >nul
if exist "chinese.txt"       copy /Y "chinese.txt"       "%OUTDIR%chinese.txt"       >nul
if exist "english.txt"       copy /Y "english.txt"       "%OUTDIR%english.txt"       >nul

echo.
echo 编译成功 [配置: %CONFIG%]: %OUTDIR%PCLock.exe
echo （PCLock.exe.config 与 chinese.txt/english.txt 已一并复制到该目录）
pause
