@echo off
setlocal
:: 单元测试运行脚本（零依赖：用系统自带 csc 编译并运行）
set CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe

"%CSC%" /nologo /target:exe /out:tests\_tests.exe /codepage:65001 /r:System.dll ^
  src\Constants.cs src\ChineseData.cs src\EnglishData.cs src\Questions.cs src\Store.cs ^
  tests\TUnit.cs tests\UnitTests.cs

if exist tests\_tests.exe (
    tests\_tests.exe
    set RC=%ERRORLEVEL%
    del tests\_tests.exe
    exit /b %RC%
)
