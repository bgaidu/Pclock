@echo off
setlocal
cd /d "%~dp0"

rem ����ʹ�� .NET 3.5 ��������Win7 �Դ��������� Win7 ������������п⣩
set CSC=%WINDIR%\Microsoft.NET\Framework\v3.5\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework64\v3.5\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo [����] δ�ҵ� .NET Framework ��������
  echo ����"������� - ���� - ���û�ر� Windows ����"������ .NET Framework 3.5 �����ԡ�
  pause
  exit /b 1
)
echo ʹ�ñ�����: %CSC%

"%CSC%" /nologo /target:winexe /out:PCLock.exe /win32manifest:app.manifest /optimize+ /codepage:65001 ^
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Security.dll ^
  src\Program.cs src\App.cs src\Store.cs src\MathUtil.cs src\Protection.cs src\Watchdog.cs src\LockForm.cs src\SettingsForm.cs src\Constants.cs

if errorlevel 1 (
  echo [����] ����ʧ�ܡ�
  pause
  exit /b 1
)
echo.
echo �������: %~dp0PCLock.exe
pause
