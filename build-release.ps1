# ============================================================================
# CapsShow - 构建发布脚本
# 功能：编译框架依赖版本 → 调用 NSIS 生成安装包
# 用法：.\build-release.ps1 [-Version "1.0.0"] [-SkipClean]
# 注意：目标机器需安装 .NET 8 Desktop Runtime
# ============================================================================

param(
    [string]$Version   = "1.0.0",
    [switch]$SkipClean = $false
)

$ErrorActionPreference = "Stop"

# ---- 路径常量 ---------------------------------------------------------------
$ProjectRoot   = Split-Path -Parent $MyInvocation.MyCommand.Path
$PublishDir    = Join-Path $ProjectRoot "publish"
$DistDir       = Join-Path $ProjectRoot "dist"
$NsiScript     = Join-Path $ProjectRoot "setup.nsi"
$ProjectFile   = Join-Path $ProjectRoot "CapsShow.csproj"

# ---- 辅助函数 ---------------------------------------------------------------
function Write-Step([string]$msg) {
    Write-Host "`n>> $msg" -ForegroundColor Cyan
}

function Write-OK([string]$msg) {
    Write-Host "   [OK] $msg" -ForegroundColor Green
}

function Write-Fail([string]$msg) {
    Write-Host "   [FAIL] $msg" -ForegroundColor Red
    exit 1
}

function Find-MakeNSIS {
    # 1. PATH 中查找
    $found = Get-Command makensis.exe -ErrorAction SilentlyContinue
    if ($found) { return $found.Source }

    # 2. 常见安装路径
    $candidates = @(
        "${env:ProgramFiles(x86)}\NSIS\makensis.exe",
        "${env:ProgramFiles}\NSIS\makensis.exe",
        "$env:LOCALAPPDATA\NSIS\makensis.exe"
    )
    foreach ($p in $candidates) {
        if (Test-Path $p) { return $p }
    }

    # 3. Chocolatey / Scoop
    $chocoPath = Join-Path $env:ChocolateyInstall "bin\makensis.exe" -ErrorAction SilentlyContinue
    if ($chocoPath -and (Test-Path $chocoPath)) { return $chocoPath }

    $scoopPath = Join-Path $env:USERPROFILE "scoop\shims\makensis.exe"
    if (Test-Path $scoopPath) { return $scoopPath }

    return $null
}

# 带重试的目录删除（应对文件锁）
function Remove-DirectoryRetry {
    param([string]$Path, [int]$MaxRetry = 5, [int]$DelaySec = 2)
    for ($i = 1; $i -le $MaxRetry; $i++) {
        try {
            if (Test-Path $Path) { Remove-Item $Path -Recurse -Force -ErrorAction Stop }
            return
        } catch {
            Write-Host "  [!] 删除 $Path 失败 (尝试 $i/$MaxRetry)，${DelaySec}s 后重试..." -ForegroundColor Yellow
            Start-Sleep -Seconds $DelaySec
        }
    }
    throw "无法删除 $Path，已重试 $MaxRetry 次"
}

# ---- 开始 -------------------------------------------------------------------
Write-Host ""
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "  CapsShow  Build & Package" -ForegroundColor Yellow
Write-Host "  Version: $Version" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow

# Step 1: 查找 makensis
Write-Step "查找 NSIS 编译器 (makensis.exe)..."
$makensis = Find-MakeNSIS
if (-not $makensis) {
    Write-Fail "找不到 makensis.exe，请先安装 NSIS: https://nsis.sourceforge.io/Download"
}
Write-OK "找到: $makensis"

# Step 2: 校验 NSIS 脚本
if (-not (Test-Path $NsiScript)) {
    Write-Fail "找不到 NSIS 脚本: $NsiScript"
}

# Step 3: 清理旧产物
if (-not $SkipClean) {
    Write-Step "清理旧的构建产物..."
    Remove-DirectoryRetry $PublishDir
    Remove-DirectoryRetry $DistDir
    $binRelease = Join-Path $ProjectRoot "bin\Release"
    Remove-DirectoryRetry $binRelease
    Write-OK "已清理 publish/, dist/ 和 bin/Release/"
}

# Step 4: dotnet restore
Write-Step "还原 NuGet 依赖包..."
dotnet restore $ProjectFile
if ($LASTEXITCODE -ne 0) { Write-Fail "dotnet restore 失败 (exit=$LASTEXITCODE)" }
Write-OK "还原完成"

# Step 5: dotnet publish（框架依赖模式，目标机器需安装 .NET 8 Desktop Runtime）
Write-Step "发布框架依赖版本 (win-x64)..."
dotnet publish $ProjectFile `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -o $PublishDir `
    /p:DebugType=None `
    /p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) { Write-Fail "dotnet publish 失败 (exit=$LASTEXITCODE)" }
Write-OK "发布完成 → $PublishDir"

# Step 6: 确认主程序存在
$mainExe = Join-Path $PublishDir "CapsShow.exe"
if (-not (Test-Path $mainExe)) {
    Write-Fail "发布产物中找不到主程序: $mainExe"
}

# Step 7: 确保 dist 目录存在（NSIS 输出目录）
if (-not (Test-Path $DistDir)) {
    New-Item -ItemType Directory -Path $DistDir | Out-Null
}

# Step 8: 调用 makensis 打包
Write-Step "调用 NSIS 生成安装包..."
$nsisArgs = @(
    "/DAPP_VERSION=$Version",
    "/DSOURCE_DIR=$PublishDir",
    "/DDIST_DIR=$DistDir",
    $NsiScript
)

& $makensis @nsisArgs
if ($LASTEXITCODE -ne 0) { Write-Fail "makensis 失败 (exit=$LASTEXITCODE)" }

# Step 9: 确认输出
$installer = Join-Path $DistDir "CapsShow-Setup-v$Version.exe"
if (-not (Test-Path $installer)) {
    Write-Fail "安装包生成失败，预期文件不存在: $installer"
}

$sizeMB = [math]::Round((Get-Item $installer).Length / 1MB, 2)
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  构建成功！" -ForegroundColor Green
Write-Host "  安装包: $installer" -ForegroundColor White
Write-Host "  大小:   $sizeMB MB" -ForegroundColor White
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
