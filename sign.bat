@echo off
setlocal
rem ---------------------------------------------------------------------------
rem PCLock code signing helper (sign.bat)
rem
rem Requires signtool.exe from the Windows SDK. Common locations:
rem   "C:\Program Files (x86)\Windows Kits\10\bin\x64\signtool.exe"
rem   "C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\signtool.exe"
rem (It ships with Visual Studio / Windows SDK; search if the path differs.)
rem
rem USAGE:
rem   sign.bat <exe-to-sign> <pfx-path> <pfx-password>
rem
rem Two cert sources:
rem   A) Self-signed (TEST ONLY): SmartScreen still warns. Generate once in
rem      PowerShell (admin):
rem        New-SelfSignedCertificate -DnsName "PCLock" -Type CodeSigningCert `
rem          -CertStoreLocation "Cert:\CurrentUser\My"
rem      Then export to a .pfx via certmgr.msc (right-click -> export) or:
rem        $p = Get-ChildItem Cert:\CurrentUser\My | Where-Object {$_.Subject -match "PCLock"}
rem        Export-PfxCertificate -Cert $p -FilePath my.pfx -Password (ConvertTo-SecureString "pw" -AsPlainText -Force)
rem   B) Commercial / EV code-signing cert (RECOMMENDED for real distribution):
rem      Buy from a CA (DigiCert / Sectigo / GlobalSign ...). EV certs also
rem      bootstrap SmartScreen reputation. Keep the .pfx private.
rem
rem A timestamp (/tr + /td) is applied so the signature stays valid after the
rem cert expires.
rem ---------------------------------------------------------------------------

set EXE=%1
set PFX=%2
set PWD=%3

if "%EXE%"=="" ( echo Usage: sign.bat ^<exe^> ^<pfx^> ^<password^> & exit /b 1 )
if "%PFX%"=="" ( echo Missing pfx path & exit /b 1 )

rem Locate signtool.exe
set SIGNTOOL=
if exist "C:\Program Files (x86)\Windows Kits\10\bin\x64\signtool.exe" set SIGNTOOL="C:\Program Files (x86)\Windows Kits\10\bin\x64\signtool.exe"
if not defined SIGNTOOL (
  for /f "delims=" %%i in ('where signtool 2^>nul') do set SIGNTOOL=%%i
)
if not defined SIGNTOOL (
  echo signtool.exe not found. Install the Windows SDK or Visual Studio.
  exit /b 1
)

echo Signing %EXE% ...
%SIGNTOOL% sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com /f "%PFX%" /p "%PWD%" "%EXE%"
if errorlevel 1 ( echo SIGN FAILED & exit /b 1 )

echo Verifying...
%SIGNTOOL% verify /pa /v "%EXE%"
if errorlevel 1 ( echo VERIFY FAILED & exit /b 1 )

rem ---------------------------------------------------------------------------
rem Optional release-certificate pinning (P1-11): set the env var to the
rem expected SHA-256 thumbprint of the release certificate. A different
rem keystore would break over-the-top installs / trust, so fail loudly.
rem Do not wrap the PowerShell line in an if (...) block: the parentheses
rem inside the command would break cmd.exe block parsing.
rem ---------------------------------------------------------------------------
if not defined PCLOCK_EXPECTED_CERT_SHA256 goto :nocertpin
set PCLOCK_SIGN_EXE=%EXE%
powershell -NoProfile -Command "$sig = Get-AuthenticodeSignature $env:PCLOCK_SIGN_EXE; if (-not $sig.SignerCertificate) { Write-Error 'EXE is not signed'; exit 1 }; $fp = $sig.SignerCertificate.Thumbprint.Replace(' ','').ToUpperInvariant(); $want = $env:PCLOCK_EXPECTED_CERT_SHA256.Replace(' ','').ToUpperInvariant(); if ($fp -ne $want) { Write-Error ('Certificate thumbprint mismatch: ' + $fp); exit 1 }; Write-Host ('Release cert OK: ' + $fp)"
if errorlevel 1 ( echo CERT FINGERPRINT CHECK FAILED & exit /b 1 )
goto :certdone
:nocertpin
echo Hint: set PCLOCK_EXPECTED_CERT_SHA256 to enforce the release signing certificate.
:certdone

echo Done.
endlocal
