@echo off
rem ===========================================================================
rem PCLock quality gate: unit tests -> Release -> Debug -> manifest -> guards.
rem Returns 0 only when every step passes; returns 1 immediately on any failure.
rem ASCII-only comments on purpose: cmd.exe reads .bat with the system ANSI
rem code page, and non-ASCII bytes may be parsed as command separators.
rem Unit tests already include the question-bank quality gate and the
rem negative mutation suite, so no extra bank-check script is needed here.
rem A failed build does NOT delete the old exe under bin\; the CI workflow
rem has a separate artifact-timestamp check. Verify timestamps locally too.
rem ===========================================================================
setlocal EnableDelayedExpansion
cd /d "%~dp0.."
set CI=true

echo === 1/10 Unit tests (tests\run.bat) ===
call tests\run.bat
if !ERRORLEVEL! NEQ 0 (
  echo [FAIL] unit tests
  exit /b 1
)

echo === 2/10 Release build ===
call build.bat Release
if !ERRORLEVEL! NEQ 0 (
  echo [FAIL] build.bat Release
  exit /b 1
)
if not exist "bin\Release\PCLock.exe" (
  echo [FAIL] bin\Release\PCLock.exe missing
  exit /b 1
)

echo === 3/10 Debug build ===
call build.bat Debug
if !ERRORLEVEL! NEQ 0 (
  echo [FAIL] build.bat Debug
  exit /b 1
)
if not exist "bin\Debug\PCLock.exe" (
  echo [FAIL] bin\Debug\PCLock.exe missing
  exit /b 1
)

echo === 4/10 Source/test manifest sanity ===
if not exist "src\Policy.cs"       ( echo [FAIL] src\Policy.cs missing & exit /b 1 )
if not exist "src\Store.Extras.cs" ( echo [FAIL] src\Store.Extras.cs missing & exit /b 1 )
if not exist "src\Housekeeping.cs" ( echo [FAIL] src\Housekeeping.cs missing & exit /b 1 )
if not exist "src\BankSet.cs"      ( echo [FAIL] src\BankSet.cs missing & exit /b 1 )
if not exist "src\UiSafe.cs"       ( echo [FAIL] src\UiSafe.cs missing & exit /b 1 )
if not exist "src\QuestionView.cs" ( echo [FAIL] src\QuestionView.cs missing & exit /b 1 )
if not exist "src\Diagnostics.cs"  ( echo [FAIL] src\Diagnostics.cs missing & exit /b 1 )
if not exist "tests\Batch4Tests.cs" ( echo [FAIL] tests\Batch4Tests.cs missing & exit /b 1 )
if not exist "tests\Batch5Tests.cs" ( echo [FAIL] tests\Batch5Tests.cs missing & exit /b 1 )
if not exist "tests\Batch6Tests.cs" ( echo [FAIL] tests\Batch6Tests.cs missing & exit /b 1 )
if not exist "tests\Batch7Tests.cs" ( echo [FAIL] tests\Batch7Tests.cs missing & exit /b 1 )
if not exist "tests\Batch8Tests.cs" ( echo [FAIL] tests\Batch8Tests.cs missing & exit /b 1 )
if not exist "src\UiText.cs"        ( echo [FAIL] src\UiText.cs missing & exit /b 1 )
if not exist "tests\Batch9Tests.cs" ( echo [FAIL] tests\Batch9Tests.cs missing & exit /b 1 )
if not exist "tools\upgrade.ps1"    ( echo [FAIL] tools\upgrade.ps1 missing & exit /b 1 )
if not exist "tools\upgrade.bat"    ( echo [FAIL] tools\upgrade.bat missing & exit /b 1 )
rem Embedded file version must be present, otherwise Explorer cannot show it
rem (gen_version.bat emits the assembly attributes; an empty value means the
rem attribute block was dropped or the script failed silently).
powershell -NoProfile -Command "$v=(Get-Item 'bin\Release\PCLock.exe').VersionInfo.FileVersion; if (-not $v) { exit 1 }; Write-Host ('[OK] FileVersion=' + $v)"
if errorlevel 1 ( echo [FAIL] exe has no FileVersion & exit /b 1 )

