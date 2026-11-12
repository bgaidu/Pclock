#requires -Version 2.0
<#
PCLock 离线升级助手（不联网、不改注册表、不触碰防作弊）
  1. 检查管理员权限与旧进程（要求先走「设置 -> 退出程序」）
  2. 校验 ZIP 的 SHA-256（若同目录有 SHA256SUMS.txt）
  3. 备份整个安装目录
  4. 只替换白名单文件：PCLock.exe / PCLock.exe.config / AppIcon.ico
  5. 校验替换后的文件版本；任一环节失败自动从备份回滚

明确不会动：chinese.txt、english.txt、banks\、logs\、review-*.json、
HKLM 注册表（PIN / 时长 / 时段 / 额度 / 加练池）、计划任务。
#>
[CmdletBinding()]
param(
    [string]$Zip = '',
    [string]$Source = '',
    [string]$InstallDir = 'C:\Program Files\TVLock',
    [string]$ExpectedVersion = '',
    [switch]$SkipChecksum
)

$ErrorActionPreference = 'Stop'
$WhiteList = @('PCLock.exe', 'PCLock.exe.config', 'AppIcon.ico')
$KeepList  = @('chinese.txt', 'english.txt', 'banks\', 'logs\', 'review-*.json')

function Write-Step($n, $text) { Write-Host ("[{0}/7] {1}" -f $n, $text) -ForegroundColor Cyan }
function Fail($text) { Write-Host ("[失败] " + $text) -ForegroundColor Red; exit 1 }
# 不用 Expand-Archive / Get-FileHash（PowerShell 5 才有）：Win7 自带 PS 2.0 也要能跑。
function Get-ZipSha256($path) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $stream = $null
    try {
        $stream = [System.IO.File]::OpenRead($path)
        return ([System.BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
    } finally {
        if ($null -ne $stream) { $stream.Dispose() }
        $sha.Dispose()
    }
}

# --- 1/7 管理员权限 ---
Write-Step 1 '检查管理员权限...'
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Fail '请右键 upgrade.bat -> 以管理员身份运行（Program Files 写入与备案需要提权）'
}

# --- 2/7 旧进程必须已退出 ---
Write-Step 2 '检查 PCLock 是否已退出...'
$running = @(Get-Process -Name 'PCLock' -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    Fail ("检测到 {0} 个 PCLock 进程仍在运行。请先：托盘 -> 设置（家长 PIN）-> 退出程序。" -f $running.Count) 
}
if (-not (Test-Path -LiteralPath $InstallDir -PathType Container)) {
    Fail ("安装目录不存在：{0}（用 -InstallDir 指定实际路径）" -f $InstallDir)
}
if (-not (Test-Path -LiteralPath (Join-Path $InstallDir 'PCLock.exe') -PathType Leaf)) {
    Fail ("{0} 下没有 PCLock.exe，路径可能不对" -f $InstallDir)
}

# --- 3/7 ZIP 路径与解压 ---
Write-Step 3 '定位发布包...'
$zipItem = $null
$tmp = $null
if (-not [string]::IsNullOrWhiteSpace($Source)) {
    # 低版本 PowerShell（Win7 自带 2.0）没有 Expand-Archive：家长手动解压后用 -Source 指定
    $src = $Source.Trim().Trim('"')
    Write-Host '      使用已解压目录（-Source）' -ForegroundColor Yellow
} else {
    if ([string]::IsNullOrWhiteSpace($Zip)) {
        $Zip = Read-Host '请输入 PCLock-Release.zip 的完整路径（也可直接把文件拖到本窗口）'
        $Zip = $Zip.Trim().Trim('"')
    }
    if (-not (Test-Path -LiteralPath $Zip -PathType Leaf)) { Fail ("找不到文件：{0}" -f $Zip) }
    $zipItem = Get-Item -LiteralPath $Zip
    if (-not (Get-Command Expand-Archive -ErrorAction SilentlyContinue)) {
        Fail '当前 PowerShell 版本过低（< 5.0），无法自动解压。请手动解压 ZIP 后加 -Source "<解压目录>" 重新运行。'
    }
    $tmp = Join-Path $env:TEMP ('pclock-upgrade-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
    New-Item -ItemType Directory -Path $tmp -Force | Out-Null
    try {
        Expand-Archive -LiteralPath $zipItem.FullName -DestinationPath $tmp -Force
    } catch {
        Fail ("解压失败：{0}" -f $_.Exception.Message)
    }
    $src = $tmp
}
if (-not (Test-Path -LiteralPath $src -PathType Container)) { Fail ("源目录不存在：{0}" -f $src) }
$exeItem = Get-ChildItem -Path $src -Filter 'PCLock.exe' -Recurse |
    Where-Object { -not $_.PSIsContainer } | Select-Object -First 1
if ($null -eq $exeItem) { Fail '发布包里没有 PCLock.exe，可能下错文件了' }
$src = $exeItem.DirectoryName
Write-Host ("      源目录：{0}" -f $src)

# --- 4/7 校验 ---
Write-Step 4 '校验完整性与版本...'
if (-not $SkipChecksum -and $null -eq $zipItem) {
    Write-Host '      -Source 模式：未指定 ZIP，跳过 SHA-256 校验' -ForegroundColor Yellow
}
if (-not $SkipChecksum -and $null -ne $zipItem) {
    $sumFile = Join-Path $zipItem.DirectoryName 'SHA256SUMS.txt'
    if (Test-Path -LiteralPath $sumFile -PathType Leaf) {
        $expected = ''
        foreach ($line in (Get-Content -LiteralPath $sumFile)) {
            if ($line -match '^\s*([0-9a-fA-F]{64})\s+\*?PCLock-Release\.zip\s*$') {
                $expected = $Matches[1].ToLowerInvariant(); break
            }
        }
        if ($expected -eq '') { Fail 'SHA256SUMS.txt 里没有 PCLock-Release.zip 的条目' }
        $actual = Get-ZipSha256 $zipItem.FullName
        if ($actual -ne $expected) {
            Fail ("ZIP 校验不通过！期望 {0}，实际 {1}。文件可能下载损坏或被替换，已中止。" -f $expected, $actual)
        }
        Write-Host ('      SHA-256 校验通过：' + $actual.Substring(0, 16) + '...') -ForegroundColor Green
    } else {
        Write-Host '      未找到 SHA256SUMS.txt，跳过校验（建议一并下载；或用 -SkipChecksum 显式跳过）' -ForegroundColor Yellow
    }
}

$srcVer = (Get-Item -LiteralPath $exeItem.FullName).VersionInfo.FileVersion
if ([string]::IsNullOrWhiteSpace($srcVer)) { Fail '包里的 PCLock.exe 没有文件版本信息，可能不是本仓库发布的版本' }
if (-not [string]::IsNullOrWhiteSpace($ExpectedVersion) -and ($srcVer -ne $ExpectedVersion)) {
    Fail ("版本不符：期望 {0}，包里是 {1}" -f $ExpectedVersion, $srcVer)
}
Write-Host ("      包内版本：{0}" -f $srcVer) -ForegroundColor Green

# --- 5/7 备份 ---
Write-Step 5 '备份安装目录...'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backup = Join-Path (Split-Path -Path $InstallDir.TrimEnd('\') -Parent) ("PCLock-backup-" + $stamp)
try {
    Copy-Item -LiteralPath $InstallDir -Destination $backup -Recurse -Force
} catch {
    Fail ("备份失败，已中止（不冒险替换）：{0}" -f $_.Exception.Message)
}
Write-Host ("      备份：{0}" -f $backup) -ForegroundColor Green

# --- 6/7 只替换白名单 ---
Write-Step 6 '替换白名单文件...'
$copied = New-Object 'System.Collections.Generic.List[string]'
try {
    foreach ($name in $WhiteList) {
        $from = Join-Path $src $name
        if (-not (Test-Path -LiteralPath $from -PathType Leaf)) {
            Write-Host ("      跳过（包里没有）：{0}" -f $name) -ForegroundColor Yellow
            continue
        }
        $to = Join-Path $InstallDir $name
        Copy-Item -LiteralPath $from -Destination $to -Force
        $copied.Add($name)
        Write-Host ("      已替换：{0}" -f $name)
    }
    if ($copied.Count -eq 0) { throw '一个文件都没替换成功' }

    # 替换后核对：目标 exe 版本必须与源一致
    $targetExe = Join-Path $InstallDir 'PCLock.exe'
    $dstVer = (Get-Item -LiteralPath $targetExe).VersionInfo.FileVersion
    if ($dstVer -ne $srcVer) { throw ("替换后版本不一致：{0} != {1}" -f $dstVer, $srcVer) }
}
catch {
    Write-Host ("[失败] {0}" -f $_.Exception.Message) -ForegroundColor Red
    Write-Host '正在从备份回滚...' -ForegroundColor Yellow
    foreach ($name in $copied) {
        $bak = Join-Path $backup $name
        $to = Join-Path $InstallDir $name
        try {
            if (Test-Path -LiteralPath $bak -PathType Leaf) { Copy-Item -LiteralPath $bak -Destination $to -Force }
            else { Remove-Item -LiteralPath $to -Force -ErrorAction SilentlyContinue }
        } catch { Write-Host ("      回滚 {0} 失败：{1}" -f $name, $_.Exception.Message) -ForegroundColor Red }
    }
    Fail ("已回滚。备份仍在：{0}" -f $backup)
}

# --- 7/7 收尾 ---
Write-Step 7 '完成'
Write-Host ''
Write-Host ('[完成] 已升级到 ' + $srcVer) -ForegroundColor Green
Write-Host ('备份目录：' + $backup)
Write-Host '以下内容保持原样，没有被覆盖：'
foreach ($k in $KeepList) { Write-Host ('  - ' + $k) }
Write-Host '注册表配置（PIN / 时长 / 时段 / 额度 / 加练池）与计划任务未改动。'
Write-Host ''
Write-Host '下一步：右键 PCLock.exe -> 以管理员身份运行（会幂等重建开机自启）。'
Write-Host '回滚方法：把备份目录里的 PCLock.exe / PCLock.exe.config 拷回安装目录。'

# 清理临时解压目录（失败不影响升级结果）
if (-not [string]::IsNullOrWhiteSpace($tmp)) {
    try { Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue } catch { }
}
exit 0
