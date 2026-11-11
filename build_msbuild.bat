@echo off
rem ===========================================================================
rem Optional VS/MSBuild build channel (regression item A5).
rem build.bat is the primary, zero-dependency channel; this script only helps
rem verify PCLock.csproj on a machine that actually has Visual Studio/MSBuild.
rem Keep this file pure ASCII: cmd.exe reads .bat with the system ANSI code page.
rem Usage: build_msbuild.bat [Debug|Release]
rem ===========================================================================
setlocal EnableDelayedExpansion
cd /d "%~dp0"
set CONFIG=%1
if "%CONFIG%"=="" set CONFIG=Release

set MSBUILD=
set VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe
if exist "%VSWHERE%" (
  for /f "delims=" %%i in ('"%VSWHERE%" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe') do set MSBUILD=%%i
)
if not defined MSBUILD (
  for %%p in (
    "%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    "%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
    "%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
    "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
  ) do if not defined MSBUILD if exist %%p set MSBUILD=%%~p
)
if not defined MSBUILD (
  echo [ERROR] MSBuild.exe not found. Install Visual Studio 2022 / Build Tools.
  exit /b 2
)

echo MSBuild: !MSBUILD!
echo Configuration: %CONFIG%
"!MSBUILD!" PCLock.csproj /p:Configuration=%CONFIG% /p:Platform=AnyCPU /v:minimal /nologo
if errorlevel 1 (
  echo [ERROR] MSBuild failed
  exit /b 1
)
if not exist "bin\%CONFIG%\PCLock.exe" (
  echo [ERROR] bin\%CONFIG%\PCLock.exe missing
  exit /b 1
)
echo [OK] MSBuild channel passed: bin\%CONFIG%\PCLock.exe
exit /b 0