echo === 5/10 UI visibility guard ===
rem Control.Visible returns false while the parent Form has not been shown yet,
rem so "if (ctrl.Visible)" is never true during constructor-time rendering.
rem That exact pattern shipped a NullReferenceException in v1.6.4: the option
rem buttons never got their Text/Tag assigned, then (int)null threw on click.
rem Implemented with PowerShell Select-String on purpose: findstr regex with
rem spaces in the pattern gets split into arguments and silently reports
rem "not found" (errorlevel 1), which would make this guard a false green.
powershell -NoProfile -Command "$bad = Get-ChildItem -Path src -Recurse -Filter *.cs | Select-String -Pattern 'if\s*\([^)]*\.Visible\s*\)' | Where-Object { $t = $_.Line.TrimStart(); -not ($t.StartsWith('//') -or $t.StartsWith('*') -or $t.StartsWith('/*')) }; if ($bad) { $bad | ForEach-Object { Write-Host ('  ' + $_.Path + ':' + $_.LineNumber + '  ' + $_.Line.Trim()) }; exit 1 }"
if errorlevel 1 (
  echo [FAIL] src uses Control.Visible as a condition - unreliable before Show
  exit /b 1
)
echo [OK] no Control.Visible conditions

echo === 6/10 Reliability batch wiring ===
rem Refuse to ship if the offline upgrade helper, diagnostics entry, checksum
rem asset or the pure UI renderer is silently bypassed / dropped.
powershell -NoProfile -Command "$rel = Get-Content '.github\workflows\release.yml' -Raw; $lf = Get-Content 'src\LockForm.cs' -Raw; $pf = Get-Content 'src\PracticeForm.cs' -Raw; $tr = Get-Content 'src\App.Tray.cs' -Raw; if ($rel -notmatch 'SHA256SUMS.txt') { Write-Host '  checksum asset missing'; exit 1 }; if ($lf -notmatch 'QuestionView.Build') { Write-Host '  LockForm bypasses QuestionView'; exit 1 }; if ($pf -notmatch 'QuestionView.Build') { Write-Host '  PracticeForm bypasses QuestionView'; exit 1 }; if ($tr -notmatch 'ExportDiagnostics') { Write-Host '  diagnostics entry missing'; exit 1 }"
if errorlevel 1 (
  echo [FAIL] reliability wiring missing
  exit /b 1
)
echo [OK] reliability wiring

