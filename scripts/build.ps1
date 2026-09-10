# ==============================================================================
# AvaloniaTodoApp 跨平台构建与发布脚本 (Windows PowerShell 对应版)
# 用法:
#   .\scripts\build.ps1                 # 默认构建 Windows x64 独立版本
#   .\scripts\build.ps1 -Target win-x64 # 构建指定平台
#   .\scripts\build.ps1 -Target all     # 构建所有支持平台
# ==============================================================================

param (
    [string]$Target = "win-x64"
)

$ErrorActionPreference = "Stop"
$ProjectDir = Split-Path -Parent $PSScriptRoot
$CsprojPath = Join-Path $ProjectDir "AvaloniaTodoApp.csproj"
$OutputRoot = Join-Path $ProjectDir "dist"

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ">>> 开始 Avalonia 桌面应用自动化构建流水线 (Windows) <<<" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan

function Publish-Platform($rid) {
    $outDir = Join-Path $OutputRoot $rid
    $zipFile = Join-Path $OutputRoot "AvaloniaTodoApp-$rid.zip"

    Write-Host "`n>>> [1/3] 正在发布平台: $rid ..." -ForegroundColor Yellow
    if (Test-Path $outDir) { Remove-Item -Recurse -Force $outDir }
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null

    dotnet publish $CsprojPath `
        -c Release `
        -r $rid `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $outDir `
        --nologo

    Write-Host ">>> [2/3] 编译发布成功: $outDir" -ForegroundColor Green

    Write-Host ">>> [3/3] 正在压缩产物为归档包: $zipFile ..." -ForegroundColor Yellow
    if (Test-Path $zipFile) { Remove-Item -Force $zipFile }
    Compress-Archive -Path "$outDir\*" -DestinationPath $zipFile -Force
    Write-Host "✓ 归档完成: $zipFile ($rid)" -ForegroundColor Green
}

if (-not (Test-Path $OutputRoot)) {
    New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
}

if ($Target -eq "all") {
    Publish-Platform "win-x64"
    Publish-Platform "osx-arm64"
    Publish-Platform "osx-x64"
    Publish-Platform "linux-x64"
} else {
    Publish-Platform $Target
}

Write-Host "`n======================================================" -ForegroundColor Green
Write-Host ">>> 所有构建任务完成！产物已输出至: $OutputRoot <<<" -ForegroundColor Green
Write-Host "======================================================" -ForegroundColor Green
