@echo off
setlocal
cd /d "%~dp0"

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

rem 输出目录（默认与脚本同目录）。如需输出到子目录，改这里即可，
rem 下方的配置文件复制会自动跟随到同一输出目录。
set OUTDIR=%~dp0

"%CSC%" /nologo /target:winexe /out:"%OUTDIR%PCLock.exe" /win32manifest:app.manifest /optimize+ /codepage:65001 ^
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Security.dll ^
  src\Program.cs src\App.cs src\Store.cs src\Protection.cs src\Watchdog.cs src\Questions.cs src\ChineseData.cs src\EnglishData.cs src\LockForm.cs src\SettingsForm.cs src\MaskForm.cs src\ScreenSaver.cs src\Constants.cs

if errorlevel 1 (
  echo [错误] 编译失败。
  pause
  exit /b 1
)

rem 将配置文件复制到与 PCLock.exe 相同的输出目录，确保一起分发/部署
if exist "PCLock.exe.config" copy /Y "PCLock.exe.config" "%OUTDIR%PCLock.exe.config" >nul

echo.
echo 编译成功: %OUTDIR%PCLock.exe
echo （PCLock.exe.config 已一并复制到同目录，须与 PCLock.exe 放在一起运行）
pause
