@echo off
rem Delayed expansion is REQUIRED here: inside the if-exist block below,
rem %ERRORLEVEL%/%RC% would be expanded at parse time (before the test exe
rem runs), so the script would always exit 0 and silently green-light
rem failing tests in CI. !ERRORLEVEL!/!RC! expand at execution time.
setlocal EnableDelayedExpansion
:: 单元测试运行脚本（零依赖：用系统自带 csc 编译并运行）
set CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe

"%CSC%" /nologo /target:exe /out:tests\_tests.exe /codepage:65001 /r:System.dll ^
  src\Constants.cs src\ChineseData.cs src\EnglishData.cs src\Questions.cs src\Store.cs ^
  src\Policy.cs src\BankIo.cs src\QuestionPolicy.cs src\Learning.cs src\ConfigIo.cs src\Strings.cs ^
  src\Housekeeping.cs src\AuditLog.cs src\BankSet.cs src\UiSafe.cs src\QuestionView.cs src\Diagnostics.cs src\UiText.cs ^
  tests\TUnit.cs tests\UnitTests.cs tests\HashTests.cs tests\MathAnswerTests.cs tests\OfflineDeductTests.cs ^
  tests\PolicyTests.cs tests\BankIoTests.cs tests\QuestionPolicyTests.cs tests\Batch3Tests.cs tests\Batch4Tests.cs ^
  tests\Batch5Tests.cs tests\Batch6Tests.cs tests\Batch7Tests.cs tests\Batch8Tests.cs tests\Batch9Tests.cs tests\Batch10Tests.cs tests\Batch11Tests.cs

rem NOTE: the file list above is intentionally explicit (core files only).
rem Do NOT glob src\*.cs here: it would pull in Program.cs/App.cs/LockForm.cs
rem (WinForms UI + a second Main entry point) which the test build must exclude.
rem When a new testable core file is added under src\, add it to this list.
rem (src\Store.cs deliberately does NOT reference src\Program.cs: its logging goes
rem  through an injectable sink, see Store.SetLogSink.)

rem A compile error must fail the script: without this check csc's non-zero exit
rem is swallowed by the "if exist" below and CI silently reports success.
if !ERRORLEVEL! NEQ 0 (
    echo [FAIL] test build failed
    if exist tests\_tests.exe del tests\_tests.exe
    exit /b 1
)

if exist tests\_tests.exe (
    tests\_tests.exe
    set RC=!ERRORLEVEL!
    del tests\_tests.exe
    exit /b !RC!
)