echo === 7/10 Live language switch wiring ===
rem Strings is a pure-logic layer: it must NOT reference WinForms. Refresh is
rem decoupled through LanguageChanged, cached source text and dynamic delegates.
powershell -NoProfile -Command "$st = Get-Content 'src\Strings.cs' -Raw; $pg = Get-Content 'src\Program.cs' -Raw; $sf = Get-Content 'src\SettingsForm.cs' -Raw; $lf = Get-Content 'src\LockForm.cs' -Raw; $uf = Get-Content 'src\UsageForm.cs' -Raw; $pf = Get-Content 'src\PracticeForm.cs' -Raw; $af = Get-Content 'src\AboutForm.cs' -Raw; $tr = Get-Content 'src\App.Tray.cs' -Raw; if ($st -notmatch 'event Action LanguageChanged') { Write-Host '  Strings event missing'; exit 1 }; if ($pg -notmatch 'LanguageChanged \+= RefreshLanguage') { Write-Host '  Program subscription missing'; exit 1 }; if ($pg -notmatch 'RegisterDynamicRefresh') { Write-Host '  dynamic refresh API missing'; exit 1 }; if ($sf -notmatch 'Strings.SetLanguage') { Write-Host '  language menu not using SetLanguage'; exit 1 }; foreach ($f in @($sf,$lf,$uf,$pf,$af,$tr)) { if ($f -notmatch 'RegisterDynamicRefresh') { Write-Host '  a window has no dynamic refresh delegate'; exit 1 } }; if ($lf -match 'pinOkBtn.Text = Strings.Get') { Write-Host '  constructor pre-translates a static label'; exit 1 }"
if errorlevel 1 (
  echo [FAIL] live language switch wiring missing
  exit /b 1
)
echo [OK] live language switch wiring
echo === 8/10 Answer feedback / translation wiring ===
rem OnWrong must keep the answered question on screen until the child clicks
rem Continue; rebuilding the batch immediately makes the answer mismatch the
rem displayed question. Pure logic and hard-lock / PIN translations stay wired.
powershell -NoProfile -Command "$lf = Get-Content 'src\LockForm.cs' -Raw; $pc = Get-Content 'src\Policy.cs' -Raw; $st = Get-Content 'src\Strings.cs' -Raw; $bad = @(); if ($lf -notmatch 'feedbackPending') { $bad += 'feedbackPending missing' }; if ($lf -notmatch 'AnswerFeedback.BlocksAnswerInput') { $bad += 'answer input guard missing' }; if ($pc -notmatch 'public static string StatusText') { $bad += 'AnswerFeedback.StatusText missing' }; $m = [regex]::Match($lf, 'void OnWrong\(\)[\s\S]*?(?=\r?\n        void )'); if (-not $m.Success) { $bad += 'OnWrong body not found' } elseif ($m.Value -match 'StartBatch\(\)') { $bad += 'OnWrong rebuilds batch too early' } elseif ($m.Value -match 'correctCount\s*=\s*0') { $bad += 'OnWrong resets progress to zero' }; if ($lf -notmatch 'continueBtn.Click[\s\S]{0,700}?AdvanceAfterWrong\(\)') { $bad += 'manual continue does not advance' }; if ($lf -notmatch 'AddQuestionAfterWrong') { $bad += 'wrong-answer refill missing' }; if ($lf -notmatch 'AnswerFlow.StarsAfterWrong') { $bad += 'star deduction not wired' }; foreach ($k in @('Daily usage quota reached','Eye-rest break','System clock changed - protective lock','Current rules:','Allowed hours:')) { if ($st -notmatch [regex]::Escape($k)) { $bad += ('missing translation: ' + $k) } }; if ($bad.Count -gt 0) { $bad | ForEach-Object { Write-Host ('  ' + $_) }; exit 1 }"
if errorlevel 1 (
  echo [FAIL] answer feedback / translation wiring missing
  exit /b 1
)
echo [OK] answer feedback / translation wiring
echo === 9/10 CI tag-only trigger guard ===
rem GitHub must never auto-run a workflow unless a v* tag is pushed.
rem Editing on: back to push branches / pull_request would silently re-enable
rem compilation on every commit, so refuse to ship in that case.
powershell -NoProfile -Command "$r = Get-Content '.github\workflows\release.yml' -Raw; $t = Get-Content '.github\workflows\tests.yml' -Raw; $bad = @(); if ($r -notmatch 'tags:') { $bad += 'release.yml has no tags trigger' }; if ($r -notmatch 'github.ref_type == ''tag''') { $bad += 'release.yml job tag guard missing' }; if ($r -match 'workflow_dispatch:') { $bad += 'release.yml manual trigger re-enabled' }; if ($t -match 'pull_request:') { $bad += 'tests.yml PR auto-trigger re-enabled' }; if ($t -match 'push:') { $bad += 'tests.yml push auto-trigger re-enabled' }; if ($bad.Count -gt 0) { $bad | ForEach-Object { Write-Host ('  ' + $_) }; exit 1 }"
if errorlevel 1 (
  echo [FAIL] CI is no longer tag-only
  exit /b 1
)
echo [OK] CI is tag-only
echo === 10/10 UI layering / dynamic text wiring ===
rem WinForms z-order: controls added LAST sit at the BOTTOM. parentPanel was
rem added last in AddCoreControls() and got hidden behind the question label,
rem so the parent PIN box was invisible and unclickable. Guard the fix and the
rem UiText pure-function wiring that keeps runtime labels translatable.
powershell -NoProfile -Command "$lf = Get-Content 'src\LockForm.cs' -Raw; $bad = @(); if ($lf -notmatch 'parentPanel.BringToFront\(\)') { $bad += 'parent panel never brought to front' }; if ($lf -notmatch 'UiText.PinLabel') { $bad += 'UiText.PinLabel not wired' }; if ($lf -notmatch 'UiText.HardLockQuestion') { $bad += 'UiText.HardLockQuestion not wired' }; if ($lf -notmatch 'UiText.WindowStatus') { $bad += 'UiText.WindowStatus not wired' }; if ($bad.Count -gt 0) { $bad | ForEach-Object { Write-Host ('  ' + $_) }; exit 1 }"
if errorlevel 1 (
  echo [FAIL] UI layering / dynamic text wiring missing
  exit /b 1
)
echo [OK] UI layering / dynamic text wiring
echo [OK] gate passed: tests + Release + Debug + manifest + guards
exit /b 0
