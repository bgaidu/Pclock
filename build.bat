@echo off
setlocal EnableDelayedExpansion
cd /d "%~dp0"

rem �÷�:
rem   build.bat            -> Ĭ�� Debug������� debug\
rem   build.bat Debug      -> ����� debug\   �������Է��� /define:DEBUG /optimize-��
rem   build.bat Release    -> ����� release\ ���Ż����� /optimize+��
rem ����������ʱ�ļ�ͳһ���ڶ�Ӧ��Ŀ¼�£�����Ⱦ��Ŀ��Ŀ¼��

set CONFIG=%1
if "%CONFIG%"=="" set CONFIG=Debug
if /I "%CONFIG%"=="debug"   set CONFIG=Debug
if /I "%CONFIG%"=="release" set CONFIG=Release

rem ����ʹ�� .NET Framework 4.x �Դ��ı��������������� .NET 4.0 ����ʱ��
rem ��װ�� .NET 4.0+ �� Win7 �ϼ������У���ϵͳδ�� 4.x�����Ȱ�װ .NET Framework 4.x����
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo [����] δ�ҵ� .NET Framework 4.x ��������
  echo �밲װ .NET Framework 4.0 ����߰汾��https://dotnet.microsoft.com/download/dotnet-framework����
  if "%CI%"=="" pause
  exit /b 1
)
echo ʹ�ñ�����: %CSC%

rem ������ѡ�����ѡ��
if "%CONFIG%"=="Debug" (
  set OPTS=/debug+ /define:DEBUG;TRACE /optimize-
) else (
  set OPTS=/optimize+ /debug-
)

rem ���Ŀ¼��bin\Debug �� bin\Release��
set OUTDIR=%~dp0bin\%CONFIG%\
if not exist "%OUTDIR%" mkdir "%OUTDIR%"

rem Generate version info. Shares gen_version.bat with the VS/csproj build so the
rem generation logic exists in exactly one place. (ASCII-only comment on purpose:
rem cmd.exe reads this file with the system ANSI code page.)
call gen_version.bat

rem Source list is collected by wildcard instead of a hardcoded file list:
rem adding/moving a .cs file no longer requires editing this script (a missing
rem entry previously caused a compile failure). _Version.cs is generated above,
rem so it is picked up by the glob as well.
rem NOTE: in "for %%f in (src\*.cs)" the loop variable already carries the
rem directory (e.g. src\App.cs), so do NOT prepend "src\" again - that yields
rem src\src\App.cs and csc fails with CS2001.
set SRCS=
for %%f in (src\*.cs) do set SRCS=!SRCS! "%%f"

"%CSC%" /nologo /target:winexe /out:"%OUTDIR%PCLock.exe" /win32manifest:app.manifest /win32icon:AppIcon.ico %OPTS% /codepage:65001 ^
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Security.dll ^
  %SRCS%

if errorlevel 1 (
  echo [����] ����ʧ�ܡ�
  if "%CI%"=="" pause
  exit /b 1
)

rem ��������ʱ�����ļ������Ŀ¼���� PCLock.exe ����һ��
if exist "PCLock.exe.config" copy /Y "PCLock.exe.config" "%OUTDIR%PCLock.exe.config" >nul
if exist "chinese.txt"       copy /Y "chinese.txt"       "%OUTDIR%chinese.txt"       >nul
if exist "english.txt"       copy /Y "english.txt"       "%OUTDIR%english.txt"       >nul
if exist "AppIcon.ico"       copy /Y "AppIcon.ico"       "%OUTDIR%AppIcon.ico"       >nul

echo.
echo ����ɹ� [����: %CONFIG%]: %OUTDIR%PCLock.exe
echo ��PCLock.exe.config �� chinese.txt/english.txt ��һ�����Ƶ���Ŀ¼��
rem CI runners (GitHub Actions sets CI=true) have no stdin - pause would hang the job.
if "%CI%"=="" pause
